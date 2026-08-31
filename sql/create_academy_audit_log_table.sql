CREATE TABLE AcademyAuditLog (
  id              INT IDENTITY(1,1) PRIMARY KEY,
  registration_id INT           NOT NULL,
  changed_by      NVARCHAR(200) NOT NULL,
  changed_at      DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
  section         NVARCHAR(50)  NOT NULL,
  changes_json    NVARCHAR(MAX) NOT NULL,
  FOREIGN KEY (registration_id) REFERENCES AcademyRegistrations(id)
);
