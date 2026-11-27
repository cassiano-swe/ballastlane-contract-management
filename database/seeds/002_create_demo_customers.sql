-- Isolated development/demo databases only. All records are fictional.
-- The demo user seed and migrations 001-005 must be applied first.
BEGIN;

INSERT INTO customers (
    id, name, trade_name, customer_type, tax_id, email, version, active,
    created_by_user_id, updated_by_user_id)
VALUES
    ('10000000-0000-4000-8000-000000000001', 'Avery Brooks', NULL, 1, 'BL-DEMO-I-001', 'avery.brooks@example.invalid', '20000000-0000-4000-8000-000000000001', TRUE,  '4a23620c-6994-4d67-80cc-9b347568f585', NULL),
    ('10000000-0000-4000-8000-000000000002', 'Blake Chen', NULL, 1, 'BL-DEMO-I-002', 'blake.chen@example.invalid', '20000000-0000-4000-8000-000000000002', TRUE,  '4a23620c-6994-4d67-80cc-9b347568f585', NULL),
    ('10000000-0000-4000-8000-000000000003', 'Casey Diaz', NULL, 1, 'BL-DEMO-I-003', 'casey.diaz@example.invalid', '20000000-0000-4000-8000-000000000003', TRUE,  '4a23620c-6994-4d67-80cc-9b347568f585', NULL),
    ('10000000-0000-4000-8000-000000000004', 'Devon Ellis', NULL, 1, 'BL-DEMO-I-004', 'devon.ellis@example.invalid', '20000000-0000-4000-8000-000000000004', TRUE,  '4a23620c-6994-4d67-80cc-9b347568f585', NULL),
    ('10000000-0000-4000-8000-000000000005', 'Emery Flores', NULL, 1, 'BL-DEMO-I-005', 'emery.flores@example.invalid', '20000000-0000-4000-8000-000000000005', TRUE,  '4a23620c-6994-4d67-80cc-9b347568f585', NULL),
    ('10000000-0000-4000-8000-000000000006', 'Finley Green', NULL, 1, 'BL-DEMO-I-006', 'finley.green@example.invalid', '20000000-0000-4000-8000-000000000006', TRUE,  '4a23620c-6994-4d67-80cc-9b347568f585', NULL),
    ('10000000-0000-4000-8000-000000000007', 'Gray Harper', NULL, 1, 'BL-DEMO-I-007', 'gray.harper@example.invalid', '20000000-0000-4000-8000-000000000007', FALSE, '4a23620c-6994-4d67-80cc-9b347568f585', NULL),
    ('10000000-0000-4000-8000-000000000008', 'Hayden Irving', NULL, 1, 'BL-DEMO-I-008', 'hayden.irving@example.invalid', '20000000-0000-4000-8000-000000000008', FALSE, '4a23620c-6994-4d67-80cc-9b347568f585', NULL),
    ('10000000-0000-4000-8000-000000000009', 'Juniper Labs LLC', 'Juniper Labs', 2, 'BL-DEMO-C-001', 'contact@juniper-labs.example.invalid', '20000000-0000-4000-8000-000000000009', TRUE,  '4a23620c-6994-4d67-80cc-9b347568f585', NULL),
    ('10000000-0000-4000-8000-000000000010', 'Keystone Works Inc', 'Keystone Works', 2, 'BL-DEMO-C-002', 'contact@keystone-works.example.invalid', '20000000-0000-4000-8000-000000000010', TRUE,  '4a23620c-6994-4d67-80cc-9b347568f585', NULL),
    ('10000000-0000-4000-8000-000000000011', 'Lumen Harbor Ltd', 'Lumen Harbor', 2, 'BL-DEMO-C-003', 'contact@lumen-harbor.example.invalid', '20000000-0000-4000-8000-000000000011', TRUE,  '4a23620c-6994-4d67-80cc-9b347568f585', NULL),
    ('10000000-0000-4000-8000-000000000012', 'Meridian Pine Co', 'Meridian Pine', 2, 'BL-DEMO-C-004', 'contact@meridian-pine.example.invalid', '20000000-0000-4000-8000-000000000012', TRUE,  '4a23620c-6994-4d67-80cc-9b347568f585', NULL),
    ('10000000-0000-4000-8000-000000000013', 'Northwind Atelier LLC', 'Northwind Atelier', 2, 'BL-DEMO-C-005', 'contact@northwind-atelier.example.invalid', '20000000-0000-4000-8000-000000000013', TRUE,  '4a23620c-6994-4d67-80cc-9b347568f585', NULL),
    ('10000000-0000-4000-8000-000000000014', 'Orchard Signal Inc', 'Orchard Signal', 2, 'BL-DEMO-C-006', 'contact@orchard-signal.example.invalid', '20000000-0000-4000-8000-000000000014', FALSE, '4a23620c-6994-4d67-80cc-9b347568f585', NULL),
    ('10000000-0000-4000-8000-000000000015', 'Prairie Circuit Ltd', 'Prairie Circuit', 2, 'BL-DEMO-C-007', 'contact@prairie-circuit.example.invalid', '20000000-0000-4000-8000-000000000015', FALSE, '4a23620c-6994-4d67-80cc-9b347568f585', NULL)
ON CONFLICT (id) DO NOTHING;

COMMIT;
