CREATE TABLE dbo.Events (
    id                INT           IDENTITY(1,1) PRIMARY KEY,
    event_code        NVARCHAR(30)  NOT NULL UNIQUE,
    event_name        NVARCHAR(200) NOT NULL,
    event_date        DATE          NULL,
    promo_text        NVARCHAR(MAX) NULL,
    draw_count        INT           NOT NULL DEFAULT 1,
    prize_description NVARCHAR(200) NULL,
    is_active         BIT           NOT NULL DEFAULT 1,
    created_at        DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.EventGuests (
    id          INT           IDENTITY(1,1) PRIMARY KEY,
    event_id    INT           NOT NULL REFERENCES dbo.Events(id),
    first_name  NVARCHAR(100) NOT NULL,
    last_name   NVARCHAR(100) NOT NULL,
    email       NVARCHAR(200) NOT NULL,
    phone       NVARCHAR(50)  NULL,
    created_at  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_EventGuest_Email UNIQUE (event_id, email)
);

CREATE TABLE dbo.EventDraws (
    id              INT           IDENTITY(1,1) PRIMARY KEY,
    event_id        INT           NOT NULL REFERENCES dbo.Events(id),
    winner_guest_id INT           NOT NULL REFERENCES dbo.EventGuests(id),
    draw_number     INT           NOT NULL,
    drawn_at        DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    drawn_by        NVARCHAR(100) NULL
);

CREATE INDEX IX_Events_EventCode    ON dbo.Events (event_code);
CREATE INDEX IX_EventGuests_EventId ON dbo.EventGuests (event_id);
CREATE INDEX IX_EventDraws_EventId  ON dbo.EventDraws (event_id);
