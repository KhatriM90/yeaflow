# YeaFlow — Tech Stack and Conventions

## Stack

| Layer | Choice | Why |
|---|---|---|
| API | ASP.NET Core, .NET 10 LTS, C# | Native `decimal` for money; DI API reachable from the same codebase if a Service Layer gap needs it |
| Connector | .NET Worker Service, self-contained, runs as a Windows service near SAP | Fits on-prem SAP servers; no runtime to install |
| App database | PostgreSQL 16+, EF Core migrations | No per-tenant licence cost; row-level security as a second isolation layer |
| Background jobs | Hangfire on PostgreSQL | Retries, scheduled reports, refresh polling without extra infrastructure |
| Front end | React 18+ with TypeScript, Vite | Team familiarity; strong ecosystem |
| Data fetching | TanStack Query | Caching, stale indicators, retries |
| Document lines grid | AG Grid (Community to start) | Keyboard-driven line entry close to SAP behaviour |
| Forms / validation | React Hook Form + Zod | Typed forms; same rules mirrored server-side with FluentValidation |
| UI components | Radix UI primitives styled with the YeaFlow tokens (or shadcn/ui) | Accessible primitives; our own look |
| Auth | ASP.NET Core Identity + OpenID Connect; MFA for privileged roles | SSO via Microsoft 365 / Google per tenant later |
| PDFs | QuestPDF | Server-side, template-driven document printing |
| Excel export | ClosedXML | Reports and list exports |
| Observability | Serilog + OpenTelemetry; health checks | Correlation IDs end to end |
| CI/CD | GitHub Actions; container images for API/front end | Pilot-tenant-first rollout |

## Solution structure

```
yeaflow/
  src/
    YeaFlow.Api/              ASP.NET Core API (controllers / minimal APIs, auth, permissions)
    YeaFlow.Application/      use cases per module: Sales, Purchasing, BusinessPartners, Inventory, Banking, Production, Reports
    YeaFlow.Domain/           entities, value objects (Money, Quantity), posting-intent state machine
    YeaFlow.Infrastructure/   EF Core, PostgreSQL, Hangfire, email, PDF, file storage
    YeaFlow.SapConnector/     Worker service near SAP: Service Layer client, session pool, refresh polling
    YeaFlow.Sap.Contracts/    typed DTOs for Service Layer entities shared by API and connector
    YeaFlow.Fiscal/           fiscalisation adapter interface + TIMS adapters (eTIMS later)
    YeaFlow.Reporting/        report definitions, SQL dialects (SqlServer, Hana)
  web/                        React + TypeScript front end
  tests/
    YeaFlow.UnitTests/
    YeaFlow.IntegrationTests/ runs against an isolated SAP test company
  docs/                       API coverage matrix, report specs, runbooks
```

## Conventions

- **Tenancy:** every table has `TenantId`; every query goes through a tenant-scoped context; PostgreSQL row-level security enabled on tenant tables. Company access is explicit per user.
- **Money:** `decimal` in C#, `numeric(19,6)` in PostgreSQL, strings in JSON for amounts; round only with SAP currency precision.
- **Posting:** every write to SAP creates a `PostingIntent` row (transaction ID, tenant, company, actor, payload hash, state) before the call. States: Draft → AwaitingApproval → Queued → Submitting → Posted | Failed | OutcomeUnknown.
- **Service Layer:** one session pool per tenant company in the connector; re-login on 401; respect ETag / re-read before PATCH; never expose SAP credentials to the browser.
- **Errors:** return RFC 7807 problem details; preserve SAP error code and message safely for the user.
- **Permissions:** policy-based authorization (`Sales.Order.Create`, `Sales.Order.Approve`, `Reports.Margin.View` …) checked in the application layer, not only in controllers or the UI.
- **Naming:** C# PascalCase; TypeScript camelCase; API routes `/api/v1/{module}/{resource}`; requirement IDs (SAL01 …) in commit messages and PR titles.
- **Testing:** unit tests for domain logic; integration tests against a dedicated SAP test company for every document chain in scope section 5.
- **Secrets:** environment variables / secret store only; never in source.
