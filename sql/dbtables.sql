-- 1. table creation
CREATE TABLE PianoCategory (
    id INT IDENTITY(1,1) PRIMARY KEY,
    name_fr NVARCHAR(100) NOT NULL,
    name_en NVARCHAR(100) NOT NULL,
    name_zh NVARCHAR(100) NOT NULL,
    allows_manual_entry BIT NOT NULL DEFAULT 0,
    has_warranty BIT NOT NULL DEFAULT 1
);

CREATE TABLE PianoType (
    id INT IDENTITY(1,1) PRIMARY KEY,
    category_id INT NOT NULL REFERENCES PianoCategory(id),
    name_fr NVARCHAR(150) NOT NULL,
    name_en NVARCHAR(150) NOT NULL,
    name_zh NVARCHAR(150) NOT NULL,
    brand_name NVARCHAR(100) NOT NULL,
    is_active BIT NOT NULL DEFAULT 1
);

CREATE TABLE WarrantyPdf (
    id INT IDENTITY(1,1) PRIMARY KEY,
    type_id INT NULL REFERENCES PianoType(id),
    category_id INT NULL REFERENCES PianoCategory(id),
    language CHAR(2) NOT NULL CHECK (language IN ('fr', 'en', 'zh')),
    blob_name NVARCHAR(255) NOT NULL,
    is_active BIT NOT NULL DEFAULT 1
);



-- 2. SEED PianoCategory
INSERT INTO PianoCategory (name_fr, name_en, name_zh, allows_manual_entry, has_warranty) VALUES
('Acoustique neuf',     'New Acoustic',     '全新原声钢琴', 0, 1),
('Numérique / Hybride', 'Digital / Hybrid', '数码/混合钢琴', 0, 1),
('Piano d''occasion',   'Used Piano',       '二手钢琴',     1, 1),
('Consignation',        'Consignment',      '寄售钢琴',     1, 0);



select * from PianoCategory

UPDATE PianoCategory SET name_zh = N'全新机械钢琴' WHERE id = 1;
UPDATE PianoCategory SET name_zh = N'数码/混合钢琴' WHERE id = 2;
UPDATE PianoCategory SET name_zh = N'二手钢琴'     WHERE id = 3;
UPDATE PianoCategory SET name_zh = N'寄售钢琴'     WHERE id = 4;

UPDATE PianoCategory SET 
    name_fr = N'Piano d''occasion certifié',
    name_en = N'Certified Pre-Loved Piano',
    name_zh = N'认证二手钢琴'
WHERE id = 3;


-- Step3— SEED PianoType (Acoustique neuf = category_id 1)
INSERT INTO PianoType (category_id, name_fr, name_en, name_zh, brand_name) VALUES
(1, N'Kawai Grand',             N'Kawai Grand',             N'卡瓦依三角钢琴',         N'Kawai'),
(1, N'Kawai Upright',           N'Kawai Upright',           N'卡瓦依立式钢琴',         N'Kawai'),
(1, N'Kawai Upright ATX/Aures', N'Kawai Upright ATX/Aures', N'卡瓦依立式ATX/Aures',   N'Kawai'),
(1, N'Kawai Grand ATX/Aures',   N'Kawai Grand ATX/Aures',   N'卡瓦依三角ATX/Aures',   N'Kawai'),
(1, N'Shigeru Kawai',           N'Shigeru Kawai',           N'Shigeru卡瓦依手工钢琴',  N'Shigeru Kawai'),
(1, N'C. Bechstein',            N'C. Bechstein',            N'贝希斯坦钢琴',           N'C. Bechstein'),
(1, N'Bösendorfer',             N'Bösendorfer',             N'博森多费尔钢琴',         N'Bösendorfer'),
(1, N'Fazioli',                 N'Fazioli',                 N'法奇欧里钢琴',           N'Fazioli'),
(1, N'Blüthner',                N'Blüthner',                N'布吕特纳钢琴',           N'Blüthner'),
(1, N'Schimmel',                N'Schimmel',                N'希梅尔钢琴',             N'Schimmel'),
(1, N'Mason & Hamlin',          N'Mason & Hamlin',          N'梅森汉姆林钢琴',         N'Mason & Hamlin'),
(1, N'Estonia',                 N'Estonia',                 N'爱沙尼亚钢琴',           N'Estonia');



