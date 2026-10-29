export function ModulePlaceholder({ title, ids }: { title: string; ids: string }) {
  return (
    <>
      <header className="yf-page-header">
        <h1>{title}</h1>
      </header>
      <section className="yf-card yf-section">
        <p>
          This module is next on the delivery plan ({ids}). Lists, document screens, approvals and reports will appear here.
        </p>
      </section>
    </>
  );
}
