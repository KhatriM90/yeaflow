import { useMemo, useState } from 'react';
import { DocumentLinesGrid, type DocumentLineRow } from '../components/DocumentLinesGrid';
import { PostToSap } from '../components/PostToSap';
import { StatusChip } from '../components/StatusChip';
import { useSession } from '../app/session';
import { buildSalesOrderPayload } from '../lib/postings';
import { add, applyDiscount, formatDecimal, isDecimal, multiply, parseDecimal, type Decimal } from '../lib/decimal';

const sampleLines: DocumentLineRow[] = [
  { lineNum: 1, itemCode: 'A1001', description: 'Maize flour 2kg', quantity: '24', unitPrice: '185.50', discountPercent: '0', warehouseCode: 'NBO01', taxCode: 'VAT16' },
  { lineNum: 2, itemCode: 'A2040', description: 'Cooking oil 1L', quantity: '12', unitPrice: '329.99', discountPercent: '2.5', warehouseCode: 'NBO01', taxCode: 'VAT16' },
];

/**
 * Layout preview of the document screen (04_Brand_and_UI_Guide, Document screen): header card → lines grid →
 * totals and document flow → approval. Sales documents (SAL01–SAL04) fill this in with live SAP data.
 * In the local demo (signed in through the API) the order can be posted to simulated SAP; the customer code steers the
 * simulated outcome (REJECT…, LOST…, DOWN…, anything else posts).
 */
export function SalesOrderPreview() {
  const { session } = useSession();
  const [lines, setLines] = useState(sampleLines);
  const [cardCode, setCardCode] = useState('C00042');
  const demo = session?.mode === 'demo';
  const subtotal = useMemo(
    () =>
      lines.reduce<Decimal>((sum, l) => {
        if (![l.quantity, l.unitPrice, l.discountPercent || '0'].every(isDecimal)) return sum;
        const gross = multiply(parseDecimal(l.quantity), parseDecimal(l.unitPrice));
        return add(sum, applyDiscount(gross, parseDecimal(l.discountPercent || '0')));
      }, parseDecimal('0')),
    [lines],
  );

  return (
    <>
      <header className="yf-page-header yf-page-header--row">
        <div>
          <h1>New sales order</h1>
          <p className="yf-muted">
            {demo
              ? 'Local demo: posts to simulated SAP through the connector. Try customer REJECT1, LOST1 or DOWN1 to see other outcomes.'
              : 'Preview with sample data — prices, tax and totals will come from SAP.'}
          </p>
        </div>
        <StatusChip status="Draft" />
      </header>

      <div className="yf-document">
        <div className="yf-document__main">
          <section className="yf-card yf-section" aria-label="Header">
            <dl className="yf-fields">
              <div>
                <dt>{demo ? <label htmlFor="card-code">Customer code</label> : 'Customer'}</dt>
                <dd>
                  {demo ? (
                    <input id="card-code" className="yf-input" value={cardCode} onChange={(e) => setCardCode(e.target.value)} />
                  ) : (
                    'C00042 · Mama Mboga Wholesalers'
                  )}
                </dd>
              </div>
              <div>
                <dt>Posting date</dt>
                <dd>29 Oct 2026</dd>
              </div>
              <div>
                <dt>Delivery date</dt>
                <dd>31 Oct 2026</dd>
              </div>
              <div>
                <dt>Currency</dt>
                <dd>KES</dd>
              </div>
            </dl>
          </section>
          <section aria-label="Lines">
            <DocumentLinesGrid rows={lines} onChange={setLines} />
          </section>
        </div>
        <aside className="yf-document__side">
          <section className="yf-card yf-section" aria-label="Totals">
            <h2>Totals</h2>
            <dl className="yf-totals">
              <div>
                <dt>Before tax</dt>
                <dd className="yf-num">{formatDecimal(subtotal)}</dd>
              </div>
              <div>
                <dt>Tax</dt>
                <dd className="yf-muted">from SAP</dd>
              </div>
            </dl>
          </section>
          {demo && session && (
            <PostToSap
              companyId={session.companyId}
              sapObject="Orders"
              buildPayload={() => buildSalesOrderPayload(cardCode, dueDate(), lines)}
              canReconcile={session.permissions.includes('Postings.Intent.Reconcile')}
            />
          )}
          <section className="yf-card yf-section" aria-label="Document flow">
            <h2>Document flow</h2>
            <ol className="yf-flow">
              <li>Quotation</li>
              <li className="is-current">Order</li>
              <li>Delivery</li>
              <li>A/R invoice · TIMS</li>
              <li>Incoming payment</li>
            </ol>
          </section>
        </aside>
      </div>
    </>
  );
}

/** Two days from today, as SAP's yyyy-MM-dd. Real documents take the date from the form (SAL02). */
function dueDate(): string {
  return new Date(Date.now() + 2 * 86_400_000).toISOString().slice(0, 10);
}
