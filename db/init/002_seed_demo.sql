BEGIN;

-- Demo credentials for active seeded accounts:
-- - rosa@asesoria.com / Secret123!
-- - miguel@ganaderia.com / Secret123!
-- - raul@ibericovalle.com / Secret123!

INSERT INTO app_user (email, name, surname, username, password_hash, role, email_verified_at, is_active)
SELECT
    'rosa@asesoria.com',
    'Rosa',
    'Rosa Murillo',
    'rosaasesoria',
    '$2a$11$sPD1nixYU4MdOnuCnA9jkeO559cjRV8ljYnea3briWLKgizGLYhqG',
    'Manager',
    now(),
    true
WHERE NOT EXISTS (
    SELECT 1 FROM app_user WHERE email = 'rosa@asesoria.com'
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
WHERE u.email = 'rosa@asesoria.com'
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
WHERE u.email = 'rosa@asesoria.com'
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
  AND manager_user.email = 'rosa@asesoria.com'
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

INSERT INTO app_user (email, name, surname, username, password_hash, role, email_verified_at, is_active)
SELECT
    'raul@ibericovalle.com',
    'Raul',
    'Sanchez Moreno',
    'rauliberico',
    '$2a$11$sPD1nixYU4MdOnuCnA9jkeO559cjRV8ljYnea3briWLKgizGLYhqG',
    'Farmer',
    now(),
    true
WHERE NOT EXISTS (
    SELECT 1 FROM app_user WHERE email = 'raul@ibericovalle.com'
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
  AND manager_user.email = 'rosa@asesoria.com'
  AND NOT EXISTS (
      SELECT 1 FROM farmer f WHERE f.user_id = farmer_user.id
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
    '22334455B',
    '622334455',
    'Badajoz',
    'Finca El Valle, km 3',
    'Jerez de los Caballeros',
    '06380',
    'Individual',
    DATE '1982-09-21',
    'Active'
FROM app_user farmer_user
CROSS JOIN app_user manager_user
WHERE farmer_user.email = 'raul@ibericovalle.com'
  AND manager_user.email = 'rosa@asesoria.com'
  AND NOT EXISTS (
      SELECT 1 FROM farmer f WHERE f.user_id = farmer_user.id
  );

INSERT INTO subscription (user_id, autorenew, expiration_date, initial_date, plan_type, state)
SELECT
    u.id,
    true,
    CURRENT_DATE + INTERVAL '9 months',
    CURRENT_DATE - INTERVAL '4 months',
    'Professional',
    'Active'
FROM app_user u
WHERE u.email = 'raul@ibericovalle.com'
  AND NOT EXISTS (
      SELECT 1 FROM subscription s WHERE s.user_id = u.id
  );

INSERT INTO livestock_farm (
    farmer_id,
    authorised_capacity,
    address,
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

INSERT INTO livestock_farm (
    farmer_id,
    authorised_capacity,
    address,
    livestock_species,
    name,
    porcine_registry_number,
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
    320,
    'Finca El Valle, km 3',
    'porcine',
    'Valle Iberico',
    '018BA0020',
    'Badajoz',
    'ES060180000046',
    'semiExtensive',
    'Raul Sanchez Moreno',
    'Jerez de los Caballeros',
    '06380',
    'Cebo'
FROM farmer f
JOIN app_user u ON u.id = f.user_id
WHERE u.email = 'raul@ibericovalle.com'
  AND NOT EXISTS (
      SELECT 1 FROM livestock_farm lf WHERE lf.rega_code = 'ES060180000046'
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
    EXTRACT(YEAR FROM animal_data.registration_date)::integer,
    animal_data.identification,
    'Entrada',
    animal_data.registration_date,
    animal_data.discharge_cause,
    animal_data.discharge_date,
    animal_data.sex
FROM farm
JOIN (
    VALUES
        ('ES100008594601', CURRENT_DATE - INTERVAL '180 days', NULL::text, NULL::date, 'Female'),
        ('ES100008594602', CURRENT_DATE - INTERVAL '150 days', NULL::text, NULL::date, 'Female'),
        ('ES100008594603', CURRENT_DATE - INTERVAL '122 days', 'Salida', CURRENT_DATE - INTERVAL '20 days', 'Male'),
        ('ES100008594604', CURRENT_DATE - INTERVAL '95 days', NULL::text, NULL::date, 'Female'),
        ('ES100008594605', CURRENT_DATE - INTERVAL '70 days', NULL::text, NULL::date, 'Male'),
        ('ES100008594606', CURRENT_DATE - INTERVAL '43 days', NULL::text, NULL::date, 'Female'),
        ('ES100008594607', CURRENT_DATE - INTERVAL '18 days', NULL::text, NULL::date, 'Female'),
        ('ES100008594608', CURRENT_DATE - INTERVAL '7 days', NULL::text, NULL::date, 'Male')
) AS animal_data(identification, registration_date, discharge_cause, discharge_date, sex) ON TRUE
WHERE NOT EXISTS (
    SELECT 1
    FROM animal a
    WHERE a.identification = animal_data.identification
);

INSERT INTO animal_birth (livestock_farm_id, mother_animal_id, father_animal_id, birth_date, offspring_number)
SELECT
    mother.livestock_farm_id,
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
    SELECT id, livestock_farm_id
    FROM animal
    WHERE identification = 'ES100008594601'
) mother
CROSS JOIN LATERAL (
    SELECT id
    FROM animal
    WHERE identification = 'ES100008594602'
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
        ('ES100008594601', 'Lengua azul', CURRENT_DATE - INTERVAL '60 days', CURRENT_DATE + INTERVAL '3 days', 'Revacunación prevista'),
        ('ES100008594602', 'Brucelosis', CURRENT_DATE - INTERVAL '33 days', CURRENT_DATE + INTERVAL '8 days', 'Seguimiento trimestral'),
        ('ES100008594603', 'Clostridiosis', CURRENT_DATE - INTERVAL '24 days', CURRENT_DATE - INTERVAL '1 day', 'Pendiente de regularizar')
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

INSERT INTO incident (
    livestock_farm_id,
    animal_id,
    change_reason,
    description,
    incident_date,
    last_identification,
    new_identification
)
SELECT
    farm.id,
    animal.id,
    incident_data.change_reason,
    incident_data.description,
    incident_data.incident_date,
    incident_data.last_identification,
    incident_data.new_identification
FROM livestock_farm farm
JOIN animal ON animal.livestock_farm_id = farm.id
JOIN (
    VALUES
        ('Reposición de crotal', 'Sustitución del crotal deteriorado tras revisión interna.', CURRENT_DATE - INTERVAL '65 days', 'ES100008594601', 'ES100008594601'),
        ('Regularización documental', 'Anotación de incidencia por diferencia detectada en documentación de acompañamiento.', CURRENT_DATE - INTERVAL '21 days', 'ES100008594605', 'ES100008594605')
) AS incident_data(change_reason, description, incident_date, last_identification, new_identification) ON animal.identification = incident_data.last_identification
WHERE farm.rega_code = 'ES061230000145'
  AND NOT EXISTS (
      SELECT 1
      FROM incident i
      WHERE i.livestock_farm_id = farm.id
        AND i.incident_date = incident_data.incident_date
        AND i.last_identification = incident_data.last_identification
  );

WITH farm AS (
    SELECT id
    FROM livestock_farm
    WHERE rega_code = 'ES060180000046'
)
INSERT INTO animal (
    livestock_farm_id,
    birth_year,
    breed,
    identification,
    origin_code,
    registration_cause,
    registration_date,
    sex
)
SELECT
    farm.id,
    animal_data.birth_year,
    animal_data.breed,
    animal_data.identification,
    animal_data.origin_code,
    animal_data.registration_cause,
    animal_data.registration_date,
    animal_data.sex
FROM farm
JOIN (
    VALUES
        (2025, 'Ibérico', 'GT1800001001', 'ES100200300400', 'Entrada', CURRENT_DATE - INTERVAL '140 days', 'Male'),
        (2025, 'Ibérico', 'GT1800001002', 'ES100200300400', 'Entrada', CURRENT_DATE - INTERVAL '135 days', 'Female'),
        (2026, 'Duroc', 'GT1800001003', 'ES060180000046', 'Autorreposicion', CURRENT_DATE - INTERVAL '42 days', 'Male'),
        (2026, 'Ibérico', 'GT1800001004', 'ES060180000046', 'Autorreposicion', CURRENT_DATE - INTERVAL '39 days', 'Female'),
        (2026, 'Landrace', 'GT1800001005', 'ES100200300400', 'Entrada', CURRENT_DATE - INTERVAL '18 days', 'Female')
) AS animal_data(birth_year, breed, identification, origin_code, registration_cause, registration_date, sex) ON TRUE
WHERE NOT EXISTS (
    SELECT 1
    FROM animal a
    WHERE a.identification = animal_data.identification
);

INSERT INTO porcino (
    animal_id,
    animal_type,
    identification_date,
    pig_registration_number,
    tag
)
SELECT
    animal.id,
    porcine_data.animal_type,
    porcine_data.identification_date,
    porcine_data.pig_registration_number,
    porcine_data.tag
FROM animal
JOIN (
    VALUES
        ('GT1800001001', 'Verraco', CURRENT_DATE - INTERVAL '138 days', '018BA0020-001', 'Lote-V1'),
        ('GT1800001002', 'Cerda de vida', CURRENT_DATE - INTERVAL '133 days', '018BA0020-002', 'Lote-CV1'),
        ('GT1800001003', 'Reposicion', CURRENT_DATE - INTERVAL '40 days', '018BA0020-003', 'Lote-R1'),
        ('GT1800001004', 'Cerda de reposicion', CURRENT_DATE - INTERVAL '37 days', '018BA0020-004', 'Lote-CR1'),
        ('GT1800001005', 'Lechon', CURRENT_DATE - INTERVAL '16 days', '018BA0020-005', 'Lote-L1')
 ) AS porcine_data(identification, animal_type, identification_date, pig_registration_number, tag)
    ON animal.identification = porcine_data.identification
WHERE NOT EXISTS (
    SELECT 1
    FROM porcino p
    WHERE p.animal_id = animal.id
);

INSERT INTO movement_certificate (
    destination_livestock_id,
    arrival_date,
    cod_remo,
    departure_date,
    means_of_transport,
    number_of_animals,
    origin_external_code,
    origin_external_name,
    solicitation_date,
    specie,
    transport_name,
    vehicle_registration_number
)
SELECT
    farm.id,
    movement_data.arrival_date,
    movement_data.cod_remo,
    movement_data.departure_date,
    movement_data.means_of_transport,
    movement_data.number_of_animals,
    movement_data.origin_external_code,
    movement_data.origin_external_name,
    movement_data.departure_date - INTERVAL '2 days',
    'porcine',
    movement_data.transport_name,
    movement_data.vehicle_registration_number
FROM livestock_farm farm
JOIN (
    VALUES
        ('REM-POR-2026-0102', CURRENT_DATE - INTERVAL '128 days', CURRENT_DATE - INTERVAL '127 days', 'Camion', 42, 'ES100200300400', 'Centro de seleccion La Jara', 'Transportes Sierra Sur', '4567MNL'),
        ('REM-POR-2026-0310', CURRENT_DATE - INTERVAL '54 days', CURRENT_DATE - INTERVAL '53 days', 'Camion', 24, 'ES100200300401', 'Nucleo genetico Los Pedroches', 'Ganatrans Extremadura', '8123KRT')
) AS movement_data(cod_remo, departure_date, arrival_date, means_of_transport, number_of_animals, origin_external_code, origin_external_name, transport_name, vehicle_registration_number) ON TRUE
WHERE farm.rega_code = 'ES060180000046'
  AND NOT EXISTS (
      SELECT 1
      FROM movement_certificate mc
      WHERE mc.cod_remo = movement_data.cod_remo
  );

INSERT INTO movement_certificate (
    origin_livestock_id,
    cod_remo,
    departure_date,
    destination_external_code,
    destination_external_name,
    means_of_transport,
    number_of_animals,
    solicitation_date,
    specie,
    transport_name,
    vehicle_registration_number
)
SELECT
    farm.id,
    movement_data.cod_remo,
    movement_data.departure_date,
    movement_data.destination_external_code,
    movement_data.destination_external_name,
    movement_data.means_of_transport,
    movement_data.number_of_animals,
    movement_data.departure_date - INTERVAL '1 day',
    'porcine',
    movement_data.transport_name,
    movement_data.vehicle_registration_number
FROM livestock_farm farm
JOIN (
    VALUES
        ('REM-POR-2026-0418', CURRENT_DATE - INTERVAL '14 days', 'ES100200300900', 'Matadero Comarcal Sur', 'Camion', 18, 'Ibertrans Dehesa', '9988JLM')
) AS movement_data(cod_remo, departure_date, destination_external_code, destination_external_name, means_of_transport, number_of_animals, transport_name, vehicle_registration_number) ON TRUE
WHERE farm.rega_code = 'ES060180000046'
  AND NOT EXISTS (
      SELECT 1
      FROM movement_certificate mc
      WHERE mc.cod_remo = movement_data.cod_remo
  );

INSERT INTO balance (
    livestock_farm_id,
    balance_date,
    destination_livestock_code,
    health_document_number,
    modification_cause,
    number_of_animals,
    origin_livestock_code
)
SELECT
    farm.id,
    balance_data.balance_date,
    balance_data.destination_livestock_code,
    balance_data.health_document_number,
    balance_data.modification_cause,
    balance_data.number_of_animals,
    balance_data.origin_livestock_code
FROM livestock_farm farm
JOIN (
    VALUES
        (CURRENT_DATE - INTERVAL '127 days', NULL::text, 'GSP-260102', 'Entrada', 42, 'ES100200300400'),
        (CURRENT_DATE - INTERVAL '53 days', NULL::text, 'GSP-260310', 'Nacimiento', 24, NULL::text),
        (CURRENT_DATE - INTERVAL '14 days', 'ES100200300900', 'GSP-260418', 'Salida', 18, NULL::text),
        (CURRENT_DATE - INTERVAL '6 days', NULL::text, 'GSP-260426', 'Muerte', 2, NULL::text)
) AS balance_data(balance_date, destination_livestock_code, health_document_number, modification_cause, number_of_animals, origin_livestock_code) ON TRUE
WHERE farm.rega_code = 'ES060180000046'
  AND NOT EXISTS (
      SELECT 1
      FROM balance b
      WHERE b.livestock_farm_id = farm.id
        AND b.balance_date = balance_data.balance_date
        AND b.modification_cause = balance_data.modification_cause
  );

INSERT INTO balance_porcino (
    balance_id,
    baits,
    boars,
    breed,
    piglets,
    pigs_reposition,
    rear,
    sows_for_live,
    sows_reposition,
    tag,
    type
)
SELECT
    balance.id,
    balance_data.baits,
    balance_data.boars,
    balance_data.breed,
    balance_data.piglets,
    balance_data.pigs_reposition,
    balance_data.rear,
    balance_data.sows_for_live,
    balance_data.sows_reposition,
    balance_data.tag,
    balance_data.type
FROM balance
JOIN livestock_farm farm ON farm.id = balance.livestock_farm_id
JOIN (
    VALUES
        (CURRENT_DATE - INTERVAL '127 days', 'Entrada', 80, 4, 'Ibérico', 0, 18, 52, 26, 12, 'Lote entrada enero', 'Cebo'),
        (CURRENT_DATE - INTERVAL '53 days', 'Nacimiento', 80, 4, 'Ibérico', 24, 18, 52, 26, 12, 'Paridera 2', 'Lechon'),
        (CURRENT_DATE - INTERVAL '14 days', 'Salida', 62, 4, 'Ibérico', 18, 16, 40, 24, 10, 'Lote salida abril', 'Cebo'),
        (CURRENT_DATE - INTERVAL '6 days', 'Muerte', 60, 4, 'Ibérico', 18, 16, 39, 24, 10, 'Corral 4', 'Cebo')
) AS balance_data(balance_date, modification_cause, baits, boars, breed, piglets, pigs_reposition, rear, sows_for_live, sows_reposition, tag, type)
    ON balance.balance_date = balance_data.balance_date
   AND balance.modification_cause = balance_data.modification_cause
WHERE farm.rega_code = 'ES060180000046'
  AND NOT EXISTS (
      SELECT 1
      FROM balance_porcino bp
      WHERE bp.balance_id = balance.id
  );

INSERT INTO census (
    livestock_farm_id,
    census_date
)
SELECT
    farm.id,
    census_data.census_date
FROM livestock_farm farm
JOIN (
    VALUES
        (CURRENT_DATE - INTERVAL '60 days'),
        (CURRENT_DATE - INTERVAL '5 days')
) AS census_data(census_date) ON TRUE
WHERE farm.rega_code = 'ES060180000046'
  AND NOT EXISTS (
      SELECT 1
      FROM census c
      WHERE c.livestock_farm_id = farm.id
        AND c.census_date = census_data.census_date
  );

INSERT INTO census_porcino (
    census_id,
    baits,
    boars,
    piglets,
    pigs_reposition,
    rears,
    sow,
    sows_reposition
)
SELECT
    census.id,
    census_data.baits,
    census_data.boars,
    census_data.piglets,
    census_data.pigs_reposition,
    census_data.rears,
    census_data.sow,
    census_data.sows_reposition
FROM census
JOIN livestock_farm farm ON farm.id = census.livestock_farm_id
JOIN (
    VALUES
        (CURRENT_DATE - INTERVAL '60 days', 68, 4, 36, 18, 44, 22, 11),
        (CURRENT_DATE - INTERVAL '5 days', 60, 4, 18, 16, 39, 24, 10)
 ) AS census_data(census_date, baits, boars, piglets, pigs_reposition, rears, sow, sows_reposition)
    ON census.census_date = census_data.census_date
WHERE farm.rega_code = 'ES060180000046'
  AND NOT EXISTS (
      SELECT 1
      FROM census_porcino cp
      WHERE cp.census_id = census.id
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
        (CURRENT_DATE + INTERVAL '4 days', 'Comprobacion de lotes de cebo y contrastacion de partes de movimiento.', 'Inspeccion programada', 73, 'Dra. Marta Pizarro'),
        (CURRENT_DATE + INTERVAL '16 days', 'Revision de reposicion y validacion del registro porcino.', 'Revision documental', 57, 'Dr. Alberto Naranjo')
) AS inspection_data(inspection_date, observations, reason, tagged_animals, veterinary) ON TRUE
WHERE farm.rega_code = 'ES060180000046'
  AND NOT EXISTS (
      SELECT 1
      FROM inspection i
      WHERE i.livestock_farm_id = farm.id
        AND i.inspection_date = inspection_data.inspection_date
        AND i.reason = inspection_data.reason
  );

INSERT INTO incident (
    livestock_farm_id,
    animal_id,
    change_reason,
    description,
    incident_date,
    last_identification,
    new_identification
)
SELECT
    farm.id,
    animal.id,
    incident_data.change_reason,
    incident_data.description,
    incident_data.incident_date,
    incident_data.last_identification,
    incident_data.new_identification
FROM livestock_farm farm
JOIN animal ON animal.livestock_farm_id = farm.id
JOIN (
    VALUES
        ('Reposicion de marca', 'Sustitucion del crotal visual deteriorado en el lote de reposicion.', CURRENT_DATE - INTERVAL '11 days', 'GT1800001004', 'GT1800001004')
) AS incident_data(change_reason, description, incident_date, last_identification, new_identification) ON animal.identification = incident_data.last_identification
WHERE farm.rega_code = 'ES060180000046'
  AND NOT EXISTS (
      SELECT 1
      FROM incident i
      WHERE i.livestock_farm_id = farm.id
        AND i.incident_date = incident_data.incident_date
        AND i.last_identification = incident_data.last_identification
  );

COMMIT;
