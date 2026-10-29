# YeaFlow — SAP Business One Web Application: Developer Scope v1.1

Prepared for Yeasoft Solutions Limited and the development team  
Version 1.1 \| 29 October 2026 \| Proposed baseline for estimation and technical validation

## 1 Purpose and product boundary

Build a responsive web application for Purchasing, Sales, Business Partners, Inventory, Banking, Production and reports for each module. Integrate with SAP Business One through Service Layer. SAP remains the authoritative accounting and inventory system; the application provides its own operational interface and user experience.

SAP administration modules are excluded. The scope is the explicitly listed workflows, not an unconditional promise to reproduce every SAP screen or function. All listed baseline features must be estimated; anything unsupported must be identified with a proposed solution before implementation.

## 2 Deployment assumptions

- Initial validation target: SAP Business One 10 FP 2505 on Microsoft SQL Server, hosted on premises. Confirm the exact patch, localization, database, add-ons and enabled features during discovery. HANA support is a separately estimated option.

- **HANA recommendation:** price SAP B1 on HANA as an early phase rather than a distant option. Service Layer business endpoints behave largely the same on both databases, but report queries, SQLQueries syntax, calculation views and test effort differ. Build the reporting layer behind a database-dialect abstraction from day one so HANA is a test-and-tune exercise, not a rewrite.

- Web first, optimized for desktop and tablet. An installable PWA may be included; a packaged desktop application is optional. Offline transaction posting is excluded from the baseline.

- Product supports multiple customer tenants with strict isolation. Initial rollout may use one customer and one company database; company access must be explicit and extensible.

## 3 Application packages and licensing assumptions

| **Package** | **Named active app users** | **Module entitlement**   |
|-------------|----------------------------|--------------------------|
| Starter     | 1–5                        | All seven scoped modules |
| Business    | 6–10                       | All seven scoped modules |
| Enterprise  | 11–20                      | All seven scoped modules |

Apply limits per customer subscription across its company databases, counting each active person once. Disabled users do not occupy an app seat; their history remains. Reject activation above the limit. Upgrades preserve data; downgrades require the active count to fit the new limit. Automated billing is optional.

Plan for appropriate SAP B1 indirect-access or existing named-user entitlements for app users. EUR 200–250 per indirect license is a customer-supplied budgeting assumption only, not a verified quotation. Confirm price basis, maintenance, taxes and applicable rights separately. App seat limits do not replace SAP entitlements.

# 4 Shared functional requirements

## Document workspace

- Provide searchable, paginated lists; saved filters; sort; status badges; open/closed/cancelled views; document drill-down; relationship links; attachment upload/download; print and PDF export. Respect action permissions for every operation.

- Document forms support header and line data, business partner, posting/document/due dates, currency, exchange rate, warehouse, unit of measure, prices, line/header discounts, tax, freight, project/cost dimensions, references, remarks and agreed user-defined fields where applicable.

- Support drafts, validation, submit, approval state, posting and permitted close/cancel/reversal actions. Posted documents are not freely editable. Expose only operations allowed for the SAP object and its current state.

- Copy from and copy to must retain SAP base-document and line references. Support partial quantities, multiple eligible base documents and remaining quantities. Revalidate availability and open quantities at posting.

## Business rules and approval behavior

- Use SAP-configured price lists, special prices, payment terms, exchange rates, tax codes and numbering series. Display server-calculated outcomes and SAP errors clearly; never silently override credit, stock or posting-period rules.

- Respect SAP approval requirements and transaction validations. Demonstrate whether Service Layer creates a draft or final document for each approval case. An app approval must not bypass a required SAP approval.

- Prevent stale updates: re-read or use supported concurrency tokens before updates, and require user resolution if SAP data changed. Financial fields use decimal arithmetic with SAP currency and rounding precision.

## Permissions and application operations

Include app-only administration for users, roles, company access, package seats, connector health and audit access. This is necessary infrastructure and is separate from excluded SAP administration screens.

