BEGIN;

ALTER TABLE movement_certificate
    ALTER COLUMN departure_date TYPE TIMESTAMPTZ USING departure_date::timestamp AT TIME ZONE 'Europe/Madrid',
    ALTER COLUMN arrival_date TYPE TIMESTAMPTZ USING CASE
        WHEN arrival_date IS NULL THEN NULL
        ELSE arrival_date::timestamp AT TIME ZONE 'Europe/Madrid'
    END,
    ALTER COLUMN solicitation_date TYPE TIMESTAMPTZ USING CASE
        WHEN solicitation_date IS NULL THEN NULL
        ELSE solicitation_date::timestamp AT TIME ZONE 'Europe/Madrid'
    END;

COMMIT;
