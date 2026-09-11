using System;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using CampusQ.Core.Infrastructure.Configuration;

namespace CampusQ.MVP.Data
{
    public static class DbConfig
    {
        private static ILogger? _logger;
        private static string? _connectionString;
        private static ConfigurationService? _configService;

        /// <summary>
        /// Gets or sets the database connection string.
        /// Should be set via SetConnectionString() or will default to configuration.
        /// </summary>
        public static string ConnectionString
        {
            get
            {
                if (_connectionString != null)
                    return _connectionString;

                if (_configService != null)
                {
                    _connectionString = _configService.GetDatabaseConnectionString();
                    return _connectionString;
                }

                // Fallback: try environment variable
                var envConnStr = Environment.GetEnvironmentVariable("CAMPUSQ_CONNECTION_STRING");
                if (!string.IsNullOrEmpty(envConnStr))
                {
                    _connectionString = envConnStr;
                    return _connectionString;
                }

                // Ultimate fallback for local development
                _connectionString = "Data Source=.\\SQLEXPRESS;Initial Catalog=CampusQ;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
                return _connectionString;
            }
            set { _connectionString = value; }
        }

        /// <summary>
        /// Initializes DbConfig with a configuration service (optional, for DI scenarios).
        /// </summary>
        public static void Initialize(ConfigurationService configService)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        public static void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

        private static void Log(LogLevel level, string message, params object[] args)
        {
            _logger?.Log(level, message, args);
            if (_logger == null)
            {
                System.Diagnostics.Debug.WriteLine(string.Format(message, args));
            }
        }

        private static void LogError(string message, Exception ex)
        {
            _logger?.LogError(ex, message);
            if (_logger == null)
            {
                System.Diagnostics.Debug.WriteLine($"{message}: {ex.Message}");
            }
        }

        public static void EnsureDatabaseAndTables()
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(ConnectionString);
                var database = builder.InitialCatalog;
                if (string.IsNullOrWhiteSpace(database))
                {
                    // fallback name
                    database = "CampusQ";
                    builder.InitialCatalog = database;
                    ConnectionString = builder.ConnectionString;
                }

                var masterBuilder = new SqlConnectionStringBuilder(ConnectionString)
                {
                    InitialCatalog = "master"
                };

                using (var conn = new SqlConnection(masterBuilder.ConnectionString))
                {
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"IF DB_ID(N'{database}') IS NULL CREATE DATABASE [{database}];";
                    cmd.ExecuteNonQuery();
                    Log(LogLevel.Information, "Database '{0}' verified/created", database);
                }

                var targetBuilder = new SqlConnectionStringBuilder(ConnectionString)
                {
                    InitialCatalog = database
                };

