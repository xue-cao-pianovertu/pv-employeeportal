CREATE TABLE dbo.RegistrationSalesStaff (
    registration_id  INT NOT NULL REFERENCES dbo.Registrations(id),
    sales_staff_id   INT NOT NULL REFERENCES dbo.SalesStaff(id),
    PRIMARY KEY (registration_id, sales_staff_id)
);
