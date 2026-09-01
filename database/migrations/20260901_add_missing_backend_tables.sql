SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.ConferenceMeetings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ConferenceMeetings
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ConferenceMeetings PRIMARY KEY,
        Title nvarchar(255) NOT NULL,
        RoomName nvarchar(255) NOT NULL,
        ScheduledStartTime datetime2 NOT NULL,
        ScheduledEndTime datetime2 NULL,
        HostUserId nvarchar(450) NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.EmployeeRatings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeRatings
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmployeeRatings PRIMARY KEY,
        EmployeeId int NOT NULL,
        Score int NOT NULL,
        Feedback nvarchar(2000) NULL,
        RatedByUserId nvarchar(450) NOT NULL,
        RatedAt datetime2 NOT NULL,
        CONSTRAINT FK_EmployeeRatings_Employees_EmployeeId
            FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id)
    );
    CREATE INDEX IX_EmployeeRatings_EmployeeId_RatedAt
        ON dbo.EmployeeRatings(EmployeeId, RatedAt DESC);
END;

IF OBJECT_ID(N'dbo.EmployeeViolations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeViolations
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmployeeViolations PRIMARY KEY,
        EmployeeId int NOT NULL,
        Title nvarchar(255) NOT NULL,
        Description nvarchar(2000) NOT NULL,
        PenaltyAmount decimal(18,2) NOT NULL,
        OccurredAt datetime2 NOT NULL,
        IssuedByUserId nvarchar(450) NOT NULL,
        CONSTRAINT FK_EmployeeViolations_Employees_EmployeeId
            FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id)
    );
    CREATE INDEX IX_EmployeeViolations_EmployeeId_OccurredAt
        ON dbo.EmployeeViolations(EmployeeId, OccurredAt DESC);
END;

IF OBJECT_ID(N'dbo.UserSwitchGroups', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserSwitchGroups
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserSwitchGroups PRIMARY KEY,
        Name nvarchar(255) NOT NULL,
        CreatedByUserId nvarchar(450) NOT NULL,
        CreatedAt datetime2 NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.UserSwitchGroupMembers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserSwitchGroupMembers
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserSwitchGroupMembers PRIMARY KEY,
        UserSwitchGroupId int NOT NULL,
        UserId nvarchar(450) NOT NULL,
        CONSTRAINT FK_UserSwitchGroupMembers_UserSwitchGroups_UserSwitchGroupId
            FOREIGN KEY (UserSwitchGroupId) REFERENCES dbo.UserSwitchGroups(Id)
    );
    CREATE UNIQUE INDEX UX_UserSwitchGroupMembers_Group_User
        ON dbo.UserSwitchGroupMembers(UserSwitchGroupId, UserId);
END;

COMMIT TRANSACTION;
