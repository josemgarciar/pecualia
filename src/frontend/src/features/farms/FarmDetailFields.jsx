export function DetailField({ label, value, fullWidth = false }) {
  return (
    <div className={fullWidth ? 'detail-full' : undefined}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

export function SummaryMetric({ label, value, tone = 'default' }) {
  return (
    <article className={`farm-detail-metric-card${tone === 'success' ? ' farm-detail-metric-card-success' : ''}`}>
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  );
}
