BEGIN;

-- Demo credentials for active seeded accounts:
-- - lucia@asesoria.com / Secret123!
-- - miguel@ganaderia.com / Secret123!

INSERT INTO app_user (email, name, surname, username, password_hash, role, email_verified_at, is_active)
SELECT
    'lucia@asesoria.com',
    'Lucia',
    'Martin Ramos',
    'luciaasesoria',
    '$2a$11$sPD1nixYU4MdOnuCnA9jkeO559cjRV8ljYnea3briWLKgizGLYhqG',
    'Manager',
    now(),
    true
WHERE NOT EXISTS (
    SELECT 1 FROM app_user WHERE email = 'lucia@asesoria.com'
);

INSERT INTO manager (user_id, organization_name, professional_identifier, phone_number, province, town, invitation_code)
SELECT
    u.id,
    'Asesoria Ganadera Martin',
    'B12345678',
    '600123123',
    'Caceres',
    'Caceres',
    '52DVD7SG'
FROM app_user u
WHERE u.email = 'lucia@asesoria.com'
  AND NOT EXISTS (
      SELECT 1 FROM manager m WHERE m.user_id = u.id
  );

INSERT INTO subscription (user_id, autorenew, expiration_date, initial_date, plan_type, state)
SELECT
    u.id,
    true,
    CURRENT_DATE + INTERVAL '1 year',
    CURRENT_DATE - INTERVAL '3 months',
    'Professional',
    'Active'
FROM app_user u
WHERE u.email = 'lucia@asesoria.com'
  AND NOT EXISTS (
      SELECT 1 FROM subscription s WHERE s.user_id = u.id
  );

INSERT INTO app_user (email, name, surname, username, password_hash, role, email_verified_at, is_active)
SELECT
    'miguel@ganaderia.com',
    'Miguel',
    'Torres Vega',
    'migueltvega',
    '$2a$11$sPD1nixYU4MdOnuCnA9jkeO559cjRV8ljYnea3briWLKgizGLYhqG',
    'Farmer',
    now(),
    true
WHERE NOT EXISTS (
    SELECT 1 FROM app_user WHERE email = 'miguel@ganaderia.com'
);

INSERT INTO farmer (
    user_id,
    manager_id,
    nif_cif,
    phone_number,
    province,
    residence,
    town,
    zip_code,
    person_type,
    birth_date,
    status
)
SELECT
    farmer_user.id,
    manager_user.id,
    '12345678A',
    '612345678',
    'Caceres',
    'Camino de la Dehesa s/n',
    'Caceres',
    '10004',
    'Individual',
    DATE '1985-04-12',
    'Active'
FROM app_user farmer_user
CROSS JOIN app_user manager_user
WHERE farmer_user.email = 'miguel@ganaderia.com'
  AND manager_user.email = 'lucia@asesoria.com'
  AND NOT EXISTS (
      SELECT 1 FROM farmer f WHERE f.user_id = farmer_user.id
  );

INSERT INTO subscription (user_id, autorenew, expiration_date, initial_date, plan_type, state)
SELECT
    u.id,
    false,
    CURRENT_DATE + INTERVAL '6 months',
    CURRENT_DATE - INTERVAL '2 months',
    'Basic',
    'Active'
FROM app_user u
WHERE u.email = 'miguel@ganaderia.com'
  AND NOT EXISTS (
      SELECT 1 FROM subscription s WHERE s.user_id = u.id
  );

INSERT INTO app_user (email, name, surname, username, password_hash, role, email_verified_at, is_active)
SELECT
    'contacto.sierra.norte@example.com',
    'Laura',
    'Prieto',
    NULL,
    NULL,
    'Farmer',
    NULL,
    false
WHERE NOT EXISTS (
    SELECT 1 FROM app_user WHERE email = 'contacto.sierra.norte@example.com'
);

