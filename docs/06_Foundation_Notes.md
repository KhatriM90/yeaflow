# Foundation notes (milestone 2.1 + groundwork for 1.2 / 1.3)

What exists, how to run it, and what must still be verified against a real SAP company.

## Run locally

Prerequisites: .NET 10 SDK, Node 22, PostgreSQL 16.

```bash
# 1. Database (owner role runs migrations; the API should use yeaflow_app, see deploy/postgres/app-role.sql)
export YEAFLOW_PG="Host=localhost;Database=yeaflow;Username=yeaflow_owner;Password=..."
dotnet tool restore
dotnet ef database update -p src/YeaFlow.Infrastructure -s src/YeaFlow.Infrastructure

# 2. API (needs an OIDC authority in Auth:Authority for real sign-in)
ConnectionStrings__YeaFlow="$YEAFLOW_PG" dotnet run --project src/YeaFlow.Api --urls http://localhost:5080

# 3. Front end
cd web && npm ci && npm run dev   # http://localhost:5173, proxies /api to :5080

# Tests
dotnet test --solution YeaFlow.slnx   # integration tests run when YEAFLOW_PG points at a CREATEDB role that is NOT a superuser
cd web && npm test
```

## What is built

| Area | Where | Notes |
|---|---|---|
| Money / Quantity | `YeaFlow.Domain/Values` | `decimal` only, SAP half-away-from-zero rounding, numeric(19,6) in PostgreSQL. The browser uses exact BigInt decimals (`web/src/lib/decimal.ts`). |
| Posting intent state machine | `YeaFlow.Domain/Posting` | Draft → AwaitingApproval → Queued → Submitting → Posted / Failed / OutcomeUnknown. Unique transaction ID per tenant, payload hash, bounded retry with backoff, settle window before an absent document is treated as "not posted", controlled review when recovery gives up, recovery of submissions stuck by a crash. |
| Posting service + jobs | `YeaFlow.Application/Postings` | Persist-before-send, duplicate-click handling, optimistic claim so only one worker submits, reconciliation by `U_YF_TxnId`, per-tenant Hangfire jobs. |
| Tenancy and seats | `YeaFlow.Domain/Tenancy`, `Application/Users/SeatService` | Starter 5 / Business 10 / Enterprise 20, each person counted once, tenant row lock so concurrent activations cannot overshoot, downgrade only if active users fit. |
| Roles and permissions | `YeaFlow.Domain/Identity` | Eight roles from scope section 4, `{Module}.{Resource}.{Action}` permissions, MFA required for app admin, approver and banking. Enforced in the application layer; the API resolves roles from the app DB, never from the token. |
| Isolation | `Infrastructure/Persistence` + migration `TenantRowLevelSecurity` | EF query filters **and** PostgreSQL RLS (FORCEd) on every tenant table, cross-tenant write guard, append-only audit trail enforced by trigger. Superuser and BYPASSRLS roles skip RLS, so the API and tests must never connect as one. |
| Service Layer client | `YeaFlow.SapConnector/ServiceLayer` | Login, bounded session pool per company (licence-aware), re-login on 401, ETag/If-Match PATCH, SQLQueries with bound params, failure classification (Rejected / TransientNotSent / OutcomeUnknown / ConcurrencyConflict). No SQL write path exists. |
| Fiscalisation | `YeaFlow.Fiscal` | `IFiscalisationAdapter`, `TimsAddOnAdapter` (reads the tenant's existing add-on UDFs, optional trigger flag), `FiscalPrintGuard` that blocks final tax-invoice print/email until signed with CUIN, CU serial and QR. |
| Reporting dialects | `YeaFlow.Reporting` | SQL Server and HANA dialects behind `ISqlDialect`; identifier and page-size validation. |
| Connector relay (D-007) | `Infrastructure/Connectors`, `Api/Endpoints/ConnectorEndpoints.cs`, `SapConnector/Relay` | The connector long-polls `POST /connector/v1/commands/lease`, runs each command against Service Layer and reports to `/commands/{id}/result`. Commands are leased with `FOR UPDATE SKIP LOCKED`, are company- and tenant-scoped under RLS, and can be withdrawn only while nobody has leased them, which is what makes "not sent" provable. Connector tokens cannot call `/api/v1` and user sign-ins cannot call `/connector/v1`. |
| API | `YeaFlow.Api` | `/api/v1/postings`, reconciliation console endpoints, seats, operator tenant creation, connector registration and company setup, `/me`, RFC 7807 errors, health checks, Serilog, OpenTelemetry (OTLP when configured), Hangfire. |
| Front end | `web/` | React + TypeScript + Vite, brand theme, Ink sidebar with all modules, global search box, company switcher, status chips, AG Grid lines grid with exact totals, document-screen layout preview. |
| CI | `.github/workflows` | Build with analyzers as errors, unit + PostgreSQL integration tests, migration drift check, npm audit, gitleaks secret scan, CodeQL, Dependabot. |

## Connect a connector (D-007)

1. Operator (role `yeaflow_operator`, MFA) registers a connector: `POST /api/v1/platform/tenants/{tenantId}/connectors {"name":"Head office"}`. The response holds `connectorId` and `token`; the token is shown **once**.
2. Operator adds each SAP company on that connector: `POST /api/v1/platform/tenants/{tenantId}/companies {"name","sapCompanyDb","databaseType":"SqlServer","connectorId"}`.
3. On the customer server, set the connector's `Connector` section: `ConnectorId`, `ApiBaseUrl` (e.g. `https://app.yeaflow.example/`), `Token` (from a secret store or environment variable `Connector__Token`, never committed), and one `Companies` entry per SAP company with its Service Layer URL and technical user.
4. Start the connector service. `GET /api/v1/platform/tenants/{tenantId}/connectors` shows it online once heartbeats arrive.

Relay timings live in the API's `Relay` section: `CommandTimeout` (30 s, how long a user's submit waits), `PollInterval`, `MaxLeaseWait` (25 s).

## Not built yet (next steps, in order)

1. **Identity (2.2):** OIDC sign-in per tenant, user/role admin screens, tenant-editable role permissions. The web sign-in is a local preview session until then.
2. **Operator console (2.3):** screens over the connector/company endpoints, Service Layer connectivity test, company discovery, token rotation, onboarding checklist.
3. Business modules from milestone 3 onward (BP, Sales with TIMS, Purchasing, Inventory, reports).

## Must verify against the installed $metadata / test company

- `U_YF_TxnId` UDF (alphanumeric 50) can be added to every document object we post, and filtering on it works through Service Layer.
- Service Layer behaviour when an approval procedure applies: does it return a draft (and which status/error), per document type. `SapPostOutcome.DraftForApproval` is modelled but not yet detected.
- Error classification: which business errors come back as 400 vs 500 on FP 2505, and TransactionNotification message format.
- Session timeout value and licence consumption per Service Layer session for the tenant's licence strategy (Q-004).
- Long-poll through the customer's proxy/firewall: idle HTTPS requests of ~25 s must not be cut; lower `WaitSeconds` if they are.
- TIMS add-on UDF names, signed/failed values and trigger method per tenant (Q-003).
