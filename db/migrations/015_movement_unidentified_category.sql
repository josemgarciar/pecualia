ALTER TABLE movement_certificate
    ADD COLUMN IF NOT EXISTS unidentified_category VARCHAR(40);

ALTER TABLE movement_certificate
    DROP CONSTRAINT IF EXISTS movement_unidentified_category_chk;

ALTER TABLE movement_certificate
    ADD CONSTRAINT movement_unidentified_category_chk CHECK (
        unidentified_category IS NULL OR unidentified_category IN ('Under4Months', 'Between4And12Months')
    );