INSERT INTO farmer (
    user_id,
    manager_id,
    nif_cif,
    company_name,
    legal_representative,
    phone_number,
    province,
    residence,
    town,
    zip_code,
    person_type,
    status
)
SELECT
    farmer_user.id,
    manager_user.id,
    'B76543210',
    'Ganados Sierra Norte SL',
    'Laura Prieto',
    '611223344',
    'Caceres',
    'Calle Real 14',
    'Trujillo',
    '10200',
    'Company',
    'PendingActivation'
FROM app_user farmer_user
CROSS JOIN app_user manager_user
WHERE farmer_user.email = 'contacto.sierra.norte@example.com'
  AND manager_user.email = 'lucia@asesoria.com'
  AND NOT EXISTS (
      SELECT 1 FROM farmer f WHERE f.user_id = farmer_user.id
  );

INSERT INTO livestock_farm (
    farmer_id,
    authorised_capacity,
    address,
    status,
    livestock_species,
    name,
    province,
    rega_code,
    regime,
    responsible,
    town,
    zip_code,
    zootechnic_classification
)
SELECT
    f.user_id,
    250,
    'Camino de la Dehesa s/n',
    'active',
    'ovine',
    'Dehesa El Robledal',
    'Caceres',
    'ES061230000145',
    'extensive',
    'Miguel Torres Vega',
    'Caceres',
    '10004',
    'Producción y reproducción'
FROM farmer f
JOIN app_user u ON u.id = f.user_id
WHERE u.email = 'miguel@ganaderia.com'
  AND NOT EXISTS (
      SELECT 1 FROM livestock_farm lf WHERE lf.rega_code = 'ES061230000145'
  );

WITH farm AS (
    SELECT id, rega_code
    FROM livestock_farm
    WHERE rega_code = 'ES061230000145'
)
INSERT INTO animal (
    livestock_farm_id,
    birth_year,
    identification,
    registration_cause,
    registration_date,
    discharge_cause,
    discharge_date,
    sex
)
SELECT
    farm.id,
    EXTRACT(YEAR FROM dates.registration_date)::integer,
    farm.rega_code || '-A' || LPAD(dates.seq::text, 3, '0'),
    'Alta inicial',
    dates.registration_date,
    dates.discharge_cause,
    dates.discharge_date,
    dates.sex
FROM farm
JOIN (
    VALUES
        (1, CURRENT_DATE - INTERVAL '180 days', NULL::text, NULL::date, 'Female'),
        (2, CURRENT_DATE - INTERVAL '150 days', NULL::text, NULL::date, 'Female'),
        (3, CURRENT_DATE - INTERVAL '122 days', 'Venta', CURRENT_DATE - INTERVAL '20 days', 'Male'),
        (4, CURRENT_DATE - INTERVAL '95 days', NULL::text, NULL::date, 'Female'),
        (5, CURRENT_DATE - INTERVAL '70 days', NULL::text, NULL::date, 'Male'),
        (6, CURRENT_DATE - INTERVAL '43 days', NULL::text, NULL::date, 'Female'),
        (7, CURRENT_DATE - INTERVAL '18 days', NULL::text, NULL::date, 'Female'),
        (8, CURRENT_DATE - INTERVAL '7 days', NULL::text, NULL::date, 'Male')
) AS dates(seq, registration_date, discharge_cause, discharge_date, sex) ON TRUE
WHERE NOT EXISTS (
    SELECT 1
    FROM animal a
    WHERE a.identification = farm.rega_code || '-A' || LPAD(dates.seq::text, 3, '0')
);

INSERT INTO animal_birth (mother_animal_id, father_animal_id, birth_date, offspring_number)
SELECT
    mother.id,
    father.id,
    birth_data.birth_date,
    birth_data.offspring_number
