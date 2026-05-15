BEGIN;

CREATE TABLE app_user (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    email VARCHAR(255) UNIQUE,
    name VARCHAR(120) NOT NULL,
    surname VARCHAR(180) NOT NULL,
    username VARCHAR(120) UNIQUE,
    password_hash TEXT,
    role VARCHAR(20) NOT NULL,
    email_verified_at TIMESTAMPTZ,
    is_active BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE manager (
    user_id BIGINT PRIMARY KEY REFERENCES app_user(id) ON DELETE CASCADE,
    organization_name VARCHAR(180) NOT NULL,
    professional_identifier VARCHAR(32) NOT NULL,
    phone_number VARCHAR(32),
    province VARCHAR(120),
    town VARCHAR(120),
    invitation_code VARCHAR(32) NOT NULL UNIQUE
);

CREATE TABLE farmer (
    user_id BIGINT PRIMARY KEY REFERENCES app_user(id) ON DELETE CASCADE,
    manager_id BIGINT REFERENCES manager(user_id) ON DELETE SET NULL,
    nif_cif VARCHAR(32) NOT NULL UNIQUE,
    second_surname VARCHAR(180),
    company_name VARCHAR(180),
    legal_representative VARCHAR(180),
    phone_number VARCHAR(32),
    province VARCHAR(120),
    residence VARCHAR(255),
    town VARCHAR(120),
    zip_code VARCHAR(16),
    person_type VARCHAR(20) NOT NULL DEFAULT 'Individual',
    birth_date DATE,
    status VARCHAR(32) NOT NULL DEFAULT 'PendingActivation'
);

CREATE TABLE subscription (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id BIGINT NOT NULL UNIQUE REFERENCES app_user(id) ON DELETE CASCADE,
    autorenew BOOLEAN NOT NULL DEFAULT false,
    expiration_date DATE NOT NULL,
    initial_date DATE NOT NULL,
    plan_type VARCHAR(60) NOT NULL,
    state VARCHAR(40) NOT NULL,
    stripe_customer_id VARCHAR(64),
    stripe_subscription_id VARCHAR(64),
    stripe_price_id VARCHAR(64),
    CONSTRAINT subscription_dates_chk CHECK (expiration_date >= initial_date)
);

CREATE TABLE account_activation_token (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES app_user(id) ON DELETE CASCADE,
    token_hash VARCHAR(128) NOT NULL UNIQUE,
    expires_at TIMESTAMPTZ NOT NULL,
    used_at TIMESTAMPTZ,
    created_by_user_id BIGINT REFERENCES app_user(id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE livestock_farm (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    farmer_id BIGINT NOT NULL REFERENCES farmer(user_id) ON DELETE CASCADE,
    authorised_capacity INTEGER,
    address VARCHAR(255),
    livestock_species VARCHAR(40) NOT NULL,
    livestock_type VARCHAR(80),
    name VARCHAR(160) NOT NULL,
    porcine_fattening_capacity INTEGER,
    porcine_mothers_capacity INTEGER,
    porcine_registry_number VARCHAR(32),
    province VARCHAR(120),
    rega_code VARCHAR(32) NOT NULL UNIQUE,
    regime VARCHAR(80),
    responsible VARCHAR(180),
    spindle INTEGER,
    town VARCHAR(120),
    x_coordinate DOUBLE PRECISION,
    y_coordinate DOUBLE PRECISION,
    zip_code VARCHAR(16),
    zootechnic_classification VARCHAR(120),
    CONSTRAINT livestock_species_chk CHECK (livestock_species IN ('ovine', 'caprine', 'porcine')),
    CONSTRAINT authorised_capacity_non_negative_chk CHECK (authorised_capacity IS NULL OR authorised_capacity >= 0),
    CONSTRAINT porcine_fattening_capacity_non_negative_chk CHECK (porcine_fattening_capacity IS NULL OR porcine_fattening_capacity >= 0),
    CONSTRAINT porcine_mothers_capacity_non_negative_chk CHECK (porcine_mothers_capacity IS NULL OR porcine_mothers_capacity >= 0)
);

CREATE TABLE animal (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    livestock_farm_id BIGINT NOT NULL REFERENCES livestock_farm(id) ON DELETE CASCADE,
    birth_date DATE,
    birth_year INTEGER,
    breed VARCHAR(80),
    destination_code VARCHAR(32),
    discharge_cause VARCHAR(80),
    discharge_date DATE,
    identification VARCHAR(80) NOT NULL UNIQUE,
    origin_code VARCHAR(32),
    registration_cause VARCHAR(80),
    registration_date DATE,
    sex VARCHAR(20),
    source_birth_id BIGINT,
    CONSTRAINT animal_birth_year_chk CHECK (birth_year IS NULL OR birth_year BETWEEN 1900 AND 2100),
    CONSTRAINT animal_registration_cause_chk CHECK (registration_cause IS NULL OR registration_cause IN ('Entrada', 'Autorreposicion')),
    CONSTRAINT animal_discharge_cause_chk CHECK (discharge_cause IS NULL OR discharge_cause IN ('Salida', 'Muerte'))
);

CREATE TABLE ovino_caprino (
    animal_id BIGINT PRIMARY KEY REFERENCES animal(id) ON DELETE CASCADE,
    dominant_allele VARCHAR(80),
    genotyping VARCHAR(120),
    low_allele VARCHAR(80),
    species_type VARCHAR(40) NOT NULL,
    CONSTRAINT ovino_caprino_species_type_chk CHECK (species_type IN ('ovine', 'caprine'))
);

CREATE TABLE porcino (
    animal_id BIGINT PRIMARY KEY REFERENCES animal(id) ON DELETE CASCADE,
    animal_type VARCHAR(80) NOT NULL,
    identification_date DATE,
    pig_registration_number VARCHAR(80),
    tag VARCHAR(80)
);

CREATE TABLE animal_birth (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    livestock_farm_id BIGINT NOT NULL REFERENCES livestock_farm(id) ON DELETE CASCADE,
    balance_id BIGINT,
    birth_date DATE NOT NULL,
    birth_weight NUMERIC(8, 3),
    observations TEXT,
    offspring_number INTEGER NOT NULL,
    CONSTRAINT animal_birth_offspring_positive_chk CHECK (offspring_number > 0),
    CONSTRAINT animal_birth_weight_non_negative_chk CHECK (birth_weight IS NULL OR birth_weight >= 0)
);

CREATE TABLE porcine_birth_transition_decision (
    birth_id BIGINT PRIMARY KEY REFERENCES animal_birth(id) ON DELETE CASCADE,
    effective_date DATE NOT NULL,
    to_rears INTEGER NOT NULL DEFAULT 0,
    to_sows_reposition INTEGER NOT NULL DEFAULT 0,
    to_males_reposition INTEGER NOT NULL DEFAULT 0,
    baseline_rears_consumed INTEGER NOT NULL DEFAULT 0,
    baseline_sows_reposition_consumed INTEGER NOT NULL DEFAULT 0,
    baseline_males_reposition_consumed INTEGER NOT NULL DEFAULT 0,
    resolved_at TIMESTAMPTZ NOT NULL,
    balance_id BIGINT,
    CONSTRAINT porcine_birth_transition_decision_non_negative_chk CHECK (
        to_rears >= 0
        AND to_sows_reposition >= 0
        AND to_males_reposition >= 0
        AND baseline_rears_consumed >= 0
        AND baseline_sows_reposition_consumed >= 0
        AND baseline_males_reposition_consumed >= 0
    )
);

ALTER TABLE animal
    ADD CONSTRAINT animal_source_birth_id_fkey
    FOREIGN KEY (source_birth_id) REFERENCES animal_birth(id) ON DELETE SET NULL;

CREATE TABLE vaccination (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    animal_id BIGINT NOT NULL REFERENCES animal(id) ON DELETE CASCADE,
    next_dose DATE,
    observations TEXT,
    responsible_veterinary BIGINT,
    vaccination_batch VARCHAR(120),
    vaccination_date DATE NOT NULL,
    vaccination_type VARCHAR(120) NOT NULL
);

CREATE TABLE movement_certificate (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    origin_livestock_id BIGINT REFERENCES livestock_farm(id) ON DELETE RESTRICT,
    destination_livestock_id BIGINT REFERENCES livestock_farm(id) ON DELETE RESTRICT,
    arrival_date TIMESTAMPTZ,
    cod_remo VARCHAR(80),
    departure_date TIMESTAMPTZ NOT NULL,
    means_of_transport VARCHAR(120),
    number_of_animals INTEGER NOT NULL,
    origin_external_code VARCHAR(32),
    origin_external_name VARCHAR(180),
    destination_external_code VARCHAR(32),
    destination_external_name VARCHAR(180),
    serie VARCHAR(80),
    solicitation_date TIMESTAMPTZ,
    specie VARCHAR(40) NOT NULL,
    status VARCHAR(32) NOT NULL DEFAULT 'Pending',
    transport_name VARCHAR(180),
    vehicle_registration_number VARCHAR(40),
    animal_type VARCHAR(80),
    unidentified_category VARCHAR(40),
    CONSTRAINT movement_number_of_animals_positive_chk CHECK (number_of_animals > 0),
    CONSTRAINT movement_dates_chk CHECK (arrival_date IS NULL OR arrival_date >= departure_date),
    CONSTRAINT movement_status_chk CHECK (status IN ('Pending', 'Confirmed')),
    CONSTRAINT movement_unidentified_category_chk CHECK (
        unidentified_category IS NULL OR unidentified_category IN ('Under4Months', 'Between4And12Months')
    )
);

CREATE TABLE movement_certificate_animals (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    movement_certificate_id BIGINT NOT NULL REFERENCES movement_certificate(id) ON DELETE CASCADE,
    animal_id BIGINT NOT NULL REFERENCES animal(id) ON DELETE RESTRICT,
    UNIQUE (movement_certificate_id, animal_id)
);

CREATE TABLE census (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    livestock_farm_id BIGINT NOT NULL REFERENCES livestock_farm(id) ON DELETE CASCADE,
    census_date DATE NOT NULL
);

CREATE TABLE census_ovino_caprino (
    census_id BIGINT PRIMARY KEY REFERENCES census(id) ON DELETE CASCADE,
    non_reproductive_between_4_12m INTEGER NOT NULL DEFAULT 0,
    non_reproductive_under_4m INTEGER NOT NULL DEFAULT 0,
    reproductive_female INTEGER NOT NULL DEFAULT 0,
    reproductive_male INTEGER NOT NULL DEFAULT 0,
    CONSTRAINT census_ovino_caprino_non_negative_chk CHECK (
        non_reproductive_between_4_12m >= 0
        AND non_reproductive_under_4m >= 0
        AND reproductive_female >= 0
        AND reproductive_male >= 0
    )
);

CREATE TABLE census_porcino (
    census_id BIGINT PRIMARY KEY REFERENCES census(id) ON DELETE CASCADE,
    baits INTEGER NOT NULL DEFAULT 0,
    boars INTEGER NOT NULL DEFAULT 0,
    piglets INTEGER NOT NULL DEFAULT 0,
    pigs_reposition INTEGER NOT NULL DEFAULT 0,
    rears INTEGER NOT NULL DEFAULT 0,
    sow INTEGER NOT NULL DEFAULT 0,
    sows_reposition INTEGER NOT NULL DEFAULT 0,
    CONSTRAINT census_porcino_non_negative_chk CHECK (
        baits >= 0
        AND boars >= 0
        AND piglets >= 0
        AND pigs_reposition >= 0
        AND rears >= 0
        AND sow >= 0
        AND sows_reposition >= 0
    )
);

CREATE TABLE balance (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    livestock_farm_id BIGINT NOT NULL REFERENCES livestock_farm(id) ON DELETE CASCADE,
    balance_date DATE NOT NULL,
    destination_livestock_code VARCHAR(32),
    modification_cause VARCHAR(80) NOT NULL,
    number_of_animals INTEGER NOT NULL,
    origin_livestock_code VARCHAR(32),
    CONSTRAINT balance_number_of_animals_positive_chk CHECK (number_of_animals > 0)
);

CREATE TABLE balance_ovino_caprino (
    balance_id BIGINT PRIMARY KEY REFERENCES balance(id) ON DELETE CASCADE,
    non_reproductive_between_4_12m INTEGER NOT NULL DEFAULT 0,
    non_reproductive_under_4m INTEGER NOT NULL DEFAULT 0,
    reproductive_females INTEGER NOT NULL DEFAULT 0,
    reproductive_males INTEGER NOT NULL DEFAULT 0,
    transport_ticket_number VARCHAR(80),
    transporter_name VARCHAR(180),
    CONSTRAINT balance_ovino_caprino_non_negative_chk CHECK (
        non_reproductive_between_4_12m >= 0
        AND non_reproductive_under_4m >= 0
        AND reproductive_females >= 0
        AND reproductive_males >= 0
    )
);

ALTER TABLE animal_birth
    ADD CONSTRAINT animal_birth_balance_id_fkey
    FOREIGN KEY (balance_id) REFERENCES balance(id) ON DELETE SET NULL;

ALTER TABLE porcine_birth_transition_decision
    ADD CONSTRAINT porcine_birth_transition_decision_balance_id_fkey
    FOREIGN KEY (balance_id) REFERENCES balance(id) ON DELETE SET NULL;

CREATE TABLE balance_porcino (
    balance_id BIGINT PRIMARY KEY REFERENCES balance(id) ON DELETE CASCADE,
    baits INTEGER NOT NULL DEFAULT 0,
    boars INTEGER NOT NULL DEFAULT 0,
    breed VARCHAR(80),
    piglets INTEGER NOT NULL DEFAULT 0,
    pigs_reposition INTEGER NOT NULL DEFAULT 0,
    rear INTEGER NOT NULL DEFAULT 0,
    sows_for_live INTEGER NOT NULL DEFAULT 0,
    sows_reposition INTEGER NOT NULL DEFAULT 0,
    tag VARCHAR(80),
    type VARCHAR(80),
    CONSTRAINT balance_porcino_non_negative_chk CHECK (
        baits >= 0
        AND boars >= 0
        AND piglets >= 0
        AND pigs_reposition >= 0
        AND rear >= 0
        AND sows_for_live >= 0
        AND sows_reposition >= 0
    )
);

CREATE TABLE incident (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    livestock_farm_id BIGINT NOT NULL REFERENCES livestock_farm(id) ON DELETE CASCADE,
    animal_id BIGINT REFERENCES animal(id) ON DELETE SET NULL,
    change_reason VARCHAR(120),
    description TEXT,
    incident_date DATE NOT NULL,
    last_identification VARCHAR(80),
    new_identification VARCHAR(80)
);

CREATE TABLE inspection (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    livestock_farm_id BIGINT NOT NULL REFERENCES livestock_farm(id) ON DELETE CASCADE,
    inspection_date DATE NOT NULL,
    observations TEXT,
    reason VARCHAR(120),
    tagged_animals INTEGER,
    veterinary VARCHAR(180),
    CONSTRAINT inspection_tagged_animals_non_negative_chk CHECK (tagged_animals IS NULL OR tagged_animals >= 0)
);

CREATE INDEX idx_livestock_farm_farmer_id ON livestock_farm(farmer_id);
CREATE INDEX idx_animal_livestock_farm_id ON animal(livestock_farm_id);
CREATE INDEX idx_vaccination_animal_id ON vaccination(animal_id);
CREATE INDEX idx_movement_origin_livestock_id ON movement_certificate(origin_livestock_id);
CREATE INDEX idx_movement_destination_livestock_id ON movement_certificate(destination_livestock_id);
CREATE INDEX idx_movement_animals_animal_id ON movement_certificate_animals(animal_id);
CREATE INDEX idx_census_livestock_farm_id ON census(livestock_farm_id);
CREATE INDEX idx_balance_livestock_farm_id ON balance(livestock_farm_id);
CREATE INDEX idx_incident_livestock_farm_id ON incident(livestock_farm_id);
CREATE INDEX idx_incident_animal_id ON incident(animal_id);
CREATE INDEX idx_inspection_livestock_farm_id ON inspection(livestock_farm_id);

COMMIT;