                using (var conn = new SqlConnection(targetBuilder.ConnectionString))
                {
                    conn.Open();
                    using var cmd = conn.CreateCommand();

                    cmd.CommandText = @"IF OBJECT_ID(N'dbo.Users') IS NULL
BEGIN
CREATE TABLE dbo.Users(
    Username NVARCHAR(100) PRIMARY KEY,
    PasswordHash NVARCHAR(MAX) NOT NULL,
    Salt NVARCHAR(200) NOT NULL,
    Role NVARCHAR(50) NOT NULL,
    CreatedAt DATETIME2 NOT NULL
);
END";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"IF OBJECT_ID(N'dbo.QueueTransfers') IS NULL
BEGIN
CREATE TABLE dbo.QueueTransfers(
    TransferId BIGINT IDENTITY(1,1) PRIMARY KEY,
    TicketNumber INT NOT NULL,
    SourceService NVARCHAR(100) NOT NULL,
    TargetService NVARCHAR(100) NOT NULL,
    Reason NVARCHAR(500) NOT NULL,
    Actor NVARCHAR(100) NOT NULL,
    TransferredAt DATETIME2 NOT NULL
);
CREATE INDEX IX_QueueTransfers_TicketNumber ON dbo.QueueTransfers (TicketNumber, TransferredAt DESC);
END";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"IF OBJECT_ID(N'dbo.Queue') IS NULL
BEGIN
CREATE TABLE dbo.Queue(
    TicketNumber INT IDENTITY(1,1) PRIMARY KEY,
    ServiceTicketNumber INT NOT NULL DEFAULT(0),
    Purpose NVARCHAR(200) NOT NULL,
    Service NVARCHAR(100) NOT NULL,
    TimeAdded DATETIME2 NOT NULL
);
END";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"IF OBJECT_ID(N'dbo.QueueHistory') IS NULL
BEGIN
CREATE TABLE dbo.QueueHistory(
    TicketNumber INT PRIMARY KEY,
    ServiceTicketNumber INT NOT NULL DEFAULT(0),
    Purpose NVARCHAR(200) NOT NULL,
    Service NVARCHAR(100) NOT NULL,
    TimeAdded DATETIME2 NOT NULL,
    ServedAt DATETIME2 NOT NULL
);
END";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"IF OBJECT_ID(N'dbo.ServiceWindows') IS NULL
BEGIN
CREATE TABLE dbo.ServiceWindows(
    WindowNumber INT NOT NULL PRIMARY KEY,
    IsActive BIT NOT NULL DEFAULT(1),
    UpdatedAt DATETIME2 NOT NULL
);
INSERT INTO dbo.ServiceWindows (WindowNumber, IsActive, UpdatedAt)
VALUES (1, 1, SYSUTCDATETIME()), (2, 1, SYSUTCDATETIME()),
       (3, 1, SYSUTCDATETIME()), (4, 1, SYSUTCDATETIME());
END";
                    cmd.ExecuteNonQuery();

                    // Create indices for performance optimization
                    CreateIndices(conn);
                }

                Log(LogLevel.Information, "Database and tables successfully ensured");
            }
            catch (Exception ex)
            {
                LogError("EnsureDatabaseAndTables failed", ex);
                throw;
            }
        }

        /// <summary>
        /// Creates performance indices on Queue and QueueHistory tables for Service, Purpose, and TimeAdded filtering.
        /// Indices help optimize queries for admission, cashier, and registrar queue filtering.
        /// </summary>
        private static void CreateIndices(SqlConnection conn)
        {
            try
            {
                using var cmd = conn.CreateCommand();

                // Index on Queue table for Service filtering (used for Admission, Cashier, Registrar queries)
                cmd.CommandText = @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Queue_Service' AND object_id = OBJECT_ID('dbo.Queue'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Queue_Service ON dbo.Queue (Service) INCLUDE (ServiceTicketNumber, Purpose, TimeAdded);
END";
                cmd.ExecuteNonQuery();

                // Index on Queue table for Service + Purpose filtering (specific queue filtering)
                cmd.CommandText = @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Queue_Service_Purpose' AND object_id = OBJECT_ID('dbo.Queue'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Queue_Service_Purpose ON dbo.Queue (Service, Purpose) INCLUDE (ServiceTicketNumber, TimeAdded);
END";
                cmd.ExecuteNonQuery();

                // Index on Queue table for TimeAdded sorting
                cmd.CommandText = @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Queue_TimeAdded' AND object_id = OBJECT_ID('dbo.Queue'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Queue_TimeAdded ON dbo.Queue (TimeAdded) INCLUDE (Service, Purpose, ServiceTicketNumber);
END";
                cmd.ExecuteNonQuery();

                // Index on QueueHistory table for Service filtering
                cmd.CommandText = @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QueueHistory_Service' AND object_id = OBJECT_ID('dbo.QueueHistory'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_QueueHistory_Service ON dbo.QueueHistory (Service) INCLUDE (ServiceTicketNumber, Purpose, TimeAdded, ServedAt);
END";
                cmd.ExecuteNonQuery();

                // Index on QueueHistory table for ServedAt sorting (historical reports)
                cmd.CommandText = @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QueueHistory_ServedAt' AND object_id = OBJECT_ID('dbo.QueueHistory'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_QueueHistory_ServedAt ON dbo.QueueHistory (ServedAt DESC) INCLUDE (Service, Purpose, TimeAdded);
END";
                cmd.ExecuteNonQuery();

                Log(LogLevel.Information, "Database indices created successfully");
            }
            catch (Exception ex)
            {
                LogError("CreateIndices failed", ex);
                // Non-critical error - don't throw, just log
            }
        }
    }
}
