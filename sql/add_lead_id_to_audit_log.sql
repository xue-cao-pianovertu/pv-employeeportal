-- Make registration_id nullable so AuditLog can hold lead entries too
ALTER TABLE dbo.AuditLog ALTER COLUMN registration_id INT NULL;

-- Add lead_id column for lead-related audit entries
ALTER TABLE dbo.AuditLog ADD lead_id INT NULL;
