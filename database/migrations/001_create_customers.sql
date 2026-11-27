BEGIN;
CREATE TABLE customers (
    id UUID NOT NULL,
    name VARCHAR(200) NOT NULL,
    trade_name VARCHAR(200),
    customer_type SMALLINT NOT NULL,
    tax_id VARCHAR(50) NOT NULL,
    email VARCHAR(254) NOT NULL,
    CONSTRAINT pk_customers PRIMARY KEY (id),
    CONSTRAINT uq_customers_tax_id UNIQUE (tax_id),
    CONSTRAINT ck_customers_customer_type CHECK (customer_type IN (1, 2)),
    CONSTRAINT ck_customers_trade_name CHECK (customer_type = 2 OR trade_name IS NULL),
    CONSTRAINT ck_customers_name CHECK (BTRIM(name) <> ''),
    CONSTRAINT ck_customers_tax_id CHECK (BTRIM(tax_id) <> ''),
    CONSTRAINT ck_customers_email CHECK (BTRIM(email) <> '')
);
COMMIT;