FROM (
    VALUES
        (CURRENT_DATE - INTERVAL '88 days', 2),
        (CURRENT_DATE - INTERVAL '36 days', 1),
        (CURRENT_DATE - INTERVAL '12 days', 3)
) AS birth_data(birth_date, offspring_number)
CROSS JOIN LATERAL (
    SELECT id
    FROM animal
    WHERE identification = 'ES061230000145-A001'
) mother
CROSS JOIN LATERAL (
    SELECT id
    FROM animal
    WHERE identification = 'ES061230000145-A002'
) father
WHERE NOT EXISTS (
    SELECT 1
    FROM animal_birth ab
    WHERE ab.mother_animal_id = mother.id
      AND ab.birth_date = birth_data.birth_date
);

INSERT INTO vaccination (animal_id, next_dose, observations, vaccination_date, vaccination_type)
SELECT
    a.id,
    vaccine_data.next_dose,
    vaccine_data.observations,
    vaccine_data.vaccination_date,
    vaccine_data.vaccination_type
FROM (
    VALUES
        ('ES061230000145-A001', 'Lengua azul', CURRENT_DATE - INTERVAL '60 days', CURRENT_DATE + INTERVAL '3 days', 'Revacunación prevista'),
        ('ES061230000145-A002', 'Brucelosis', CURRENT_DATE - INTERVAL '33 days', CURRENT_DATE + INTERVAL '8 days', 'Seguimiento trimestral'),
        ('ES061230000145-A003', 'Clostridiosis', CURRENT_DATE - INTERVAL '24 days', CURRENT_DATE - INTERVAL '1 day', 'Pendiente de regularizar')
) AS vaccine_data(identification, vaccination_type, vaccination_date, next_dose, observations)
JOIN animal a ON a.identification = vaccine_data.identification
WHERE NOT EXISTS (
    SELECT 1
    FROM vaccination v
    WHERE v.animal_id = a.id
      AND v.vaccination_type = vaccine_data.vaccination_type
      AND v.vaccination_date = vaccine_data.vaccination_date
);

INSERT INTO movement_certificate (
    origin_livestock_id,
    cod_remo,
    departure_date,
    number_of_animals,
    solicitation_date,
    specie
)
SELECT
    farm.id,
    movement_data.cod_remo,
    movement_data.departure_date,
    movement_data.number_of_animals,
    movement_data.departure_date - INTERVAL '2 days',
    'ovine'
FROM livestock_farm farm
JOIN (
    VALUES
        ('REM-2025-1103', CURRENT_DATE - INTERVAL '150 days', 6),
        ('REM-2026-0207', CURRENT_DATE - INTERVAL '100 days', 4),
        ('REM-2026-0318', CURRENT_DATE - INTERVAL '38 days', 7),
        ('REM-2026-0415', CURRENT_DATE - INTERVAL '9 days', 3)
) AS movement_data(cod_remo, departure_date, number_of_animals) ON TRUE
WHERE farm.rega_code = 'ES061230000145'
  AND NOT EXISTS (
      SELECT 1
      FROM movement_certificate mc
      WHERE mc.cod_remo = movement_data.cod_remo
  );

INSERT INTO inspection (
    livestock_farm_id,
    inspection_date,
    observations,
    reason,
    tagged_animals,
    veterinary
)
SELECT
    farm.id,
    inspection_data.inspection_date,
    inspection_data.observations,
    inspection_data.reason,
    inspection_data.tagged_animals,
    inspection_data.veterinary
FROM livestock_farm farm
JOIN (
    VALUES
        (CURRENT_DATE + INTERVAL '6 days', 'Revisión de identificación y documentación', 'Inspección programada', 24, 'Dra. Elena Varela'),
        (CURRENT_DATE + INTERVAL '18 days', 'Seguimiento preventivo del lote', 'Revisión sanitaria', 18, 'Dr. Miguel Cordero')
) AS inspection_data(inspection_date, observations, reason, tagged_animals, veterinary) ON TRUE
WHERE farm.rega_code = 'ES061230000145'
  AND NOT EXISTS (
      SELECT 1
      FROM inspection i
      WHERE i.livestock_farm_id = farm.id
        AND i.inspection_date = inspection_data.inspection_date
        AND i.reason = inspection_data.reason
  );

COMMIT;
