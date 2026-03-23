-- Add price column to Registrations table
ALTER TABLE dbo.Registrations
ADD price DECIMAL(10, 2) NULL;
