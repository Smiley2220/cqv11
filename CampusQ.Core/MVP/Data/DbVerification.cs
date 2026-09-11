using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace CampusQ.MVP.Data
{
    /// <summary>
    /// Database verification utility to test connectivity, schema, and indices.
    /// Can be run at startup to ensure database is properly configured.
    /// </summary>
    public static class DbVerification
    {
        private static ILogger? _logger;

        public static void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

        private static void Log(LogLevel level, string message, params object[] args)
        {
            _logger?.Log(level, message, args);
            // Fallback to Debug output if no logger is configured
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

        public static bool VerifyDatabase(string connectionString)
        {
            try
            {
                Log(LogLevel.Information, "=== Database Verification Started ===");

                // Ensure database exists first
                DbConfig.EnsureDatabaseAndTables();
                Log(LogLevel.Information, "✓ Database and tables initialized");

                // Test connection
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    Log(LogLevel.Information, "✓ Database connection successful");

                    // Verify Queue table structure
                    if (!VerifyTableExists(conn, "Queue"))
                    {
                        Log(LogLevel.Error, "✗ Queue table not found");
                        return false;
                    }
                    Log(LogLevel.Information, "✓ Queue table exists");

                    // Verify QueueHistory table structure
                    if (!VerifyTableExists(conn, "QueueHistory"))
                    {
                        Log(LogLevel.Error, "✗ QueueHistory table not found");
                        return false;
                    }
                    Log(LogLevel.Information, "✓ QueueHistory table exists");

                    // Verify Users table structure
                    if (!VerifyTableExists(conn, "Users"))
                    {
                        Log(LogLevel.Error, "✗ Users table not found");
                        return false;
                    }
                    Log(LogLevel.Information, "✓ Users table exists");

                    // Verify indices
                    var indices = GetTableIndices(conn, "Queue");
                    Log(LogLevel.Information, "✓ Queue table has {0} indices: {1}", indices.Count, string.Join(", ", indices));

                    var historyIndices = GetTableIndices(conn, "QueueHistory");
                    Log(LogLevel.Information, "✓ QueueHistory table has {0} indices: {1}", historyIndices.Count, string.Join(", ", historyIndices));

                    // Test service-specific queries
                    if (!TestServiceQueries(conn))
                    {
                        Log(LogLevel.Error, "✗ Service queries failed");
                        return false;
                    }
                    Log(LogLevel.Information, "✓ Service queries (Admission, Cashier, Registrar) verified");
                }

                Log(LogLevel.Information, "=== Database Verification Completed Successfully ===");
                return true;
            }
            catch (Exception ex)
            {
                LogError("=== Database Verification Failed ===", ex);
                return false;
            }
        }

        private static bool VerifyTableExists(SqlConnection conn, string tableName)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}'";
            var result = cmd.ExecuteScalar();
            return result != null;
        }

        private static List<string> GetTableIndices(SqlConnection conn, string tableName)
        {
            var indices = new List<string>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"SELECT name FROM sys.indexes 
                                 WHERE object_id = OBJECT_ID('dbo.{tableName}') 
                                 AND name IS NOT NULL 
                                 ORDER BY name";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                indices.Add(reader.GetString(0));
            }
            return indices;
        }

        private static bool TestServiceQueries(SqlConnection conn)
        {
            try
            {
                var services = new[] { "Admission", "Cashier", "Registrar" };

                foreach (var service in services)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM dbo.Queue WHERE Service = @service";
                    cmd.Parameters.AddWithValue("@service", service);
                    var count = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                    Log(LogLevel.Debug, "  - {0} queue count: {1}", service, count);
                }

                return true;
            }
            catch (Exception ex)
            {
                LogError("Service query test failed", ex);
                return false;
            }
        }

        /// <summary>
        /// Gets database statistics for monitoring and reporting.
        /// </summary>
        public static DatabaseStats GetDatabaseStats(string connectionString)
        {
            var stats = new DatabaseStats();

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Total queue entries
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM dbo.Queue";
                        stats.TotalActiveQueueEntries = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                    }

                    // Queue entries by service
                    foreach (var service in new[] { "Admission", "Cashier", "Registrar" })
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT COUNT(*) FROM dbo.Queue WHERE Service = @service";
                            cmd.Parameters.AddWithValue("@service", service);
                            var count = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                            stats.QueueCountByService[service] = count;
                        }
                    }

                    // Historical entries
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM dbo.QueueHistory";
                        stats.TotalHistoricalEntries = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                    }

                    // User accounts
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM dbo.Users";
                        stats.TotalUserAccounts = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                    }

                    stats.LastVerifiedAt = DateTime.Now;
                    stats.IsHealthy = true;
                }
            }
            catch (Exception ex)
            {
                LogError("GetDatabaseStats failed", ex);
                stats.IsHealthy = false;
                stats.LastError = ex.Message;
            }

            return stats;
        }
    }

    public class DatabaseStats
    {
        public int TotalActiveQueueEntries { get; set; }
        public Dictionary<string, int> QueueCountByService { get; set; } = new Dictionary<string, int>();
        public int TotalHistoricalEntries { get; set; }
        public int TotalUserAccounts { get; set; }
        public DateTime LastVerifiedAt { get; set; }
        public bool IsHealthy { get; set; }
        public string LastError { get; set; }

        public override string ToString()
        {
            return $@"Database Statistics (as of {LastVerifiedAt:g}):
  Health Status: {(IsHealthy ? "✓ Healthy" : "✗ Unhealthy")}
  Active Queue Entries: {TotalActiveQueueEntries}
    - Admission: {QueueCountByService.GetValueOrDefault("Admission", 0)}
    - Cashier: {QueueCountByService.GetValueOrDefault("Cashier", 0)}
    - Registrar: {QueueCountByService.GetValueOrDefault("Registrar", 0)}
  Historical Entries: {TotalHistoricalEntries}
  User Accounts: {TotalUserAccounts}
{(string.IsNullOrEmpty(LastError) ? "" : $"  Last Error: {LastError}")}";
        }
    }
}
