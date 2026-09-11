-- ============================================================================
-- CampusQ Database Setup Script
-- This script creates/updates the CampusQ database with all necessary tables
-- and performance indices for Admission, Cashier, and Registrar queue management.
-- ============================================================================

-- Create database if it doesn't exist
IF DB_ID(N'CampusQ') IS NULL
	CREATE DATABASE [CampusQ];
GO

-- Switch to CampusQ database
USE [CampusQ];
GO

-- ============================================================================
-- Create Users Table
-- ============================================================================
IF OBJECT_ID(N'dbo.Users') IS NULL
BEGIN
	CREATE TABLE dbo.Users(
		Username NVARCHAR(100) PRIMARY KEY,
		PasswordHash NVARCHAR(MAX) NOT NULL,
		Salt NVARCHAR(200) NOT NULL,
		Role NVARCHAR(50) NOT NULL,
		CreatedAt DATETIME2 NOT NULL
	);

	PRINT 'Created Users table';
END
ELSE
	PRINT 'Users table already exists';
GO

-- ============================================================================
-- Create Queue Table (Active Queue)
-- Stores current tickets waiting to be served for Admission, Cashier, Registrar
-- ============================================================================
IF OBJECT_ID(N'dbo.Queue') IS NULL
BEGIN
	CREATE TABLE dbo.Queue(
		TicketNumber INT IDENTITY(1,1) PRIMARY KEY,
		ServiceTicketNumber INT NOT NULL DEFAULT(0),
		Purpose NVARCHAR(200) NOT NULL,
		Service NVARCHAR(100) NOT NULL,
		TimeAdded DATETIME2 NOT NULL,
		IsPriority BIT NOT NULL DEFAULT(0)
	);

	PRINT 'Created Queue table';
END
ELSE
	PRINT 'Queue table already exists';
GO

-- Add priority support to existing Queue installations
IF COL_LENGTH('dbo.Queue','IsPriority') IS NULL
    ALTER TABLE dbo.Queue ADD IsPriority BIT NOT NULL CONSTRAINT DF_Queue_IsPriority DEFAULT(0);
GO

-- ============================================================================
-- Create QueueHistory Table (Served/Historical Queue)
-- Stores completed/served tickets for audit trail and reporting
-- ============================================================================
IF OBJECT_ID(N'dbo.QueueHistory') IS NULL
BEGIN
	CREATE TABLE dbo.QueueHistory(
		TicketNumber INT PRIMARY KEY,
		ServiceTicketNumber INT NOT NULL DEFAULT(0),
		Purpose NVARCHAR(200) NOT NULL,
		Service NVARCHAR(100) NOT NULL,
		TimeAdded DATETIME2 NOT NULL,
		ServedAt DATETIME2 NOT NULL,
		IsPriority BIT NOT NULL DEFAULT(0)
	);

	PRINT 'Created QueueHistory table';
END
ELSE
	PRINT 'QueueHistory table already exists';
GO

-- Add priority to history for reporting/audit
IF COL_LENGTH('dbo.QueueHistory','IsPriority') IS NULL
    ALTER TABLE dbo.QueueHistory ADD IsPriority BIT NOT NULL CONSTRAINT DF_QueueHistory_IsPriority DEFAULT(0);
GO

-- ============================================================================
-- Create ServiceWindows Table
-- ============================================================================
IF OBJECT_ID(N'dbo.ServiceWindows') IS NULL
BEGIN
	CREATE TABLE dbo.ServiceWindows(
		WindowNumber INT NOT NULL PRIMARY KEY,
		IsActive BIT NOT NULL DEFAULT(1),
		UpdatedAt DATETIME2 NOT NULL
	);
	INSERT INTO dbo.ServiceWindows (WindowNumber, IsActive, UpdatedAt)
	VALUES (1, 1, SYSUTCDATETIME()), (2, 1, SYSUTCDATETIME()),
		   (3, 1, SYSUTCDATETIME()), (4, 1, SYSUTCDATETIME());
	PRINT 'Created ServiceWindows table';
END
ELSE
	PRINT 'ServiceWindows table already exists';
GO

-- ============================================================================
-- Create QueueTransfers Table (Immutable Transfer Audit)
-- ============================================================================
IF OBJECT_ID(N'dbo.QueueTransfers') IS NULL
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
	PRINT 'Created QueueTransfers table';
END
ELSE
	PRINT 'QueueTransfers table already exists';
GO

-- ============================================================================
-- Create Indices for Performance Optimization
-- These indices optimize queue filtering by Service (Admission, Cashier, Registrar)
-- and sorting operations
-- ============================================================================

