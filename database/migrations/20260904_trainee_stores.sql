SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.TraineeStores', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TraineeStores
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TraineeStores PRIMARY KEY,
        Name nvarchar(200) NOT NULL,
        PhoneNumber nvarchar(50) NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_TraineeStores_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt datetime2 NULL
    );
END;

IF OBJECT_ID(N'dbo.TraineeStoreManufacturingCompanies', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TraineeStoreManufacturingCompanies
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TraineeStoreManufacturingCompanies PRIMARY KEY,
        TraineeStoreId int NOT NULL,
        ManufacturingCompanyId int NOT NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_TraineeStoreManufacturingCompanies_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_TraineeStoreManufacturingCompanies_TraineeStores_TraineeStoreId
            FOREIGN KEY (TraineeStoreId) REFERENCES dbo.TraineeStores(Id) ON DELETE CASCADE,
        CONSTRAINT FK_TraineeStoreManufacturingCompanies_ManufacturingCompanies_ManufacturingCompanyId
            FOREIGN KEY (ManufacturingCompanyId) REFERENCES dbo.ManufacturingCompanies(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_TraineeStoreManufacturingCompanies_TraineeStoreId
        ON dbo.TraineeStoreManufacturingCompanies(TraineeStoreId);
    CREATE INDEX IX_TraineeStoreManufacturingCompanies_ManufacturingCompanyId
        ON dbo.TraineeStoreManufacturingCompanies(ManufacturingCompanyId);
    CREATE UNIQUE INDEX UX_TraineeStoreManufacturingCompanies_TraineeStoreId_ManufacturingCompanyId
        ON dbo.TraineeStoreManufacturingCompanies(TraineeStoreId, ManufacturingCompanyId);
END;

COMMIT TRANSACTION;
