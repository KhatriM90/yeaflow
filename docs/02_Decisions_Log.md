# YeaFlow — Decisions Log

Newest decisions override older ones and override the scope document where they conflict. Add a row whenever something is agreed; never delete a row — mark it superseded instead.

| ID | Date | Decision | Status |
|---|---|---|---|
| D-001 | 2026-10-29 | Product name is **YeaFlow** (a Yeasoft product). Descriptor: "Works with SAP Business One". No SAP logo; Yeasoft is not described as an SAP partner. | Agreed (pending domain/trademark check) |
| D-002 | 2026-10-29 | Stack: ASP.NET Core (.NET 10 LTS) API and connector, React + TypeScript front end, PostgreSQL app database. See 03_Tech_Stack_and_Conventions.md. | Agreed |
| D-003 | 2026-10-29 | Kenya fiscalisation baseline is **KRA TIMS** via each tenant's existing integration (SAP B1 TIMS add-on or middleware). Fiscalisation sits behind a pluggable adapter so eTIMS (OSCU/VSCU) can be added later. Native TIMS integration is a separately quoted option. | Agreed |
| D-004 | 2026-10-29 | First target: SAP B1 10 FP 2505 on SQL Server. HANA to be priced as an early phase; reporting SQL built behind a dialect layer from day one. | Agreed |
| D-005 | 2026-10-29 | Brand theme: Flow Teal #0F766E, Ink #0B1F2A, Savanna Amber #F59E0B, Mist #F6F8F9; Manrope / Inter / JetBrains Mono. See 04_Brand_and_UI_Guide.md. | Agreed |
| D-006 | 2026-10-29 | Developer scope v1.1 accepted as working baseline (adds UX, tenant configuration, document delivery, Kenya compliance, platform operations, secure SDLC, release/support). | Agreed |
| D-007 | 2026-10-30 | The SAP connector makes **outbound-only** HTTPS connections to the YeaFlow API and long-polls it for work; the customer opens no inbound ports. Each connector has its own token (`Authorization: Connector {tenant}.{connector}.{secret}`); the API stores only a SHA-256 hash and shows the token once at registration. A command the connector never took is withdrawn and treated as "not sent"; a command it took but did not answer is "outcome unknown" and goes to reconciliation. | Agreed |

## Open — to close during discovery

| ID | Question | Owner | Status |
|---|---|---|---|
| Q-001 | Native TIMS integration in the core product, or only use each customer's existing add-on? | Manish | Open |
| Q-002 | Pilot customer, their SAP patch, database (SQL/HANA) and company count | Manish | Open |
| Q-003 | Pilot customer's TIMS integration: add-on/middleware, control-unit type, trigger method | Manish | Open |
| Q-004 | SAP licence strategy for app users (indirect access vs named users) and technical-account approach | Manish | Open |
| Q-005 | Hosting: Yeasoft cloud (AWS/OCI) vs customer on-premises for the app; connector always near SAP | Manish | Open |
| Q-006 | Enabled bins/batches/serials, approval templates, currencies, branches, UDFs and add-ons at pilot | Manish | Open |
| Q-007 | VAT withholding agents among pilot customers; WHT codes in use; M-Pesa Paybill/Till accounts | Manish | Open |
| Q-008 | Dashboard KPIs per role, notification channels, print layouts per document type | Manish | Open |
| Q-009 | Domain (yeaflow.co.ke / .com) and trademark check for the YeaFlow name | Manish | Open |
| Q-010 | Pricing per package (Starter / Business / Enterprise) and support tiers | Manish | Open |
