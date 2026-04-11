-- Migration: add payment_status and delivery_status to Registrations
-- Run manually before deploying

CREATE TABLE dbo.PaymentStatus (
  id       NVARCHAR(20)  NOT NULL PRIMARY KEY,
  label_fr NVARCHAR(50)  NOT NULL,
  label_en NVARCHAR(50)  NOT NULL
);
INSERT INTO dbo.PaymentStatus VALUES
  ('not_paid',        'Non payé',    'Not paid'),
  ('partially_paid',  'Part. payé',  'Partially paid'),
  ('fully_paid',      'Payé',        'Fully paid'),
  ('store_financing', 'Financement', 'Store financing');

CREATE TABLE dbo.DeliveryStatus (
  id       NVARCHAR(20)  NOT NULL PRIMARY KEY,
  label_fr NVARCHAR(50)  NOT NULL,
  label_en NVARCHAR(50)  NOT NULL
);
INSERT INTO dbo.DeliveryStatus VALUES
  ('to_plan',       N'À planifier',    'To plan'),
  ('sent_to_mover', N'Chez déménageur','Sent to mover'),
  ('delivered',     N'Livré',          'Delivered');

ALTER TABLE dbo.Registrations
  ADD payment_status  NVARCHAR(20) NOT NULL DEFAULT 'not_paid'
        REFERENCES dbo.PaymentStatus(id),
      delivery_status NVARCHAR(20) NOT NULL DEFAULT 'to_plan'
        REFERENCES dbo.DeliveryStatus(id);
