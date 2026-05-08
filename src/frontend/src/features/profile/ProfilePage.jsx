import { useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Building2,
  CheckCircle2,
  CreditCard,
  Mail,
  Settings,
  Shield,
  UserCircle2
} from 'lucide-react';
import { useAuth } from '../../shared/auth/AuthContext';
import { getPlanLabel } from '../../shared/subscription/plans';

export function ProfilePage() {
  const { user } = useAuth();
  const navigate = useNavigate();

  const initials = useMemo(
    () => `${user?.name?.[0] ?? ''}${user?.surname?.[0] ?? ''}`.toUpperCase() || 'P',
    [user]
  );
  const roleLabel = user?.role === 'Manager' ? 'Gestor@' : 'Ganader@';
  const planLabel = getPlanLabel(user);
  const statusLabel = user?.isActive ? 'Activo' : 'Pendiente';
  const heroDescription = user?.role === 'Manager'
    ? 'Administra tu identidad profesional y consulta el estado general de tu cuenta desde un espacio más limpio.'
    : 'Consulta tu cuenta y el plan asociado a tus explotaciones desde un único espacio.';
  const detailCards = [
    { label: 'Correo', value: user?.email ?? 'No informado', icon: Mail },
    { label: 'Usuario', value: user?.username || 'Pendiente de activación', icon: Shield },
    { label: 'Rol', value: roleLabel, icon: UserCircle2 },
    { label: 'Plan actual', value: planLabel, icon: CreditCard },
    { label: 'Estado', value: statusLabel, icon: CheckCircle2 }
  ];

  if (user?.organizationName) {
    detailCards.push({ label: 'Organización', value: user.organizationName, icon: Building2 });
  }

  if (user?.farmerStatus) {
    detailCards.push({ label: 'Estado de Ganader@', value: user.farmerStatus, icon: Building2 });
  }

  return (
    <div className="page-stack">
      <header className="page-header">
        <div>
          <h1>Perfil</h1>
          <p>Centro de cuenta para consultar tu acceso, plan actual e identidad principal.</p>
        </div>
      </header>

      <section className="profile-hero-card">
        <div className="profile-hero-main">
          <div className="profile-avatar-xl">{initials}</div>
          <div className="profile-hero-copy">
            <div className="profile-hero-badges">
              <span className="profile-plan-pill">{planLabel}</span>
              <span className="profile-role-pill">{roleLabel}</span>
              <span className={user?.isActive ? 'profile-status-pill' : 'profile-status-pill profile-status-pill-pending'}>
                {statusLabel}
              </span>
            </div>
            <h2>{user?.name} {user?.surname}</h2>
            <p>{heroDescription}</p>
          </div>
        </div>

        <div className="profile-hero-actions">
          <button className="primary-button" type="button" onClick={() => navigate('/app/profile/subscription')}>
            <CreditCard size={16} />
            Gestionar suscripción
          </button>
          <button className="secondary-button" type="button" onClick={() => navigate('/app/profile/settings')}>
            <Settings size={16} />
            Ajustes
          </button>
        </div>
      </section>

      <section className="profile-detail-grid">
        {detailCards.map((item) => {
          const Icon = item.icon;
          return (
            <article key={item.label} className="profile-detail-card">
              <div className="profile-detail-icon">
                <Icon size={16} />
              </div>
              <span>{item.label}</span>
              <strong>{item.value}</strong>
            </article>
          );
        })}
      </section>
    </div>
  );
}