select * from PianoType

---- Step 4 — SEED PianoType (Numérique / Hybride = category_id 2)
INSERT INTO PianoType (category_id, name_fr, name_en, name_zh, brand_name) VALUES
(2, N'Kawai CX / KDP / ES',     N'Kawai CX / KDP / ES',     N'卡瓦依CX/KDP/ES',     N'Kawai'),
(2, N'Kawai CN / VA / DG / NV', N'Kawai CN / VA / DG / NV', N'卡瓦依CN/VA/DG/NV',   N'Kawai'),
(2, N'Casio',                   N'Casio',                   N'卡西欧',               N'Casio');



select * from WarrantyPdf


CREATE TABLE TradeUpPdf (
    id INT IDENTITY(1,1) PRIMARY KEY,
    language CHAR(2) NOT NULL CHECK (language IN ('fr', 'en', 'zh')),
    blob_name NVARCHAR(255) NOT NULL,
    is_active BIT NOT NULL DEFAULT 1,
    UNIQUE (language)
);


select * from PianoType

-- Kawai Grand (type_id 1) - acoustic covers both grand and upright
INSERT INTO WarrantyPdf (type_id, category_id, language, blob_name) VALUES
(1, NULL, 'fr', N'Kawai_Acoustic_PVCopy_French.pdf'),
(1, NULL, 'en', N'Kawai_Acoustic_PVCopy_English.pdf'),
(1, NULL, 'zh', N'Warranty_TO_ADD_Later.pdf');


-- Piano d'occasion shared PDF (category_id 3)
INSERT INTO WarrantyPdf (type_id, category_id, language, blob_name) VALUES
(NULL, 3, 'fr', N'pv_used_piano_warranty_fr.pdf'),
(NULL, 3, 'en', N'pv_used_piano_warranty_en.pdf'),
(NULL, 3, 'zh', N'pv_used_piano_warranty_cn.pdf');


select * from WarrantyPdf

select * from TradeUpPdf

INSERT INTO TradeUpPdf (language, blob_name) VALUES
('fr', N'pv_trade_exchange_buyback_fr.pdf'),
('en', N'pv_trade_exchange_buyback_en.pdf'),
('zh', N'pv_trade_exchange_buyback_cn.pdf');


UPDATE PianoCategory SET name_zh = N'全新原声钢琴' WHERE id = 1;



select * from bench



-------------------------------------------------
-- Kawai Upright (type_id 2) - same PDF as Grand
INSERT INTO WarrantyPdf (type_id, category_id, language, blob_name) VALUES
(2, NULL, 'fr', N'Kawai_Acoustic_PVCopy_French.pdf'),
(2, NULL, 'en', N'Kawai_Acoustic_PVCopy_French.pdf'),
(2, NULL, 'zh', N'Warranty_TO_ADD_Later.pdf');

-- Kawai Upright ATX/Aures (type_id 3) - 
INSERT INTO WarrantyPdf (type_id, category_id, language, blob_name) VALUES
(3, NULL, 'fr', N'Warranty_TO_ADD_Later.pdf'),
(3, NULL, 'en', N'Kawai_ATX_Aures_PVCopy_English.pdf'),
(3, NULL, 'zh', N'Warranty_TO_ADD_Later.pdf');

-- Kawai Grand ATX/Aures (type_id 4) - same as Upright ATX
INSERT INTO WarrantyPdf (type_id, category_id, language, blob_name) VALUES
(4, NULL, 'fr', N'Warranty_TO_ADD_Later.pdf'),
(4, NULL, 'en', N'Kawai_ATX_Aures_PVCopy_English.pdf'),
(4, NULL, 'zh', N'Warranty_TO_ADD_Later.pdf');

-- Shigeru Kawai (type_id 5)
INSERT INTO WarrantyPdf (type_id, category_id, language, blob_name) VALUES
(5, NULL, 'fr', N'Kawai_Acoustic_PVCopy_French.pdf'),
(5, NULL, 'en', N'Kawai_Acoustic_PVCopy_French.pdf'),
(5, NULL, 'zh', N'Warranty_TO_ADD_Later.pdf');


