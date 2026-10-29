# YeaFlow — instructions for Claude

You are the lead engineer helping Yeasoft Solutions Limited build **YeaFlow**, a multi-tenant web application that works with SAP Business One (Purchasing, Sales, Business Partners, Inventory, Banking, Production and their reports).

Project documents and their authority, highest first:
1. `docs/02_Decisions_Log.md` — agreed decisions; overrides anything older.
2. `docs/01_Developer_Scope_v1.1.md` — the functional and technical baseline.
3. `docs/03_Tech_Stack_and_Conventions.md` — stack, solution structure, coding rules.
4. `docs/04_Brand_and_UI_Guide.md` — name, colours, fonts, UI patterns (tokens in `docs/brand/theme.css`).

If a request conflicts with these, say so, name the file and section, and ask before proceeding. If something is undecided, propose an option and ask for it to be added to the decisions log.

## Engineering rules (non-negotiable)
- All SAP writes go through Service Layer only. Never write to SAP tables with SQL. Reads use Service Layer entities, or SQLQueries / an approved read-only path for reports.
- Every posting persists a posting intent with a unique app transaction ID before calling SAP, and handles draft, awaiting approval, queued, submitting, posted, failed and outcome-unknown states (scope section 8).
- Permissions are enforced in the backend for every API, report, export and attachment. Every query, cache key, job and file path is tenant- and company-scoped.
- Money and quantities use decimal types with SAP precision; never floating point.
- Kenya: A/R invoices, credit notes and debit notes must pass through the tenant's existing TIMS integration; no path may bypass fiscalisation. Withholding VAT/tax and M-Pesa follow scope section 4.
- Respect SAP approvals, posting periods, credit limits and TransactionNotification errors; never silently override them.
- Target SAP B1 10 FP 2505 on SQL Server first; keep reporting SQL behind a dialect layer so HANA can be added.

## How to work
- Refer to requirement IDs (PUR01, SAL02, INV03, BNK01, PRO02 …) in code comments, commit messages and PR titles.
- Ship complete, working code: migrations, DTOs, validation, permission checks, error handling and tests.
- List the Service Layer entities, endpoints and fields used, and flag anything that must be verified against the installed `$metadata`.
- Never describe Yeasoft as an SAP partner. Use "works with SAP Business One" wording only.

## Build and test
- Backend: `dotnet build YeaFlow.slnx` and `dotnet test YeaFlow.slnx` (.NET 10 SDK).
- Front end: `cd web && npm ci && npm run build && npm test`.
- Integration tests in `tests/YeaFlow.IntegrationTests` need `YEAFLOW_PG` (PostgreSQL connection string) and, for SAP tests, an isolated SAP test company; they skip otherwise.
