-- Add has_tradeup flag to PianoCategory
-- Run once before deploying item 3
ALTER TABLE dbo.PianoCategory ADD has_tradeup BIT NOT NULL DEFAULT 0;

-- Enable tradeup gate for Acoustique neuf (id=1) and Occasion certifié (id=2) only
UPDATE dbo.PianoCategory SET has_tradeup = 1 WHERE id IN (1, 3);
