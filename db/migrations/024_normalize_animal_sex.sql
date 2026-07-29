UPDATE animal
SET sex = CASE
    WHEN LOWER(BTRIM(sex)) IN ('female', 'hembra', 'h', 'f') THEN 'Female'
    WHEN LOWER(BTRIM(sex)) IN ('male', 'macho', 'm') THEN 'Male'
    ELSE sex
END
WHERE sex IS NOT NULL
  AND LOWER(BTRIM(sex)) IN ('female', 'hembra', 'h', 'f', 'male', 'macho', 'm');
