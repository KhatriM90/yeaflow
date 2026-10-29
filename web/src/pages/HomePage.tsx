import { Link } from 'react-router-dom';
import { StatusChip } from '../components/StatusChip';
import type { PostingStatus } from '../lib/postingStatus';
import { useSession } from '../app/session';

/**
 * Role-based home (scope section 4, "Role-based home"). KPIs per role are open question Q-008, so tiles show
 * placeholders until the KPI list is agreed; each tile will drill into its filtered list.
 */
const tiles = [
  { label: "Today's sales", value: '—', to: '/sales' },
  { label: 'Open orders', value: '—', to: '/sales' },
  { label: 'Overdue receivables', value: '—', to: '/business-partners' },
  { label: 'Stock alerts', value: '—', to: '/inventory' },
  { label: 'Pending receipts', value: '—', to: '/purchasing' },
  { label: 'Production in progress', value: '—', to: '/production' },
];

const statusLegend: PostingStatus[] = ['Draft', 'AwaitingApproval', 'Queued', 'Posted', 'Failed', 'OutcomeUnknown', 'TimsSigned'];

export function HomePage() {
  const { session } = useSession();

  return (
    <>
      <header className="yf-page-header">
        <h1>Karibu, {session?.displayName}</h1>
        <p className="yf-muted">Your tasks and numbers for today.</p>
      </header>

      <section className="yf-tiles" aria-label="Key figures">
        {tiles.map((t) => (
          <Link key={t.label} to={t.to} className="yf-card yf-tile">
            <span className="yf-tile__label">{t.label}</span>
            <span className="yf-tile__value">{t.value}</span>
          </Link>
        ))}
      </section>

      <section className="yf-card yf-section" aria-labelledby="status-title">
        <h2 id="status-title">Document status</h2>
        <p className="yf-muted">Every document shows where it stands. Only SAP-confirmed documents are ever shown as posted.</p>
        <div className="yf-chip-row">
          {statusLegend.map((s) => (
            <StatusChip key={s} status={s} />
          ))}
        </div>
      </section>
    </>
  );
}
