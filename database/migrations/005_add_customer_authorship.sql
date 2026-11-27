BEGIN;

ALTER TABLE customers
    ADD COLUMN created_by_user_id UUID,
    ADD COLUMN updated_by_user_id UUID,
    ADD CONSTRAINT fk_customers_created_by_user FOREIGN KEY (created_by_user_id) REFERENCES users (id) ON DELETE RESTRICT,
    ADD CONSTRAINT fk_customers_updated_by_user FOREIGN KEY (updated_by_user_id) REFERENCES users (id) ON DELETE RESTRICT;

COMMIT;
