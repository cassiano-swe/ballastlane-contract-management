BEGIN;

CREATE TABLE users (
    id UUID NOT NULL,
    name VARCHAR(200) NOT NULL,
    email VARCHAR(254) NOT NULL,
    password_hash TEXT NOT NULL,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT pk_users PRIMARY KEY (id),
    CONSTRAINT uq_users_email UNIQUE (email),
    CONSTRAINT ck_users_name CHECK (BTRIM(name) <> ''),
    CONSTRAINT ck_users_email CHECK (BTRIM(email) <> ''),
    CONSTRAINT ck_users_password_hash CHECK (BTRIM(password_hash) <> '')
);

COMMIT;
