import { useMemo, useState } from 'react';
import { Navigate, Outlet, Route, Routes, NavLink, useLocation, useNavigate } from 'react-router-dom';
import {
  Building2,
  ChevronRight,
  LayoutDashboard,
  LogOut,
  Menu,
  ShieldCheck,
  Tag,
  Users,
  X
} from 'lucide-react';
import { LoginPage } from '../features/auth/LoginPage';
import { RegisterManagerPage } from '../features/auth/RegisterManagerPage';
import { RegisterFarmerPage } from '../features/auth/RegisterFarmerPage';
import { ForgotPasswordPage } from '../features/auth/ForgotPasswordPage';
import { ResetPasswordPage } from '../features/auth/ResetPasswordPage';
import { ActivateAccountPage } from '../features/auth/ActivateAccountPage';
import { DashboardPage } from '../features/dashboard/DashboardPage';
import { FarmersPage } from '../features/farmers/FarmersPage';
import { FarmsPage } from '../features/farms/FarmsPage';
import { FarmDetailPage } from '../features/farms/FarmDetailPage';
import { ProfilePage } from '../features/profile/ProfilePage';
import { SettingsPage } from '../features/profile/SettingsPage';
import { SubscriptionPage } from '../features/profile/SubscriptionPage';
import { useAuth } from '../shared/auth/AuthContext';
import { getPlanLabel } from '../shared/subscription/plans';

function RequireAuth() {
  const { token, bootstrapped } = useAuth();

  if (!bootstrapped) {
    return <div className="screen-center">Cargando sesión...</div>;
  }

  if (!token) {
    return <Navigate to="/login" replace />;
  }

  return <Outlet />;
}

function AppShell() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const isManager = user?.role === 'Manager';
  const [mobileOpen, setMobileOpen] = useState(false);
  const initials = useMemo(
    () => `${user?.name?.[0] ?? ''}${user?.surname?.[0] ?? ''}`.toUpperCase() || 'P',
    [user]
  );
  const roleLabel = isManager ? 'Gestor@' : 'Ganader@';
  const planLabel = getPlanLabel(user);
  const accountHeading = isManager ? (user?.organizationName || 'Cuenta profesional') : 'Perfil de explotación';
  const secondaryIdentity = isManager
    ? (user?.email || 'Correo no disponible')
    : (user?.username || user?.email || 'Identidad pendiente');
  const primaryItems = [
    { to: '/app/dashboard', label: 'Inicio', icon: LayoutDashboard },
    ...(isManager ? [{ to: '/app/farmers', label: 'Ganaderos', icon: Users }] : []),
    { to: '/app/farms', label: 'Explotaciones', icon: Building2 },
  ];
  const profileActive = location.pathname.startsWith('/app/profile');

  const renderLink = (item) => {
    const Icon = item.icon;

    if (item.disabled) {
      return (
        <div key={item.label} className="sidebar-link sidebar-link-disabled">
          <Icon size={17} className="sidebar-link-icon" />
          <span>{item.label}</span>
        </div>
      );
    }

    return (
      <NavLink
        key={item.to}
        to={item.to}
        onClick={() => setMobileOpen(false)}
        className={({ isActive }) => `sidebar-link${isActive ? ' sidebar-link-active' : ''}`}
      >
        {({ isActive }) => (
          <>
            <Icon size={17} className={isActive ? 'sidebar-link-icon sidebar-link-icon-active' : 'sidebar-link-icon'} />
            <span>{item.label}</span>
          </>
        )}
      </NavLink>
    );
  };

  return (
    <div className="app-shell">
      {mobileOpen && <button className="sidebar-overlay" onClick={() => setMobileOpen(false)} aria-label="Cerrar menú" />}

      <aside className={`sidebar${mobileOpen ? ' sidebar-open' : ''}`}>
        <div className="sidebar-brand">
          <div className="sidebar-brand-mark">
            <Tag size={16} />
          </div>
          <span className="sidebar-brand-title">Pecualia</span>
          <button className="sidebar-mobile-close" onClick={() => setMobileOpen(false)} aria-label="Cerrar menú">
            <X size={18} />
          </button>
        </div>

        <nav className="sidebar-section">
          <span className="sidebar-section-title">Principal</span>
          <div className="nav-links">
            {primaryItems.map(renderLink)}
          </div>
        </nav>

        <section className="sidebar-section sidebar-section-fill">
          <span className="sidebar-section-title">Perfil Activo</span>
          <button
            type="button"
            className={profileActive ? 'sidebar-profile-panel sidebar-profile-panel-active' : 'sidebar-profile-panel'}
            onClick={() => {
              setMobileOpen(false);
              navigate('/app/profile');
            }}
          >
            <div className="sidebar-profile-panel-top">
              <div className="sidebar-avatar sidebar-avatar-lg">{initials}</div>
              <div className="sidebar-profile-panel-copy">
                <strong>{user?.name} {user?.surname}</strong>
                <span>{accountHeading}</span>
              </div>
              <ChevronRight size={16} className="sidebar-profile-panel-arrow" />
            </div>

            <div className="sidebar-profile-badges">
              <span className="sidebar-plan-chip">{planLabel}</span>
              <span className="sidebar-role-chip">{roleLabel}</span>
            </div>

            <div className="sidebar-profile-stats">
              <div>
                <span>{isManager ? 'Correo' : 'Usuario'}</span>
                <strong>{secondaryIdentity}</strong>
              </div>
            </div>

          </button>
        </section>

        <div className="sidebar-footer">
          <div className="sidebar-footer-actions">
            <button className="sidebar-logout sidebar-logout-full" onClick={logout} aria-label="Cerrar sesión">
              <LogOut size={15} />
              <span>Cerrar sesión</span>
            </button>
          </div>
        </div>
      </aside>

      <div className="app-main">
        <header className="mobile-topbar">
          <button className="mobile-topbar-button" onClick={() => setMobileOpen(true)} aria-label="Abrir menú">
            <Menu size={20} />
          </button>
          <div className="mobile-topbar-brand">
            <div className="sidebar-brand-mark">
              <Tag size={14} />
            </div>
            <span>Pecualia</span>
          </div>
        </header>

        <main className="page-content">
        <Outlet />
        </main>
      </div>
    </div>
  );
}

export function AppRouter() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/login" replace />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register/manager" element={<RegisterManagerPage />} />
      <Route path="/register/farmer" element={<RegisterFarmerPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />
      <Route path="/activate-account" element={<ActivateAccountPage />} />

      <Route element={<RequireAuth />}>
        <Route path="/app" element={<AppShell />}>
          <Route path="dashboard" element={<DashboardPage />} />
          <Route path="farmers" element={<FarmersPage />} />
          <Route path="farms" element={<FarmsPage />} />
          <Route path="farms/:farmId" element={<FarmDetailPage />} />
          <Route path="profile" element={<ProfilePage />} />
          <Route path="profile/settings" element={<SettingsPage />} />
          <Route path="profile/subscription" element={<SubscriptionPage />} />
          <Route index element={<Navigate to="/app/dashboard" replace />} />
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/login" replace />} />
    </Routes>
  );
}