PRINT 'Creating performance indices...';
GO

-- Queue Table Indices
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Queue_Service_Priority' AND object_id = OBJECT_ID('dbo.Queue'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Queue_Service_Priority ON dbo.Queue (Service, IsPriority DESC, TimeAdded, TicketNumber) INCLUDE (Purpose, ServiceTicketNumber);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Queue_Service' AND object_id = OBJECT_ID('dbo.Queue'))
BEGIN
	CREATE NONCLUSTERED INDEX IX_Queue_Service ON dbo.Queue (Service) 
	INCLUDE (ServiceTicketNumber, Purpose, TimeAdded);
	PRINT '  ✓ Created IX_Queue_Service';
END
ELSE
	PRINT '  ○ IX_Queue_Service already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Queue_Service_Purpose' AND object_id = OBJECT_ID('dbo.Queue'))
BEGIN
	CREATE NONCLUSTERED INDEX IX_Queue_Service_Purpose ON dbo.Queue (Service, Purpose) 
	INCLUDE (ServiceTicketNumber, TimeAdded);
	PRINT '  ✓ Created IX_Queue_Service_Purpose';
END
ELSE
	PRINT '  ○ IX_Queue_Service_Purpose already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Queue_TimeAdded' AND object_id = OBJECT_ID('dbo.Queue'))
BEGIN
	CREATE NONCLUSTERED INDEX IX_Queue_TimeAdded ON dbo.Queue (TimeAdded) 
	INCLUDE (Service, Purpose, ServiceTicketNumber);
	PRINT '  ✓ Created IX_Queue_TimeAdded';
END
ELSE
	PRINT '  ○ IX_Queue_TimeAdded already exists';
GO

-- QueueHistory Table Indices
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QueueHistory_Service' AND object_id = OBJECT_ID('dbo.QueueHistory'))
BEGIN
	CREATE NONCLUSTERED INDEX IX_QueueHistory_Service ON dbo.QueueHistory (Service) 
	INCLUDE (ServiceTicketNumber, Purpose, TimeAdded, ServedAt);
	PRINT '  ✓ Created IX_QueueHistory_Service';
END
ELSE
	PRINT '  ○ IX_QueueHistory_Service already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QueueHistory_ServedAt' AND object_id = OBJECT_ID('dbo.QueueHistory'))
BEGIN
	CREATE NONCLUSTERED INDEX IX_QueueHistory_ServedAt ON dbo.QueueHistory (ServedAt DESC) 
	INCLUDE (Service, Purpose, TimeAdded);
	PRINT '  ✓ Created IX_QueueHistory_ServedAt';
END
ELSE
	PRINT '  ○ IX_QueueHistory_ServedAt already exists';
GO

-- ============================================================================
-- Verification Queries
-- ============================================================================
PRINT '';
PRINT '=== Database Verification ===';
GO

-- Check table counts
SELECT 
	'Users' AS TableName,
	COUNT(*) AS [RowCount]
FROM dbo.Users
UNION ALL
SELECT 
	'Queue' AS TableName,
	COUNT(*) AS [RowCount]
FROM dbo.Queue
UNION ALL
SELECT 
	'QueueHistory' AS TableName,
	COUNT(*) AS [RowCount]
FROM dbo.QueueHistory
ORDER BY TableName;
GO

-- Check queue distribution by service
PRINT '';
PRINT 'Active Queue Distribution by Service:';
SELECT 
	Service,
	COUNT(*) AS Count,
	COUNT(CASE WHEN Service = 'Admission' THEN 1 END) AS Admission,
	COUNT(CASE WHEN Service = 'Cashier' THEN 1 END) AS Cashier,
	COUNT(CASE WHEN Service = 'Registrar' THEN 1 END) AS Registrar
FROM dbo.Queue
GROUP BY Service
UNION ALL
SELECT
	'TOTAL',
	COUNT(*),
	COUNT(CASE WHEN Service = 'Admission' THEN 1 END),
	COUNT(CASE WHEN Service = 'Cashier' THEN 1 END),
	COUNT(CASE WHEN Service = 'Registrar' THEN 1 END)
FROM dbo.Queue;
GO

-- Check indices
PRINT '';
PRINT 'Database Indices:';
SELECT 
	t.name AS TableName,
	i.name AS IndexName,
	i.type_desc AS IndexType
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE t.name IN ('Queue', 'QueueHistory', 'Users')
AND i.name IS NOT NULL
ORDER BY t.name, i.name;
GO

PRINT '';
PRINT '=== Database Setup Complete ===';
