BEGIN;

ALTER TABLE customers ADD COLUMN version UUID;

UPDATE customers SET version = gen_random_uuid() WHERE version IS NULL;

ALTER TABLE customers ALTER COLUMN version SET NOT NULL;

ALTER TABLE customers ADD CONSTRAINT ck_customers_version
    CHECK (version <> '00000000-0000-0000-0000-000000000000'::UUID);

COMMIT;
