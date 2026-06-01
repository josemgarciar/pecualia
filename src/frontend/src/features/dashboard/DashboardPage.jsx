import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Area,
  AreaChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis
} from 'recharts';
import {
  AlertTriangle,
  ArrowLeftRight,
  Building2,
  ChevronRight,
  ClipboardCheck,
  FileCheck2,
  Syringe,
  TrendingUp,
  Users
} from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { useAuth } from '../../shared/auth/AuthContext';
import { getPlanLabel } from '../../shared/subscription/plans';

const toneMap = {
  success: { bg: '#DDEBDF', color: '#2F6B4F' },
  info: { bg: '#DBEAFE', color: '#2563EB' },
  warning: { bg: '#FEF3C7', color: '#D97706' },
  danger: { bg: '#FEE2E2', color: '#DC2626' },
  violet: { bg: '#EDE9FE', color: '#6366F1' }
};

export function DashboardPage() {
  const { user } = useAuth();
  const [summary, setSummary] = useState(null);
  const [error, setError] = useState('');
  const planLabel = getPlanLabel(user);

  useEffect(() => {
    apiRequest('/api/dashboard/summary')
      .then(setSummary)
      .catch((requestError) => setError(requestError.message));
  }, []);

  const metrics = useMemo(() => {
    const baseMetrics = [];

    if (user?.role === 'Manager') {
      baseMetrics.push(
        { label: 'Ganaderos gestionados', value: summary?.managedFarmers ?? '—', icon: Users, tone: 'success' },
        { label: 'Explotaciones', value: summary?.activeFarms ?? '—', icon: Building2, tone: 'success' },
        { label: 'Movimientos este mes', value: summary?.movementsThisMonth ?? '—', icon: ArrowLeftRight, tone: 'info' },
        { label: 'Activaciones pendientes', value: summary?.pendingActivations ?? '—', icon: ClipboardCheck, tone: 'violet' }
      );
    } else {
      baseMetrics.push(
        { label: 'Explotaciones', value: summary?.farms ?? '—', icon: Building2, tone: 'success' },
        { label: 'Explotaciones gestionadas', value: summary?.activeFarms ?? '—', icon: Building2, tone: 'success' },
        { label: 'Animales registrados', value: summary?.totalAnimals ?? '—', icon: Users, tone: 'info' },
        { label: 'Actuaciones próximas', value: summary?.upcomingActions ?? '—', icon: Syringe, tone: 'violet' }
      );
    }

    return baseMetrics;
  }, [summary, user]);

  const chartData = summary?.monthlyActivity ?? [];
  const pendingTasks = summary?.pendingTasks ?? [];
  const trendLabel = useMemo(() => {
    if (summary?.monthlyTrendPercentage == null) {
      return 'Sin base comparativa';
    }

    if (summary.monthlyTrendPercentage === 0) {
      return 'Sin variación';
    }

    const sign = summary.monthlyTrendPercentage > 0 ? '+' : '';
    return `${sign}${summary.monthlyTrendPercentage}% vs mes anterior`;
  }, [summary]);

  const quickActions = user?.role === 'Manager'
    ? [
        { label: 'Nuevo Ganader@', icon: Users, to: '/app/farmers', state: { openCreateModal: true } },
        { label: 'Nueva explotación', icon: Building2, to: '/app/farms', state: { openCreateModal: true } },
      ]
    : [
        { label: 'Nueva explotación', icon: Building2, to: '/app/farms', state: { openCreateModal: true } },
        { label: 'Mi cuenta', icon: ClipboardCheck, to: '/app/profile' },
      ];

  return (
    <div className="dashboard-page">
      <header className="dashboard-header">
        <div>
          <h1>Buenos días, {user?.name}</h1>
          <div className="dashboard-subheader">
            <span className="dashboard-role-chip">{user?.role === 'Manager' ? 'Gestor@ Profesional' : 'Ganader@'}</span>
            <span>{user?.role === 'Manager' ? `Plan ${planLabel} activo` : `Plan ${planLabel} activo`}</span>
          </div>
        </div>
      </header>

      {error && <div className="error-banner">{error}</div>}

      <section className="dashboard-metrics">
        {metrics.map((metric) => {
          const Icon = metric.icon;
          const tone = toneMap[metric.tone];

          return (
            <article className="dashboard-metric-card" key={metric.label}>
              <div className="dashboard-metric-top">
                <div className="dashboard-metric-icon" style={{ background: tone.bg, color: tone.color }}>
                  <Icon size={18} />
                </div>
              </div>
              <div>
                <strong>{metric.value}</strong>
                <span>{metric.label}</span>
              </div>
            </article>
          );
        })}
      </section>

      <section className="dashboard-grid">
        <article className="dashboard-card dashboard-card-chart">
          <div className="dashboard-card-header">
            <div>
              <h2>Actividad mensual</h2>
              <p>Altas, bajas, nacimientos y movimientos</p>
            </div>
            <div className="dashboard-trend-pill">
              <TrendingUp size={14} />
              <span>{trendLabel}</span>
            </div>
          </div>

          <div className="dashboard-chart">
            <ResponsiveContainer width="100%" height={240}>
              <AreaChart data={chartData}>
                <defs>
                  <linearGradient id="colorRegistrations" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#2F6B4F" stopOpacity={0.15} />
                    <stop offset="95%" stopColor="#2F6B4F" stopOpacity={0} />
                  </linearGradient>
                  <linearGradient id="colorDischarges" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#DC2626" stopOpacity={0.1} />
                    <stop offset="95%" stopColor="#DC2626" stopOpacity={0} />
                  </linearGradient>
                  <linearGradient id="colorBirths" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#D97706" stopOpacity={0.12} />
                    <stop offset="95%" stopColor="#D97706" stopOpacity={0} />
                  </linearGradient>
                  <linearGradient id="colorMovements" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#2563EB" stopOpacity={0.12} />
                    <stop offset="95%" stopColor="#2563EB" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="#F0F2F0" />
                <XAxis dataKey="monthLabel" tick={{ fill: '#637168', fontSize: 12 }} axisLine={false} tickLine={false} />
                <YAxis tick={{ fill: '#637168', fontSize: 12 }} axisLine={false} tickLine={false} />
                <Tooltip
                  contentStyle={{
                    borderRadius: 12,
                    border: '1px solid #D7DED8',
                    background: '#FFFFFF',
                    color: '#1E2A24',
                    fontSize: 12
                  }}
                />
                <Area type="monotone" dataKey="registrations" name="Altas" stroke="#2F6B4F" strokeWidth={2} fill="url(#colorRegistrations)" />
                <Area type="monotone" dataKey="discharges" name="Bajas" stroke="#DC2626" strokeWidth={2} fill="url(#colorDischarges)" />
                <Area type="monotone" dataKey="births" name="Nacimientos" stroke="#D97706" strokeWidth={2} fill="url(#colorBirths)" />
                <Area type="monotone" dataKey="movements" name="Movimientos" stroke="#2563EB" strokeWidth={2} fill="url(#colorMovements)" />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </article>

        <article className="dashboard-card">
          <div className="dashboard-card-header">
            <div>
              <h2>Acciones rápidas</h2>
              <p>Accesos directos al trabajo más frecuente</p>
            </div>
          </div>

          <div className="quick-actions-grid">
            {quickActions.map((action) => {
              const Icon = action.icon;

              return (
                <Link className="quick-action-card" key={action.label} to={action.to} state={action.state}>
                  <div className="quick-action-icon">
                    <Icon size={16} />
                  </div>
                  <span>{action.label}</span>
                </Link>
              );
            })}
          </div>
        </article>
      </section>

      <article className="dashboard-card">
        <div className="dashboard-card-header">
          <div>
            <h2>Tareas pendientes</h2>
            <p>Alertas operativas y revisiones próximas desde datos reales</p>
          </div>
          <span className="pending-chip">{pendingTasks.length} pendientes</span>
        </div>

        <div className="pending-list">
          {pendingTasks.length === 0 && (
            <div className="pending-item">
              <div className="pending-item-icon" style={{ background: '#F0F2F0', color: '#637168' }}>
                <ClipboardCheck size={16} />
              </div>
              <div className="pending-item-copy">
                <strong>Sin actuaciones pendientes</strong>
                <span>No hay vacunas, inspecciones o guías pendientes dentro del margen operativo.</span>
              </div>
            </div>
          )}

          {pendingTasks.map((task) => {
            const Icon = task.kind === 'MovementConfirmation'
              ? FileCheck2
              : task.kind === 'PorcineTransition'
                ? AlertTriangle
              : task.kind === 'Inspection'
                ? ClipboardCheck
                : Syringe;
            const tone = toneMap[task.tone];

            return (
              <div className="pending-item" key={`${task.title}-${task.dueDate}`}>
                <div className="pending-item-icon" style={{ background: tone.bg, color: tone.color }}>
                  <Icon size={16} />
                </div>
                <div className="pending-item-copy">
                  <strong>{task.title}</strong>
                  <span>{task.detail}</span>
                </div>
                <button className="pending-item-action" type="button">
                  <ChevronRight size={16} />
                </button>
              </div>
            );
          })}
        </div>
      </article>
    </div>
  );
}
