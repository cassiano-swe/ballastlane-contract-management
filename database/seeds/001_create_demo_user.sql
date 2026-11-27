-- Isolated development only. The demonstration password is publicly known.
BEGIN;

INSERT INTO users (id, name, email, password_hash, active)
VALUES ('4a23620c-6994-4d67-80cc-9b347568f585', 'Demo User', 'demo@ballastlane.example',
        'AQAAAAIAAYagAAAAEC3vMkO4S3Nhij1AfDZnt8snw/0hcyEoLDIObe4eRopxlmRJ0UVCwqjDjyfatD+TMA==', TRUE)
ON CONFLICT (email) DO NOTHING;

COMMIT;
