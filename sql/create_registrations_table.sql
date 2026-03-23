-- ─────────────────────────────────────────────────────────────────────────────
-- Create Registrations table
-- Run once in Azure SQL (Piano Vertu employee portal database)
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE dbo.Registrations (
    id                  INT            IDENTITY(1,1)  PRIMARY KEY,
    ref_id              NVARCHAR(20)   NOT NULL UNIQUE,   -- PV-2026-0001

    language            NVARCHAR(2)    NOT NULL,           -- FR, EN, ZH

    -- Customer info
    customer_last_name  NVARCHAR(100)  NOT NULL,
    customer_first_name NVARCHAR(100)  NOT NULL,
    customer_email      NVARCHAR(200)  NOT NULL,
    customer_phone1     NVARCHAR(50),
    customer_phone2     NVARCHAR(50),
    heard_from          NVARCHAR(200),

    -- Delivery address
    delivery_street     NVARCHAR(200)  NOT NULL,
    delivery_apt        NVARCHAR(50),
    delivery_city       NVARCHAR(100),
    delivery_province   NVARCHAR(50),
    delivery_postal     NVARCHAR(20),
    within_40km         BIT            NOT NULL DEFAULT 0,
    delivery_floor      NVARCHAR(100),
    delivery_elevator   BIT            NOT NULL DEFAULT 0,
    steps_outside       INT                     DEFAULT 0,
    steps_inside        INT                     DEFAULT 0,
    stair_turns         INT                     DEFAULT 0,
    mover_notes         NVARCHAR(MAX),

    -- Collection / recycling
    collect_piano       BIT            NOT NULL DEFAULT 0,
    collect_desc        NVARCHAR(MAX),
    recycle_piano       BIT            NOT NULL DEFAULT 0,
    recycle_desc        NVARCHAR(MAX),
    crane_required      BIT            NOT NULL DEFAULT 0,

    -- Delivery scheduling
    delivery_asap       BIT            NOT NULL DEFAULT 0,
    delivery_date       NVARCHAR(20),
    delivery_notes      NVARCHAR(MAX),
    surcharge_flag      BIT            NOT NULL DEFAULT 0,

    -- Piano
    piano_category_id   INT,
    piano_type_id       INT,
    piano_make          NVARCHAR(200),
    piano_model         NVARCHAR(200),
    piano_serial        NVARCHAR(100),
    piano_color         NVARCHAR(100),
    purchase_date       DATE           NOT NULL,
    accessories         NVARCHAR(MAX),
    bench_model_id      INT,
    bench_notes         NVARCHAR(MAX),
    piano_notes         NVARCHAR(MAX),

    -- Compliance & documents
    humidity_confirmed  BIT            NOT NULL DEFAULT 0,
    warranty_pdf_blob   NVARCHAR(500),   -- blob name only, never a SAS URL
    tradeup_pdf_blob    NVARCHAR(500),   -- blob name only, never a SAS URL

    -- Signature
    signature_type      NVARCHAR(10)   NOT NULL,           -- 'drawn' or 'typed'
    signature_blob_name NVARCHAR(500),                     -- blob in 'signatures' container
    signed_at           DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),

    -- Staff fields (filled in portal after submission)
    invoice_number      NVARCHAR(100),
    from_location       NVARCHAR(200),
    old_piano_dest      NVARCHAR(200),
    surcharge_amount    DECIMAL(10,2)           DEFAULT 0,
    cheque_to_collect   BIT            NOT NULL DEFAULT 0,
    google_review       BIT            NOT NULL DEFAULT 0,
    fully_paid          BIT            NOT NULL DEFAULT 0,
    staff_notes         NVARCHAR(MAX),

    -- Meta
    created_at          DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Reg_Category FOREIGN KEY (piano_category_id) REFERENCES dbo.PianoCategory(id),
    CONSTRAINT FK_Reg_Type     FOREIGN KEY (piano_type_id)     REFERENCES dbo.PianoType(id),
    CONSTRAINT FK_Reg_Bench    FOREIGN KEY (bench_model_id)    REFERENCES dbo.bench(id)
);