- Roles: sales, purchasing, warehouse, banking, production, approver, report viewer and app administrator. Grant view/create/update/approve/cancel/export separately; restrict company, branch, warehouse and sensitive prices/costs as required.

- Enforce permissions in backend APIs, report queries, exports and attachment access. Hiding a menu is insufficient. Business authorizations must remain valid even if an integration identity has broader SAP permissions.

- Use individual app identities, secure password reset or SSO, session expiry and MFA for privileged users. Record actor, tenant, company, time, action, result, correlation ID and SAP document reference in an append-only audit trail.

## Printing and data exchange

Baseline: one agreed layout per transaction family, company branding, PDF and Excel/CSV reports. Use authorized, validated imports only where separately specified. Reproducing existing Crystal Reports or arbitrary customer layouts requires an inventory and separate estimate.

## User experience and productivity

- **Role-based home:** each role lands on a dashboard with its KPIs (today's sales, open orders, overdue receivables, stock alerts, pending receipts, production in progress) and task tiles that drill into the filtered list.

- **Approvals inbox:** one queue for items awaiting my approval and my submitted requests, with approve/reject, mandatory rejection reason, comments, document preview and approval history. Support out-of-office delegation where SAP approval configuration allows; the app must never approve on behalf of SAP.

- **Global search:** one search box across document numbers, business partners, items, batches and serials, honouring permissions and company scope.

- **Fast data entry:** keyboard-driven line grid, item lookup by code/barcode/description, paste lines from Excel, duplicate a document, recently used customers/items, and per-user defaults for branch, warehouse, sales employee and series.

- **Line context:** show available stock by warehouse, last price to this customer/from this supplier, price list price and (for authorised users) cost and gross margin while entering lines. Warn on selling below cost or below an agreed minimum margin; block only where SAP rules or tenant policy require it.

- **Notifications:** in-app notifications for approvals, rejections, failed or outcome-unknown postings and completed background reports, with optional email. WhatsApp or SMS channels are a separately quoted option.

- **Usability standards:** target WCAG 2.1 AA for core screens; supported browsers are current Chrome, Edge, Firefox and Safari, plus iPad Safari for tablet use. Use the tenant's time zone (EAT by default), date and number formats; store timestamps in UTC.

## Tenant configuration and extensibility

- **Form configuration per tenant:** show, hide, reorder and relabel fields; set mandatory fields and defaults; expose agreed SAP user-defined fields on forms, lists, filters and exports without code changes. Configuration changes are versioned and audited.

- **Customer-specific validations:** surface SAP TransactionNotification and add-on validation messages clearly and in context. Optional app-side pre-validation rules may warn earlier but must never replace the SAP-side rule.

- Feature flags per tenant and package so modules, options and pilot features can be enabled without separate builds.

- **Print templates:** tenant-selectable layouts per document type with logo, footer, bank details and terms; template changes do not require a release.

## Document delivery and communication

- Email documents (quotation, sales order, purchase order, invoice, credit note, BP statement) directly from the app as PDF to selected BP contacts, using the tenant's own SMTP or Microsoft 365 sender. Log each send against the document.

- **Scheduled reports:** authorised users can schedule standard reports (for example daily sales, aging, stock below minimum) to be emailed as PDF/Excel. Scheduled output must apply the recipient's permissions, not the scheduler's.

## Kenya statutory and localization requirements

- **KRA TIMS:** A/R invoices, credit notes and debit notes created from the app must be fiscalised exactly as SAP-originated documents are. Most SAP B1 customers currently fiscalise through a TIMS control-unit integration (typically an SAP B1 TIMS add-on or middleware), so discovery must identify each tenant's existing mechanism and how it is triggered (on posting, by UDF/flag, or by a background service). The app must not offer a path that bypasses fiscalisation.

- **TIMS status and print:** show fiscalisation status (pending, signed, failed) on the document; the final tax-invoice PDF must carry the control-unit invoice number (CUIN), CU serial number, QR code and KRA PINs as returned by the TIMS integration. Block printing or emailing a final tax invoice until it is signed. Credit notes must reference the original fiscalised invoice.

- **TIMS on purchases:** capture the supplier's control-unit invoice number on A/P invoices so input VAT and expenses remain claimable, and report A/P invoices missing it.

- **Native TIMS integration and eTIMS readiness:** if a tenant has no existing TIMS mechanism, a direct control-unit integration is a separately quoted option. Design the fiscalisation step behind a pluggable adapter so a future move to KRA eTIMS (OSCU/VSCU) can be added without reworking document flows.

- **Withholding taxes:** incoming payments must record VAT withholding (2%) by customers appointed as withholding agents, including the certificate number; outgoing payments must apply withholding income tax where configured in SAP withholding-tax codes. Provide withholding reports for reconciliation against KRA iTax certificates.

- **M-Pesa:** treat M-Pesa Paybill/Till as configured payment means mapped to SAP accounts, with transaction code capture on incoming payments. Daraja API integration (C2B confirmation matching, STK push from an invoice) is a separately quoted option.

- **Currencies:** KES local currency with USD/EUR and others as configured. Exchange rates come from SAP; optional automated import of CBK rates into SAP is a separate option.

- **Data protection:** comply with the Kenya Data Protection Act, 2019 — data-processing agreement between Yeasoft and each tenant, ODPC registration as required, documented hosting location, retention and deletion rules for personal data in BP contacts and attachments, and a breach notification procedure within the statutory 72 hours.

# 5 Purchasing and Sales

## Purchasing baseline

- PUR01 Purchase requests and supplier quotations: create, edit permitted drafts, submit, review and generate subsequent documents. Include comparison of supplier quotation price, quantity and delivery date.

- PUR02 Purchase orders: item and service types where supported, delivery dates, warehouse allocation, terms, discounts, taxes, open quantity and partial fulfillment.

- PUR03 Goods receipt PO and goods returns: base-document linking, partial receipts/returns, warehouse/bin allocation and required batch/serial selection.

- PUR04 A/P invoices and credit notes: based and standalone documents where SAP permits, expense/account allocation for service documents, tax/freight and open balances. Include A/P down payment request/invoice and application subject to API validation.

- PUR05 Landed costs: include as a separately identified validation item within purchasing; developer must confirm object coverage, allocation behavior and valuation impact before final estimate.

Purchasing reports: purchase analysis by supplier/item/date; open purchase requests/orders; overdue deliveries; receipts not invoiced; purchase returns/credits; supplier aging and statement links; purchase price history. Define treatment of service lines, credit notes and cancelled documents.

## Sales baseline

- SAL01 Quotations and orders: item and service types where supported, customer pricing, special prices, discounts, credit status, expected delivery dates and stock availability.

- SAL02 Deliveries and returns: partial delivery, eligible multiple base documents, warehouse/bin allocation, batch/serial selection and return references.

- SAL03 A/R invoices and credit notes: based and standalone documents where permitted, taxes, freight, discounts and payment status. Include reserve invoices and A/R down payment request/invoice and application subject to API validation.

- SAL04 Fulfillment: document chain from quotation through payment, open quantity/value and cancellation/closure subject to SAP rules. Picking and packing is a separately quoted option.

Sales reports: sales by customer/item/salesperson/date; gross profit with agreed cost basis and restricted access; open quotations/orders; delivery backlog; delivered not invoiced; returns/credits; customer aging and statement links; collections and sales target comparison only if targets are supplied.

## Required workflow demonstration

Purchasing: PO → partial GRPO → A/P invoice → outgoing payment, plus return and credit. Sales: quotation → order → partial delivery → A/R invoice → incoming payment, plus return and credit. Confirm document links, quantities, taxes, stock and automatic journals directly in SAP for each scenario.

Business configuration stays in SAP. The app selects existing tax codes, accounts, warehouses, periods and series; it does not introduce separate accounting logic.

# 6 Business Partners and Inventory

## Business Partners baseline

- BP01 Create and maintain customers, suppliers and leads with permitted conversion, contact persons, billing/shipping addresses, tax identifiers and duplicate detection.

- BP02 Maintain agreed commercial fields: group, currency, price list, payment terms, sales employee, credit limit, properties and user-defined fields. Restrict changes to sensitive fields; use existing SAP configuration values.

- BP03 Show balances, overdue amounts, open documents, payment history, document drill-down, attachments and notes. Permit inactive/frozen status changes only where authorized and supported.

BP reports: customer/supplier statements; receivable/payable aging; balances and credit utilization; transaction history; master-data completeness. Aging must support a selected as-of date and agreed posting/due-date basis.

## Inventory baseline

- INV01 Item master: create/view/update permitted item data, descriptions, groups, barcodes, units of measure, price-list entries, warehouse data, planning fields and user-defined fields. Reuse existing setup; protect valuation/account-determination changes.

- INV02 Stock inquiry: on-hand, committed, ordered and available quantities by warehouse; bin detail where enabled; batch/serial balances and movement traceability.

- INV03 Inventory transfer requests and transfers: partial fulfillment, source/destination warehouse, bins, batches and serials. Barcode input must work with keyboard-wedge scanners; specialist hardware integration is optional.

- INV04 Inventory goods receipts and issues outside purchasing/production, with reason, allowed account/dimension allocation and audit trail.

- INV05 Inventory counting and inventory posting: count capture, variance review and authorized posting using supported objects. Establish the count cut-off procedure; detect intervening movements.

- INV06 Inventory revaluation: include as a restricted validation item; confirm API availability and SAP valuation-method constraints before commitment. Never implement valuation by direct table updates.

Inventory reports: stock by item/warehouse/bin; availability; movements; batch/serial trace; minimum-stock exceptions; counting variances; slow-moving stock using an agreed inactivity rule; stock valuation as of a selected date.

## Reconciliation requirements

Quantity and valuation reports must handle cancellations, backdated documents, returns, transfers, batches/serials, units of measure and relevant valuation methods. Compare against the corresponding SAP report with identical filters. For inventory-to-GL comparison, use the agreed inventory accounts and explain timing or account-scope differences rather than forcing unmatched totals.

No full historical migration is required: existing SAP records remain accessible through controlled retrieval and report queries. App caches are replaceable and never treated as the authoritative stock balance.

# 7 Banking and Production

## Banking baseline

- BNK01 Incoming payments from customers: select open documents, allocate partial/full payments, handle on-account amounts, approved payment means and supported multi-currency scenarios.

- BNK02 Outgoing supplier payments: select open documents, partial/full allocation, on-account amounts, payment references and authorized payment means.

- BNK03 Payment cancellation/reversal and internal business-partner reconciliation, including credit-note matching and partial reconciliation, where supported by the installed APIs.

- BNK04 View bank/cash account transactions and balances with drill-down to source documents. Deposits, cheque handling and external bank reconciliation must be individually validated and estimated before inclusion in the committed baseline.

Banking reports: receipts/payments register; allocation and unapplied amounts; cash/bank movements and balances; cancelled payments; reconciliation status. Payment execution through banks, bank feeds and automatic bank-file generation are optional integrations, not assumed.

## Production baseline

- PRO01 Bills of materials: view/create/update supported production BOMs, components, quantities, warehouses and resource lines where used. Restrict master changes and preserve SAP rules.

- PRO02 Production orders: create, update permitted fields, plan quantities/dates, release, close or cancel as permitted; include standard, special and disassembly workflows if enabled and API-supported.

- PRO03 Issue for production and receipt from production: partial issues/receipts, component substitutions or additional lines only where SAP allows, manual/backflush issue methods, batch/serial/bin handling and production-order references.

- PRO04 Display planned/issued/received quantities, open components, shortages, actual costs and variance based on SAP records. Handle rejected output, returns and by-products only through verified supported SAP workflows.

Production reports: order status and progress; planned versus actual output; component requirements and shortages; component consumption; production receipts; cost/variance and work in progress using agreed SAP definitions.

## Explicit exclusions and options

Excluded baseline: SAP Administration, configuration wizards, chart-of-accounts maintenance, manual general-ledger journal entry screens, budgets, fixed assets, HR/payroll, service management and CRM opportunity management. Automatic SAP journals arising from scoped transactions remain essential.

Separately quote MRP runs/recommendations, advanced production scheduling, shop-floor terminals, quality management, subcontracting, mobile native apps, desktop packaging, e-commerce/POS, bank integrations, e-invoicing integrations, extra localizations and bespoke add-on functionality. Also separately quote: WhatsApp approvals and notifications; customer self-service portal (statements, invoices, order status); field sales order taking with offline capture; barcode and batch label printing; Excel import of documents and master data; recurring documents and blanket agreements; natural-language assistant and embedded BI dashboards built on Yeasoft's existing products; and Daraja, CBK-rate, native TIMS and future eTIMS connectors listed above.

Existing localization and add-on validations must be identified during discovery. If a scoped transaction depends on one, its required behavior must be implemented or the workflow explicitly blocked pending an agreed solution.

# 8 Architecture and integration contract

## Proposed component structure

Browser or PWA → authenticated application API → SAP connector → private Service Layer → SAP B1 company. Separate app database stores users, subscriptions, permissions, preferences, audit events, posting intents and retry state. Background workers handle refresh and recovery. Suggested stack: React with TypeScript, ASP.NET Core and PostgreSQL or SQL Server; developer may propose an equivalent supported stack.

- Deploy a connector close to the on-premises SAP environment. For cloud hosting, use a private network connection or authenticated outbound connector. Do not expose SAP credentials to browsers or leave Service Layer publicly accessible without a protected gateway.

- All SAP business mutations use supported Service Layer operations. No direct INSERT, UPDATE, DELETE, triggers or schema changes on SAP system tables. If a gap requires DI API, propose it as an explicit separate adapter and obtain scope approval; do not silently substitute direct SQL.

- Read access: prefer business endpoints. Reports may use supported Service Layer SQLQueries or an explicitly approved read-only reporting path with least privilege, parameter validation and tenant restrictions. No arbitrary SQL editor is exposed to users.

## Mandatory API coverage deliverable

For every requirement ID and document operation, provide: SAP object/entity/action; API version; methods; required fields; base-document linkage; UDFs; authorization; approval behavior; localization limits; read/write coverage; and tested patch. Label each as verified, conditional, unsupported or optional. Validate against the installed \$metadata and an isolated test company. Do not infer support from a desktop menu or a newer SAP release.

## Posting and recovery behavior

- Assign a unique application transaction ID and persist a posting intent before submission. Store tenant, company, actor, payload hash, status and result reference. Enforce uniqueness for duplicate clicks and worker retries.

- States: draft, awaiting approval, queued, submitting, posted, failed and outcome unknown. Only show posted after SAP confirmation or successful reconciliation of an uncertain result.

- On timeout after submission, reconcile against a durable approved SAP-side reference where available before retrying. If the outcome cannot be proven, require controlled review. Do not assume Service Layer provides native idempotency for all operations.

- Retry transient failures with bounded backoff; do not retry validation failures blindly. Preserve SAP error detail safely. For multi-document workflows, track each committed step; never imply cross-document atomicity without a tested supported transaction boundary.

## Two way visibility and proposed service targets

App writes submit immediately when connected. SAP-originated changes refresh through supported events where available, otherwise incremental polling with overlap, deduplication and periodic reconciliation. Show last-refreshed time and stale status. Proposed target: ordinary SAP changes visible within 60 seconds under normal load; agree exceptions for large reports. During outages, save drafts or pending requests with explicit status and revalidate before posting.

## Multi-tenant platform operations

- **Operator console for Yeasoft:** create and onboard tenants, register a connector, test Service Layer connectivity, discover company databases, map users and assign packages — with an onboarding checklist that a tenant cannot go live without completing.

- **Connector fleet management:** see connector version, health and last contact for every tenant; push signed connector updates with rollback; alert on connectors that are offline or out of date.

- **Support access:** Yeasoft staff may access tenant data only through time-limited, tenant-approved support sessions that are fully audited. No standing access to customer business data.

- **SAP version compatibility:** publish a support matrix (for example the current and previous feature pack on each supported database). Run the automated regression suite against a customer's target patch before they upgrade SAP, and record any blocked workflows.

- **Service Layer capacity:** document session pooling, session timeouts, SAP licence consumption per session and load-balancer configuration. Size Service Layer for 20 concurrent app users without exhausting SAP sessions or licences.

- Usage metrics per tenant (active users, postings, report runs, failures) to support billing, capacity planning and seat-limit enforcement.

- **Tenant offboarding:** on contract end, export app-owned data, configuration and audit history in an agreed format, then perform and certify deletion within an agreed period. SAP data is untouched.

# 9 Reporting security and quality acceptance

## Reporting contract

- Each report has an owner-approved specification: purpose, columns, filters, date basis, currency, cancellation/credit treatment, rounding, permissions, source objects, refresh interval and SAP comparison report.

- Provide date range/as-of date as applicable, company/branch, warehouse, item/group, BP/group and salesperson filters. Support drill-down, PDF/Excel export, pagination and asynchronous generation for large results. Restrict cost and margin visibility.

- Financial and stock figures must reconcile to agreed SAP reports using the same dataset and filters. Explain any rounding tolerance in advance; unexplained differences fail acceptance. Exported output must match screen filters and authorization.

## Security and operating requirements

- Encrypt transport and secrets; use least-privilege service access, secure session handling, parameterized queries, input validation, attachment scanning/limits and request throttling. Redact credentials and sensitive payloads from logs.

- Isolate every tenant across database access, background jobs, caches, files and reports. Explicitly map app identities to SAP identities/entitlements and document any technical-account strategy with its authorization and audit implications.

- Monitor connectivity, posting latency, failed/unknown transactions, refresh lag, queue depth and authentication failures. Include an authorized retry/reconciliation console, alerting and a documented incident procedure.

- Maintain separate development, UAT and production environments. Use scrubbed data outside production. Back up app data and configuration; coordinate SAP backups and test restoration. Proposed recovery targets for app-owned data: RPO 24 hours and RTO 4 hours, subject to hosting agreement.

- **Secure development:** static analysis and dependency vulnerability scanning in the build pipeline; secrets scanning on every commit; independent penetration test against OWASP ASVS Level 2 before the first production tenant and at least annually, with critical and high findings fixed before go-live.

## Acceptance tests required before release

| **Test**                 | **Pass condition**                                                                           |
|--------------------------|----------------------------------------------------------------------------------------------|
| Transaction chains       | Partial and full purchase/sales flows match SAP stock, balances, tax and document links.     |
| Error and retry          | Double click, expired session and timeout after commit do not create duplicate transactions. |
| Approvals and access     | Required approvals hold; unauthorized writes, exports and cross-tenant reads are denied.     |
| Inventory and production | Bins, batches, serials, backflush, partial output and counting changes match SAP behavior.   |
| Payments and reporting   | Partial/on-account payments reconcile; module reports match agreed SAP results.              |
| Concurrency and outage   | Stale updates are detected; pending work remains explicit and recoverable after outage.      |
| Seats and load           | Seat limits hold at 5, 10 and 20; test 20 concurrent users with representative records.      |

Proposed performance targets under agreed infrastructure and dataset: p95 ordinary list/search ≤3 seconds, ordinary posting ≤5 seconds excluding approval waiting, standard report ≤10 seconds. Larger exports run in the background. Record actual measurements and SAP/network bottlenecks; obtain agreement on exceptions.

# 10 Delivery plan and developer quotation

## Milestones and exit criteria

| **Milestone**               | **Deliverable and exit criterion**                                                                                                                                   |
|-----------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 1 Discovery and feasibility | Confirmed environment, workflow inventory, API coverage matrix, sample reports and risk list; demonstrate login, draft/approval, posting and timeout reconciliation. |
| 2 Foundation                | Tenant/company isolation, identity, roles, subscriptions, connector, audit and retry state; security and seat-limit tests pass.                                      |
| 3 Operational core          | BP, Sales, Purchasing and Inventory with their reports; end-to-end SAP reconciliation passes.                                                                        |
| 4 Banking and Production    | Agreed payment/reconciliation and production workflows with reports; UAT scenarios pass.                                                                             |
| 5 Pilot and handover        | Load/security review, restore test, user training, signed UAT and controlled production rollout.                                                                     |

## Release management and support model

- Automated CI/CD with versioned releases, release notes and database migrations that run forward safely. Roll out to a pilot tenant first, then remaining tenants; keep a documented rollback for every release.

- Agreed maintenance windows communicated to tenants in advance; target availability for app-owned services of 99.5% monthly, excluding customer SAP, network and approved maintenance.

- **Support tiers by package:** first-line support by Yeasoft, second-line by the development team, with defined response and resolution targets for critical, high, medium and low issues, and a defect warranty period after go-live.

## Required handover materials

- Complete source code in a client-controlled repository; build/deployment scripts; environment configuration guide; dependency/license inventory; database migrations; documented application API and SAP mappings.

- Automated integration and regression tests, UAT scripts/results, report definitions, role matrix, monitoring setup, backup/restore procedure and operations runbook.

- User guide, administrator/connector guide, training session, release notes, known limitations and agreed defect warranty/support terms. Production secrets must be transferred securely, never committed to source.

## Developer response requested

Return an itemized estimate by module and milestone, separating baseline features, conditional API work and optional enhancements. Include effort, team roles, calendar schedule, dependencies, assumptions, recurring hosting costs, third-party charges and post-launch support. State which acceptance criteria are included and identify every exception.

Do not mark a module complete only because its main document can be created. Completion includes permitted lifecycle actions, validations, permissions, failure handling, printing, reports, reconciliation and approved UAT evidence.

## Decisions to close during discovery

Confirm company count and localization; exact SAP patch; employee roles and license mapping; enabled bins/batches/serials; approval templates; currencies and branches; required UDFs/add-ons; bank methods; production order/BOM types; report samples; historic data volumes; hosting location; and named owners for UAT. Any proposed reduction or expansion must be recorded in the agreed baseline. Also confirm: each tenant's TIMS integration (add-on or middleware), control-unit type and trigger method; whether customers are VAT withholding agents; withholding-tax codes in use; M-Pesa Paybill/Till accounts; required email sender; dashboard KPIs per role; notification channels; form fields and UDFs to expose per role; print layouts per document type; and target database (SQL Server or HANA) per pilot customer.

## SAP reference material

Reference documentation supports technical discovery; actual behavior must be tested on the target release. These links are not a guarantee that every scoped operation is exposed by Service Layer.

SAP Business One Service Layer guide  
https://help.sap.com/doc/fc2f5477516c404c8bf9ad1315a17238/10.0/en-US/Working_with_SAP_Business_One_Service_Layer.pdf

SAP Business One 10 License Guide  
https://help.sap.com/doc/14c6d92982fd4a9dbf7df66b5108433b/10.0/en-US/LicenseGuide.pdf
