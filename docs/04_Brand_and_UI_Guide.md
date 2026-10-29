# YeaFlow — Brand and UI Guide

## Name and wording
- Product: **YeaFlow** — "A Yeasoft product".
- Tagline: *Your SAP Business One operations — sales, purchasing, stock, banking and production — in one fast web app.*
- Descriptor: "Works with SAP Business One". Never use the SAP logo; never call Yeasoft an SAP partner.

## Logo
- The mark: two strokes merge into one stem, forming a **Y** — the team in the app and SAP Business One flowing into one source of truth.
- Left stroke and stem in teal, right stroke in amber. On dark backgrounds use bright teal #2DD4BF.
- Files in `3_Brand_Assets/`: `yeaflow-mark.svg`, `yeaflow-logo.svg`, `yeaflow-logo-dark.svg`, `yeaflow-logo-mono.svg`, `yeaflow-app-icon.svg`, `favicon.svg`.
- Wordmark: "Yea" in Ink, "Flow" in Flow Teal, Manrope ExtraBold. The SVG logos use live text, so convert text to outlines in a design tool before sending to printers.
- Minimum size: mark 16 px (favicon uses thicker strokes); keep clear space equal to the mark's stroke width × 2.

## Colour tokens

| Token | Hex | Use |
|---|---|---|
| `--yf-primary` | #0F766E | Primary buttons, links, active states |
| `--yf-primary-hover` | #0B5A54 | Hover/pressed |
| `--yf-primary-bright` | #2DD4BF | Teal on dark backgrounds |
| `--yf-ink` | #0B1F2A | Text, sidebar, app icon |
| `--yf-accent` | #F59E0B | Logo accent, badges, highlights (not for text on white) |
| `--yf-accent-text` | #92400E | Amber text on light backgrounds |
| `--yf-bg` | #F6F8F9 | App background |
| `--yf-surface` | #FFFFFF | Cards, forms, tables |
| `--yf-border` | #DDE3E7 | Borders, dividers |
| `--yf-muted` | #5A6B76 | Secondary text, labels |

## Document status chips (map to posting states)

| Status | Background | Text |
|---|---|---|
| Draft | #EEF2F4 | #3B4F5C |
| Awaiting approval | #FEF3C7 | #92400E |
| Queued / Submitting | #DBEAFE | #1E40AF |
| Posted | #CCFBF1 | #115E59 |
| Failed | #FEE2E2 | #991B1B |
| Outcome unknown | #EDE9FE | #5B21B6 |
| TIMS signed | #0B1F2A | #FFFFFF |

## Typography
- **Manrope** (700/800): headings, logo, KPI numbers.
- **Inter** (400/500/600): body, forms, tables. Base size 14 px; labels 12–13 px.
- **JetBrains Mono** (500): document numbers, amounts, item codes; right-align numeric columns.

## UI patterns
- Layout: dark Ink sidebar with modules (Home, Sales, Purchasing, Business Partners, Inventory, Banking, Production, Reports, Approvals); top bar with global search, company/branch switcher and account.
- Document screen: header fields card → lines grid → totals card and document-flow panel on the right → approval card.
- Line warnings (low stock, below-cost price) appear inline under the grid in amber, with a one-click fix where possible.
- Radius: 10–12 px controls, 16 px cards, pill chips. Touch targets ≥ 44 px. Text contrast ≥ 4.5:1.
- Light theme first; dark theme optional later using the same tokens.
