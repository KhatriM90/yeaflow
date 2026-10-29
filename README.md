# YeaFlow

*Your SAP Business One operations — sales, purchasing, stock, banking and production — in one fast web app.*

A Yeasoft product. Works with SAP Business One.

| | |
|---|---|
| Scope, decisions, stack, brand | [`docs/`](docs/) (the decisions log overrides the scope) |
| How to run, what is built, what to verify | [`docs/06_Foundation_Notes.md`](docs/06_Foundation_Notes.md) |
| Instructions for Claude Code | [`CLAUDE.md`](CLAUDE.md) |

```
src/
  YeaFlow.Api/            ASP.NET Core API (.NET 10)
  YeaFlow.Application/    use cases, permission checks
  YeaFlow.Domain/         Money, posting-intent state machine, tenancy, roles
  YeaFlow.Infrastructure/ EF Core + PostgreSQL (row-level security), jobs
  YeaFlow.SapConnector/   Windows service near SAP: Service Layer client
  YeaFlow.Sap.Contracts/  Service Layer DTOs
  YeaFlow.Fiscal/         fiscalisation adapters (KRA TIMS)
  YeaFlow.Reporting/      report definitions, SQL Server / HANA dialects
web/                      React + TypeScript front end
tests/                    unit and integration tests
```