-- C. Bechstein (type_id 6)
INSERT INTO WarrantyPdf (type_id, category_id, language, blob_name) VALUES
(6, NULL, 'fr', N'Bechstein_warranty_PCCopy_fr.pdf'),
(6, NULL, 'en', N'Bechstein_warranty_PVCopy_en.pdf'),
(6, NULL, 'zh', N'Bechstein_warranty_PVTranslate_cn.pdf');


-- Bösendorfer (type_id 7)
INSERT INTO WarrantyPdf (type_id, category_id, language, blob_name) VALUES
(7, NULL, 'fr', N'Warranty_TO_ADD_Later.pdf'),
(7, NULL, 'en', N'Warranty_TO_ADD_Later.pdf'),
(7, NULL, 'zh', N'Warranty_TO_ADD_Later.pdf');

-- Fazioli (type_id 8)
INSERT INTO WarrantyPdf (type_id, category_id, language, blob_name) VALUES
(8, NULL, 'fr', N'Warranty_TO_ADD_Later.pdf'),
(8, NULL, 'en', N'Warranty_TO_ADD_Later.pdf'),
(8, NULL, 'zh', N'Warranty_TO_ADD_Later.pdf');

-- Blüthner (type_id 9)
INSERT INTO WarrantyPdf (type_id, category_id, language, blob_name) VALUES
(9, NULL, 'fr', N'Warranty_TO_ADD_Later.pdf'),
(9, NULL, 'en', N'Warranty_TO_ADD_Later.pdf'),
(9, NULL, 'zh', N'Warranty_TO_ADD_Later.pdf');

-- Schimmel (type_id 10)
INSERT INTO WarrantyPdf (type_id, category_id, language, blob_name) VALUES
(10, NULL, 'fr', N'Warranty_TO_ADD_Later.pdf'),
(10, NULL, 'en', N'Warranty_TO_ADD_Later.pdf'),
(10, NULL, 'zh', N'Warranty_TO_ADD_Later.pdf');

-- Mason & Hamlin (type_id 11)
INSERT INTO WarrantyPdf (type_id, category_id, language, blob_name) VALUES
(11, NULL, 'fr', N'Warranty_TO_ADD_Later.pdf'),
(11, NULL, 'en', N'Warranty_TO_ADD_Later.pdf'),
(11, NULL, 'zh', N'Warranty_TO_ADD_Later.pdf');

-- Estonia (type_id 12)
INSERT INTO WarrantyPdf (type_id, category_id, language, blob_name) VALUES
(12, NULL, 'fr', N'Warranty_TO_ADD_Later.pdf'),
(12, NULL, 'en', N'Warranty_TO_ADD_Later.pdf'),
(12, NULL, 'zh', N'Warranty_TO_ADD_Later.pdf');

-- Kawai CX/KDP/ES (type_id 13)
INSERT INTO WarrantyPdf (type_id, category_id, language, blob_name) VALUES
(13, NULL, 'fr', N'Kawai_CX_KDP_ES_PVCopy_FR.pdf'),
(13, NULL, 'en', N'Kawai_CX_KDP_ES_PVCopy_EN.pdf'),
(13, NULL, 'zh', N'Warranty_TO_ADD_Later.pdf');


-- Kawai CN/VA/DG/NV (type_id 14)
INSERT INTO WarrantyPdf (type_id, category_id, language, blob_name) VALUES
(14, NULL, 'fr', N'Kawai_CN_CA_DG_NV_PVCopy_FR.pdf'),
(14, NULL, 'en', N'Kawai_CN_CA_DG_NV_PVCopy_EN.pdf'),
(14, NULL, 'zh', N'Warranty_TO_ADD_Later.pdf');


-- Casio (type_id 15)
INSERT INTO WarrantyPdf (type_id, category_id, language, blob_name) VALUES
(15, NULL, 'fr', N'Warranty_TO_ADD_Later.pdf'),
(15, NULL, 'en', N'Warranty_TO_ADD_Later.pdf'),
(15, NULL, 'zh', N'Warranty_TO_ADD_Later.pdf');



select * from WarrantyPdf