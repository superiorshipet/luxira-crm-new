SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.AdminEmployeeNotificationReplyStates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdminEmployeeNotificationReplyStates
    (
        AdminNotificationId int NOT NULL CONSTRAINT PK_AdminEmployeeNotificationReplyStates PRIMARY KEY,
        RequiresReply bit NOT NULL,
        ReplyText nvarchar(1000) NULL,
        RepliedAt datetimeoffset NULL,
        ReplySeenByAdmin bit NOT NULL,
        CONSTRAINT FK_AdminEmployeeNotificationReplyStates_AdminEmployeeNotifications_AdminNotificationId
            FOREIGN KEY (AdminNotificationId) REFERENCES dbo.AdminEmployeeNotifications(Id)
    );
END;

COMMIT TRANSACTION;
