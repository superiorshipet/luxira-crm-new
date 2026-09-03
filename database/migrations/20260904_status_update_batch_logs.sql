SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.StatusUpdateBatchLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StatusUpdateBatchLogs
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_StatusUpdateBatchLogs PRIMARY KEY,
        BatchKey uniqueidentifier NOT NULL,
        EmployeeUserId nvarchar(450) NULL,
        EmployeeName nvarchar(250) NULL,
        EmployeeImageUrl nvarchar(1000) NULL,
        CountryName nvarchar(120) NULL,
        StoreId int NULL,
        StoreName nvarchar(250) NULL,
        FinalStatusValue int NOT NULL,
        FinalStatusName nvarchar(120) NOT NULL,
        OrderCount int NOT NULL,
        UpdatedAt datetime2 NOT NULL
    );
    CREATE INDEX IX_StatusUpdateBatchLogs_UpdatedAt ON dbo.StatusUpdateBatchLogs(UpdatedAt);
    CREATE INDEX IX_StatusUpdateBatchLogs_BatchKey ON dbo.StatusUpdateBatchLogs(BatchKey);
    CREATE INDEX IX_StatusUpdateBatchLogs_EmployeeUserId_UpdatedAt ON dbo.StatusUpdateBatchLogs(EmployeeUserId, UpdatedAt);
END;

IF OBJECT_ID(N'dbo.StatusUpdateBatchLogItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StatusUpdateBatchLogItems
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_StatusUpdateBatchLogItems PRIMARY KEY,
        BatchLogId int NOT NULL,
        OrderId int NOT NULL,
        OrderCode nvarchar(80) NOT NULL,
        FinalStatusValue int NOT NULL,
        FinalStatusName nvarchar(120) NOT NULL,
        FailureReason nvarchar(500) NULL,
        DeliveryCompanyName nvarchar(250) NULL,
        CountryName nvarchar(120) NULL,
        StoreName nvarchar(250) NULL,
        UpdatedAt datetime2 NOT NULL,
        CONSTRAINT FK_StatusUpdateBatchLogItems_StatusUpdateBatchLogs_BatchLogId FOREIGN KEY (BatchLogId) REFERENCES dbo.StatusUpdateBatchLogs(Id)
    );
    CREATE INDEX IX_StatusUpdateBatchLogItems_BatchLogId ON dbo.StatusUpdateBatchLogItems(BatchLogId);
    CREATE INDEX IX_StatusUpdateBatchLogItems_OrderId ON dbo.StatusUpdateBatchLogItems(OrderId);
    CREATE INDEX IX_StatusUpdateBatchLogItems_UpdatedAt ON dbo.StatusUpdateBatchLogItems(UpdatedAt);
END;

COMMIT TRANSACTION;
