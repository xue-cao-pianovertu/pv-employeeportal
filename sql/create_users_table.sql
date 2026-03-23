-- ─────────────────────────────────────────────────────────────────────────────
-- Create Users table (Phase 3 — Auth)
-- Run once in Azure SQL
-- Note: passwords stored as plain text for now; BCrypt added in Phase 6
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE dbo.Users (
    id          INT            IDENTITY(1,1) PRIMARY KEY,
    username    NVARCHAR(100)  NOT NULL UNIQUE,
    password    NVARCHAR(200)  NOT NULL,    -- plain text until Phase 6
    role        NVARCHAR(20)   NOT NULL,    -- staff | admin | tuner | mover | teacher
    full_name   NVARCHAR(200),
    is_active   BIT            NOT NULL DEFAULT 1,
    created_at  DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
);

-- ── Seed first staff account (change password immediately after setup) ────────
INSERT INTO dbo.Users (username, password, role, full_name)
VALUES ('admin', 'changeme', 'admin', N'Administrateur');
