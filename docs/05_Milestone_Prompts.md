# YeaFlow — Chat Starters

Start one new chat per topic inside the Claude Project, and work through them in milestone order (scope section 10). Copy a prompt, fill in anything in [brackets], and send. When a chat produces a decision, add it to 02_Decisions_Log.md and re-upload the file.

## Milestone 1 — Discovery and feasibility

**1.1 API coverage matrix**
> Build the mandatory API coverage matrix from scope section 8 for every requirement ID in sections 5–7. For each: SAP object/entity, Service Layer endpoint and method, required fields, base-document linkage, approval behaviour, TIMS impact, and a status of verified / conditional / unsupported / optional based on SAP B1 10 FP 2505. Output as a table I can paste into Excel, and list what I must verify against $metadata.

**1.2 Service Layer spike**
> Write the YeaFlow.SapConnector Service Layer client: login, session pool per tenant company, re-login on 401, ETag handling, typed GET/POST/PATCH, SQLQueries execution, and structured SAP error mapping. Include a console test that creates a draft sales order and reads it back.

**1.3 Posting intent and outcome-unknown recovery**
> Implement the PostingIntent entity, state machine and repository (scope section 8, posting and recovery). Include duplicate-click protection, bounded retries, and the reconciliation job that resolves OutcomeUnknown by looking up the SAP document via a UDF holding our transaction ID.

## Milestone 2 — Foundation

**2.1 Solution scaffold**
> Scaffold the full solution from 03_Tech_Stack_and_Conventions.md: projects, references, EF Core context with TenantId and PostgreSQL row-level security, Serilog, OpenTelemetry, health checks, Hangfire, and a GitHub Actions build. Give every file.

**2.2 Identity, roles and permissions**
> Implement users, roles, company access and policy-based permissions for the roles in scope section 4 (view/create/update/approve/cancel/export, plus branch, warehouse and cost/margin restrictions). Include MFA for privileged roles and the audit trail.

**2.3 Tenants, packages and seats**
> Implement tenants, subscriptions and seat limits for Starter (1–5), Business (6–10) and Enterprise (11–20) per scope section 3, plus the Yeasoft operator console endpoints for onboarding a tenant and registering a connector.

**2.4 Front-end shell**
> Build the React + TypeScript app shell using 3_Brand_Assets/theme.css: Ink sidebar with the modules, top bar with global search and company switcher, login, role-based home dashboard placeholder, status chip component and AG Grid wrapper for document lines.

## Milestone 3 — Operational core

**3.1 Business Partners (BP01–BP03)**
> Implement BP01–BP03 end to end: API, connector calls, permissions, React list and form screens, duplicate detection and tests.

**3.2 Sales documents (SAL01–SAL04)**
> Implement the sales chain quotation → order → delivery → A/R invoice with copy-from/copy-to, partial quantities, stock and price context on lines, approvals and printing. A/R invoices must call the fiscalisation adapter (TIMS) before final print.

**3.3 Fiscalisation adapter**
> Implement YeaFlow.Fiscal: the IFiscalisationAdapter interface, a TIMS adapter that works with an existing SAP B1 TIMS add-on (detect signed status from [UDFs/table the add-on uses]), status polling, and the print block until signed.

**3.4 Purchasing (PUR01–PUR05)** · **3.5 Inventory (INV01–INV06)** · **3.6 Module reports**
> Implement [requirement IDs] end to end, following the same pattern as Sales.

## Milestone 4 — Banking and Production

**4.1 Payments (BNK01–BNK04)**
> Implement incoming and outgoing payments with allocation, on-account amounts, withholding VAT/tax capture and M-Pesa payment means per scope section 4.

**4.2 Production (PRO01–PRO04)**
> Implement BOMs, production orders, issue for production and receipt from production with batch/serial/bin handling.

## Milestone 5 — Pilot and handover

**5.1 Acceptance tests**
> Write the automated acceptance test suite for the table in scope section 9 against an isolated SAP test company.

**5.2 Runbook and user guide**
> Draft the operations runbook, connector install guide and end-user guide for YeaFlow.
