import { useMemo, useState } from 'react';
import { Navigate, Outlet, Route, Routes, NavLink } from 'react-router-dom';
import {
  BarChart3,
  Building2,
  CreditCard,
  LayoutDashboard,
  LogOut,
  Menu,
  Settings,
  Tag,
  UserCircle2,
  Users,
  X
} from 'lucide-react';
import { LoginPage } from '../features/auth/LoginPage';
import { RegisterManagerPage } from '../features/auth/RegisterManagerPage';
import { RegisterFarmerPage } from '../features/auth/RegisterFarmerPage';
import { ActivateAccountPage } from '../features/auth/ActivateAccountPage';
import { DashboardPage } from '../features/dashboard/DashboardPage';
import { FarmersPage } from '../features/farmers/FarmersPage';
import { FarmsPage } from '../features/farms/FarmsPage';
import { FarmDetailPage } from '../features/farms/FarmDetailPage';
import { ProfilePage } from '../features/profile/ProfilePage';
import { useAuth } from '../shared/auth/AuthContext';

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
  const isManager = user?.role === 'Manager';
  const [mobileOpen, setMobileOpen] = useState(false);
  const initials = useMemo(
    () => `${user?.name?.[0] ?? ''}${user?.surname?.[0] ?? ''}`.toUpperCase() || 'P',
    [user]
  );
  const primaryItems = [
    { to: '/app/dashboard', label: 'Inicio', icon: LayoutDashboard },
    ...(isManager ? [{ to: '/app/farmers', label: 'Ganaderos', icon: Users }] : []),
    { to: '/app/farms', label: 'Explotaciones', icon: Building2 },
  ];
  const secondaryItems = [
    { to: '/app/profile', label: 'Perfil', icon: UserCircle2 },
    { label: 'Suscripción', icon: CreditCard, disabled: true },
    { label: 'Ajustes', icon: Settings, disabled: true }
  ];

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

        <nav className="sidebar-section sidebar-section-secondary">
          <span className="sidebar-section-title">Cuenta</span>
          <div className="nav-links">
            {secondaryItems.map(renderLink)}
          </div>
        </nav>

        <div className="sidebar-footer">
          <div className="sidebar-user-card">
            <div className="sidebar-avatar">{initials}</div>
            <div className="sidebar-user-copy">
              <strong>{user?.name} {user?.surname}</strong>
              <div className="sidebar-user-meta">
                <span className="sidebar-plan-chip">{user?.role === 'Manager' ? 'PRO' : 'ACTIVA'}</span>
                <span>{user?.role === 'Manager' ? 'Gestora' : 'Ganadero'}</span>
              </div>
            </div>
            <button className="sidebar-logout" onClick={logout} aria-label="Cerrar sesión">
              <LogOut size={15} />
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
      <Route path="/activate-account" element={<ActivateAccountPage />} />

      <Route element={<RequireAuth />}>
        <Route path="/app" element={<AppShell />}>
          <Route path="dashboard" element={<DashboardPage />} />
          <Route path="farmers" element={<FarmersPage />} />
          <Route path="farms" element={<FarmsPage />} />
          <Route path="farms/:farmId" element={<FarmDetailPage />} />
          <Route path="profile" element={<ProfilePage />} />
          <Route index element={<Navigate to="/app/dashboard" replace />} />
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/login" replace />} />
    </Routes>
  );
}
