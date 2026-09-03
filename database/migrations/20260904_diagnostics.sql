SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.AppLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppLogs
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppLogs PRIMARY KEY,
        CreatedAtUtc datetime2 NOT NULL,
        Level nvarchar(32) NOT NULL,
        Category nvarchar(256) NOT NULL,
        Message nvarchar(max) NOT NULL,
        Exception nvarchar(max) NULL,
        Type nvarchar(32) NULL,
        Kind nvarchar(64) NULL
    );
    CREATE INDEX IX_AppLogs_CreatedAtUtc ON dbo.AppLogs(CreatedAtUtc);
    CREATE INDEX IX_AppLogs_Type_Kind_CreatedAtUtc ON dbo.AppLogs(Type, Kind, CreatedAtUtc);
END;

IF OBJECT_ID(N'dbo.AppMetrics', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppMetrics
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppMetrics PRIMARY KEY,
        CreatedAtUtc datetime2 NOT NULL,
        Kind nvarchar(64) NOT NULL,
        DurationMs float NOT NULL,
        Path nvarchar(300) NULL,
        UserName nvarchar(128) NULL,
        Serial int NULL,
        Label nvarchar(400) NULL,
        SqlCount int NULL,
        SqlTotalMs float NULL,
        [RowCount] int NULL,
        MetricsJson nvarchar(max) NULL,
        Detail nvarchar(max) NULL
    );
    CREATE INDEX IX_AppMetrics_CreatedAtUtc ON dbo.AppMetrics(CreatedAtUtc);
    CREATE INDEX IX_AppMetrics_Kind_CreatedAtUtc ON dbo.AppMetrics(Kind, CreatedAtUtc);
END;

COMMIT TRANSACTION;
