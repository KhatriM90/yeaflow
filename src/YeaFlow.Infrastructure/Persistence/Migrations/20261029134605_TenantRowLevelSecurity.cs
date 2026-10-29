using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YeaFlow.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Second isolation layer (03_Tech_Stack_and_Conventions, Tenancy): PostgreSQL row-level security on every tenant table,
    /// keyed on the <c>app.tenant_id</c> setting written by TenantConnectionInterceptor. FORCE applies it to the table owner too.
    /// Also makes the audit trail append-only.
    /// </summary>
    public partial class TenantRowLevelSecurity : Migration
    {
        private static readonly string[] TenantTables = ["companies", "users", "user_company_access", "posting_intents", "audit_events"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantTables)
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {table} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON {table}
                        USING ("TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                        WITH CHECK ("TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                    """);
            }

            migrationBuilder.Sql("""
                CREATE FUNCTION audit_events_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_events is append-only';
                END;
                $$;
                CREATE TRIGGER audit_events_no_update_delete
                    BEFORE UPDATE OR DELETE OR TRUNCATE ON audit_events
                    FOR EACH STATEMENT EXECUTE FUNCTION audit_events_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS audit_events_no_update_delete ON audit_events;
                DROP FUNCTION IF EXISTS audit_events_append_only();
                """);

            foreach (var table in TenantTables)
            {
                migrationBuilder.Sql($"""
                    DROP POLICY IF EXISTS tenant_isolation ON {table};
                    ALTER TABLE {table} NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE {table} DISABLE ROW LEVEL SECURITY;
                    """);
            }
        }
    }
}
