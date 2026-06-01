import { useEffect, useMemo, useState } from 'react';
import { TrendingDown, TrendingUp } from 'lucide-react';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { apiRequest } from '../../shared/api/client';
import { currentYear, monthLabels, speciesToneMap } from './FarmDetailShared';

export function FarmCensusBalancesSection({ farm }) {
  const [activeSubTab, setActiveSubTab] = useState('census');
  const [year, setYear] = useState(currentYear);
  const [census, setCensus] = useState(null);
  const [balance, setBalance] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    loadData(year);
  }, [farm.id, year]);

  async function loadData(targetYear = year) {
    setLoading(true);
    setError('');

    try {
      const [censusResponse, balanceResponse] = await Promise.all([
        apiRequest(`/api/farms/${farm.id}/census?year=${targetYear}`),
        apiRequest(`/api/farms/${farm.id}/balances?year=${targetYear}`)
      ]);
      setCensus(censusResponse);
      setBalance(balanceResponse);
    } catch (requestError) {
      setError(requestError.message);
      setCensus(null);
      setBalance(null);
    } finally {
      setLoading(false);
    }
  }

  const yearOptions = useMemo(
    () => Array.from(new Set([currentYear, currentYear - 1, currentYear - 2, ...(census?.availableYears ?? [])])).sort((a, b) => b - a),
    [census?.availableYears]
  );

  if (loading) {
    return <div className="panel-card empty-state">Cargando censos y balances...</div>;
  }

  const isPorcine = farm.livestockSpecies === 'Porcine';
  const total = census?.total ?? 0;
  const censusCards = isPorcine
    ? [
        { label: 'Verracos', value: census?.boars ?? 0, color: '#1d4ed8', bg: '#dbeafe' },
        { label: 'Cerdas vida', value: census?.sowsForLive ?? 0, color: '#be185d', bg: '#fce7f3' },
        { label: 'Hembras reposición', value: census?.sowsReposition ?? 0, color: '#d97706', bg: '#fef3c7' },
        { label: 'Machos reposición', value: census?.malesReposition ?? 0, color: '#2563eb', bg: '#dbeafe' },
        { label: 'Lechones', value: census?.piglets ?? 0, color: '#7c3aed', bg: '#ede9fe' },
        { label: 'Recría', value: census?.rears ?? 0, color: '#0f766e', bg: '#ccfbf1' },
        { label: 'Cebo', value: census?.baits ?? 0, color: '#9d174d', bg: '#fce7f3' },
        { label: 'Pendientes reclasificación', value: census?.pendingPorcineTransitions ?? 0, color: '#b45309', bg: '#ffedd5' }
      ]
    : [
        { label: 'Reproductores macho', value: census?.reproductiveMales ?? 0, color: '#1d4ed8', bg: '#dbeafe' },
        { label: 'Reproductores hembra', value: census?.reproductiveFemales ?? 0, color: '#be185d', bg: '#fce7f3' },
        { label: 'Menores de 4 meses', value: census?.nonReproductiveUnder4Months ?? 0, color: '#d97706', bg: '#fef3c7' },
        { label: 'De 4 a 12 meses', value: census?.nonReproductiveBetween4And12Months ?? 0, color: '#7c3aed', bg: '#ede9fe' }
      ];
  const safeDivisor = total || 1;
  const censusCardsWithPct = censusCards.map((card) => ({
    ...card,
    pct: Math.round((card.value / safeDivisor) * 100)
  }));

  const balanceRegistrations = balance?.registrations ?? 0;
  const balanceBirths = balance?.births ?? 0;
  const balanceDeaths = balance?.deaths ?? 0;
  const balanceDepartures = balance?.departures ?? 0;
  const balanceMovementEntries = balance?.movementEntries ?? 0;
  const balanceMovementDepartures = balance?.movementDepartures ?? 0;
  const balanceNet = balance?.balance ?? 0;
  const balanceMetrics = [
    { label: 'Altas', value: `+${balanceRegistrations}`, color: '#2F6B4F', bg: '#DDEBDF' },
    { label: 'Bajas', value: `-${balanceDeaths}`, color: '#dc2626', bg: '#fee2e2' },
    { label: 'Nacimientos', value: `+${balanceBirths}`, color: '#d97706', bg: '#fef3c7' },
    { label: 'Mov. entrada', value: `+${balanceMovementEntries}`, color: '#1d4ed8', bg: '#dbeafe' },
    { label: 'Mov. salida', value: `-${balanceMovementDepartures}`, color: '#f97316', bg: '#ffedd5' },
    { label: 'Balance', value: balanceNet >= 0 ? `+${balanceNet}` : `${balanceNet}`, color: balanceNet >= 0 ? '#2F6B4F' : '#dc2626', bg: balanceNet >= 0 ? '#DDEBDF' : '#fee2e2' }
  ];
  const chartData = (balance?.months ?? []).map((month) => ({
    mes: monthLabels[month.month - 1],
    altas: month.registrations ?? 0,
    bajas: month.deaths ?? 0,
    nacimientos: month.births ?? 0
  }));

  return (
    <section className="panel-card stack">
      <div className="section-heading-row">
        <div>
          <h2>Censos y balances</h2>
          <p>Censo calculado automáticamente a partir de nacimientos, autoreposiciones, guías y muertes.</p>
        </div>
        <select value={year} onChange={(event) => setYear(Number(event.target.value))}>
          {yearOptions.map((availableYear) => (
            <option key={availableYear} value={availableYear}>{availableYear}</option>
          ))}
        </select>
      </div>

      {error && <div className="error-banner">{error}</div>}

      <div className="census-subtab-row">
        <button type="button" className={activeSubTab === 'census' ? 'census-subtab-active' : ''} onClick={() => setActiveSubTab('census')}>Censos</button>
        <button type="button" className={activeSubTab === 'balances' ? 'census-subtab-active' : ''} onClick={() => setActiveSubTab('balances')}>Balances</button>
      </div>

      {activeSubTab === 'census' && (
        <div className="census-visual-card">
          <div className="census-visual-header">
            <div>
              <h3 className="census-visual-title">Censo actual</h3>
              <p className="census-visual-subtitle">Año: {year} · {speciesToneMap[farm.livestockSpecies]?.label ?? farm.livestockSpecies}</p>
            </div>
            <div className="census-visual-total">
              <span className="census-visual-total-label">TOTAL ANIMALES</span>
              <span className="census-visual-total-value">{total}</span>
            </div>
          </div>

          <div className="census-visual-section">
            <span className="census-visual-section-label">{isPorcine ? 'TIPOS DE ANIMAL' : 'DISTRIBUCIÓN DEL CENSO'}</span>
            <div className="census-visual-categories">
              {censusCardsWithPct.map((category) => (
                <div key={category.label} className="census-category-card" style={{ background: category.bg }}>
                  <div className="census-category-top">
                    <span className="census-category-value" style={{ color: category.color }}>{category.value}</span>
                    <span className="census-category-pct" style={{ color: category.color }}>{category.pct}%</span>
                  </div>
                  <span className="census-category-label" style={{ color: category.color }}>{category.label}</span>
                  <div className="census-category-bar" style={{ background: `${category.color}30` }}>
                    <div className="census-category-bar-fill" style={{ width: `${category.pct}%`, background: category.color }} />
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="census-distribution">
            <span className="census-visual-section-label">DISTRIBUCIÓN VISUAL</span>
            <div className="census-distribution-bar">
              {censusCardsWithPct.map((category) => (
                <div key={category.label} style={{ width: `${category.pct}%`, background: category.color }} />
              ))}
            </div>
            <div className="census-distribution-legend">
              {censusCardsWithPct.map((category) => (
                <div key={category.label} className="census-legend-item">
                  <span className="census-legend-dot" style={{ background: category.color }} />
                  <span>{category.label}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {activeSubTab === 'balances' && balance && (
        <div className="stack">
          <div className="census-visual-card">
            <h3 className="census-visual-title">Actividad mensual</h3>
            <div className="census-chart-wrapper">
              <ResponsiveContainer width="100%" height={200}>
                <BarChart data={chartData}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#F0F2F0" />
                  <XAxis dataKey="mes" tick={{ fill: '#637168', fontSize: 12 }} axisLine={false} tickLine={false} />
                  <YAxis tick={{ fill: '#637168', fontSize: 12 }} axisLine={false} tickLine={false} />
                  <Tooltip contentStyle={{ borderRadius: 8, border: '1px solid #D7DED8', fontSize: 12 }} />
                  <Bar dataKey="altas" name="Altas" fill="#2F6B4F" radius={[4, 4, 0, 0]} />
                  <Bar dataKey="bajas" name="Bajas" fill="#dc2626" radius={[4, 4, 0, 0]} />
                  <Bar dataKey="nacimientos" name="Nacimientos" fill="#E7B84C" radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>

          <div className="census-visual-card">
            <div className="balance-card-header">
              <h3 className="census-visual-title">Balance {monthLabels[new Date().getMonth()]} {year}</h3>
            </div>
            <div className="balance-metrics-grid">
              {balanceMetrics.map((item) => (
                <div key={item.label} className="balance-metric-tile" style={{ background: item.bg }}>
                  <span className="balance-metric-value" style={{ color: item.color }}>{item.value}</span>
                  <span className="balance-metric-label" style={{ color: item.color }}>{item.label}</span>
                </div>
              ))}
            </div>
            <div className="balance-trend-row">
              {balanceNet >= 0 ? <TrendingUp size={16} className="balance-trend-icon-positive" /> : <TrendingDown size={16} className="balance-trend-icon-negative" />}
              <span className={balanceNet >= 0 ? 'balance-trend-text-positive' : 'balance-trend-text-negative'}>
                Balance {balanceNet >= 0 ? 'positivo' : 'negativo'} este mes
              </span>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
