SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.OrderStatusHistoryDeliveryCompanySnapshots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderStatusHistoryDeliveryCompanySnapshots
    (
        OrderStatusHistoryId INT NOT NULL CONSTRAINT PK_OrderStatusHistoryDeliveryCompanySnapshots PRIMARY KEY,
        OrderId INT NOT NULL,
        DeliveryCompanyId INT NULL,
        DeliveryCompanyName NVARCHAR(300) NULL,
        CapturedAt DATETIME2(7) NOT NULL CONSTRAINT DF_OrderStatusHistoryDeliveryCompanySnapshots_CapturedAt DEFAULT SYSDATETIME()
    );
    CREATE INDEX IX_OrderStatusHistoryDeliveryCompanySnapshots_OrderId
        ON dbo.OrderStatusHistoryDeliveryCompanySnapshots(OrderId, OrderStatusHistoryId);
END;

EXEC(N'
CREATE OR ALTER TRIGGER dbo.TR_OrderStatusHistories_DeliveryCompanySnapshot
ON dbo.OrderStatusHistories
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE target
       SET target.OrderId = source.OrderId,
           target.DeliveryCompanyId = source.DeliveryCompanyId,
           target.DeliveryCompanyName = source.DeliveryCompanyName,
           target.CapturedAt = SYSDATETIME()
    FROM dbo.OrderStatusHistoryDeliveryCompanySnapshots target
    INNER JOIN
    (
        SELECT insertedRow.Id AS OrderStatusHistoryId, insertedRow.OrderId,
               currentOrder.DeliveryCompanyId,
               COALESCE(NULLIF(LTRIM(RTRIM(company.DisplayName)), N''''), NULLIF(LTRIM(RTRIM(company.Name)), N''''), N'''') AS DeliveryCompanyName
        FROM inserted insertedRow
        LEFT JOIN dbo.Orders currentOrder ON currentOrder.Id = insertedRow.OrderId
        LEFT JOIN dbo.DeliveryCompanies company ON company.Id = currentOrder.DeliveryCompanyId
    ) source ON source.OrderStatusHistoryId = target.OrderStatusHistoryId;

    INSERT INTO dbo.OrderStatusHistoryDeliveryCompanySnapshots
        (OrderStatusHistoryId, OrderId, DeliveryCompanyId, DeliveryCompanyName, CapturedAt)
    SELECT insertedRow.Id, insertedRow.OrderId, currentOrder.DeliveryCompanyId,
           COALESCE(NULLIF(LTRIM(RTRIM(company.DisplayName)), N''''), NULLIF(LTRIM(RTRIM(company.Name)), N''''), N''''),
           SYSDATETIME()
    FROM inserted insertedRow
    LEFT JOIN dbo.Orders currentOrder ON currentOrder.Id = insertedRow.OrderId
    LEFT JOIN dbo.DeliveryCompanies company ON company.Id = currentOrder.DeliveryCompanyId
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.OrderStatusHistoryDeliveryCompanySnapshots existing
        WHERE existing.OrderStatusHistoryId = insertedRow.Id
    );
END;');

COMMIT TRANSACTION;
