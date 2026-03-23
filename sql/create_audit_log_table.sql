-- Audit log: who changed what on which registration
CREATE TABLE dbo.AuditLog (
    id              INT IDENTITY(1,1) PRIMARY KEY,
    registration_id INT             NOT NULL,
    changed_by      NVARCHAR(100)   NOT NULL,
    changed_at      DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    section         NVARCHAR(20)    NOT NULL,   -- 'staff' or 'client'
    changes_json    NVARCHAR(MAX)   NOT NULL
);

CREATE INDEX IX_AuditLog_registration ON dbo.AuditLog (registration_id, changed_at DESC);
