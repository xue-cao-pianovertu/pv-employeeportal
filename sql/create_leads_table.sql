-- Lead status lookup table
CREATE TABLE dbo.LeadStatus (
    id       NVARCHAR(20) NOT NULL PRIMARY KEY,
    label_fr NVARCHAR(60) NOT NULL,
    label_en NVARCHAR(60) NOT NULL
);

INSERT INTO dbo.LeadStatus (id, label_fr, label_en) VALUES
    ('new',            N'Nouveau',        'New'),
    ('contacted',      N'Contacté',       'Contacted'),
    ('interested',     N'Intéressé',      'Interested'),
    ('to_follow_up',   N'À relancer',     'To Follow Up'),
    ('not_interested', N'Pas intéressé',  'Not Interested'),
    ('converted',      N'Converti',       'Converted');

-- Potential customer leads table
CREATE TABLE dbo.Leads (
    id             INT           IDENTITY(1,1) PRIMARY KEY,
    language       NVARCHAR(2)   NOT NULL,
    first_name     NVARCHAR(100) NOT NULL,
    last_name      NVARCHAR(100) NOT NULL,
    email          NVARCHAR(200) NOT NULL,
    phone          NVARCHAR(50)  NULL,
    contact_type   NVARCHAR(10)  NOT NULL                -- visit | call | email | web | other
        CHECK (contact_type IN ('visit', 'call', 'email', 'web', 'other')),
    contact_date   DATE          NOT NULL,
    piano_interest NVARCHAR(MAX) NULL,
    customer_notes NVARCHAR(MAX) NULL,
    lead_status    NVARCHAR(20)  NOT NULL DEFAULT 'new'
        REFERENCES dbo.LeadStatus(id),
    staff_notes    NVARCHAR(MAX) NULL,
    created_at     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
