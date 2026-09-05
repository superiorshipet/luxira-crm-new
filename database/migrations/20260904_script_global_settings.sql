IF OBJECT_ID(N'dbo.ScriptGlobalSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScriptGlobalSettings
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScriptGlobalSettings PRIMARY KEY,
        [Key] nvarchar(64) NOT NULL,
        [Value] nvarchar(256) NOT NULL,
        UpdatedAt datetime2 NULL,
        UpdatedBy nvarchar(256) NULL
    );
    CREATE UNIQUE INDEX IX_ScriptGlobalSettings_Key ON dbo.ScriptGlobalSettings([Key]);
END;
