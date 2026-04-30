-- Add has_humidity_notice flag to PianoCategory
-- Controls whether the "Avis d'humidité" section (step 4) is shown on the registration form
-- Default 1 (show). Set to 0 for Numérique / Hybride (digital pianos — humidity does not apply).

ALTER TABLE dbo.PianoCategory
    ADD has_humidity_notice BIT NOT NULL DEFAULT 1;

-- Numérique / Hybride (id = 2) — hide humidity notice
UPDATE dbo.PianoCategory SET has_humidity_notice = 0 WHERE id = 2;
