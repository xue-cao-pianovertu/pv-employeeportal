CREATE TABLE dbo.SalesStaff (
    id         INT            IDENTITY(1,1) PRIMARY KEY,
    name       NVARCHAR(100)  NOT NULL,
    is_active  BIT            NOT NULL DEFAULT 1,
    created_at DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
);
