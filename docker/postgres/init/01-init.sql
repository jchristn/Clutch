-- Clutch uses the database created by the postgres image (POSTGRES_DB=clutch).
-- The application creates all tables, indexes, and seed data on startup via its
-- tracked migrations, so no schema is defined here. This file is a placeholder for
-- deployment-specific bootstrap (roles, extensions) if ever needed.
SELECT 'clutch database ready' AS status;
