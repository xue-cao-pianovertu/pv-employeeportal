-- Add status column to Registrations table
-- Values: potential | completed | paid | partially_paid

ALTER TABLE dbo.Registrations
ADD status NVARCHAR(20) NOT NULL DEFAULT 'potential';
