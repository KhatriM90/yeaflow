/** YeaFlow mark: two strokes merge into one stem (04_Brand_and_UI_Guide, Logo). */
export function Mark({ size = 28, onDark = false }: { size?: number; onDark?: boolean }) {
  const teal = onDark ? 'var(--yf-primary-bright)' : 'var(--yf-primary)';
  return (
    <svg width={size} height={size} viewBox="0 0 64 64" aria-hidden="true" fill="none" strokeWidth={8} strokeLinecap="round">
      <path d="M50 10 C50 26, 32 26, 32 38" stroke="var(--yf-accent)" />
      <path d="M14 10 C14 26, 32 26, 32 38" stroke={teal} />
      <path d="M32 38 L32 55" stroke={teal} />
    </svg>
  );
}

export function Logo({ onDark = false }: { onDark?: boolean }) {
  return (
    <span className={`yf-logo${onDark ? ' yf-logo--dark' : ''}`} aria-label="YeaFlow">
      <Mark onDark={onDark} />
      <span className="yf-logo__word" aria-hidden="true">
        <span className="yf-logo__yea">Yea</span>
        <span className="yf-logo__flow">Flow</span>
      </span>
    </span>
  );
}
