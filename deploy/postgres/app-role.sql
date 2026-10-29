-- Least-privilege runtime role for the YeaFlow API (scope section 9).
-- Migrations run as the owner role; the API connects as yeaflow_app, which cannot alter tables or disable
-- row-level security. RLS is FORCEd on tenant tables, so even the owner sees only the tenant in app.tenant_id.
-- Run once per environment as a superuser, after the first migration. Set the password from your secret store.

CREATE ROLE yeaflow_app LOGIN;
GRANT CONNECT ON DATABASE yeaflow TO yeaflow_app;
GRANT USAGE ON SCHEMA public TO yeaflow_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO yeaflow_app;
REVOKE UPDATE, DELETE, TRUNCATE ON audit_events FROM yeaflow_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO yeaflow_app;

-- Hangfire keeps its own schema.
CREATE SCHEMA IF NOT EXISTS hangfire AUTHORIZATION yeaflow_app;
