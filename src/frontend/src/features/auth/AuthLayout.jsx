import { Link, useLocation } from 'react-router-dom';

const HERO_IMAGE = 'https://images.unsplash.com/photo-1682119416157-f3771fc38f4f?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxzaGVlcCUyMGZsb2NrJTIwZmFybSUyMHNwYWluJTIwcGFzdG9yYWx8ZW58MXx8fHwxNzc2NjE1NDg3fDA&ixlib=rb-4.1.0&q=80&w=1080';
const APP_ICON = '/pecualia_icon.png';

export function AuthLayout({ title, subtitle, children, footer }) {
  const { pathname } = useLocation();
  const showBackToLogin = pathname !== '/login';

  return (
    <div className="auth-layout">
      <section className="auth-hero">
        <img className="auth-hero-image" src={HERO_IMAGE} alt="Rebaño en una explotación ganadera" />
        <div className="auth-hero-overlay" />
        <div className="hero-copy">
          <div className="brand-block">
            <div className="brand-mark">
              <img className="brand-mark-image" src={APP_ICON} alt="Icono de Pecualia" />
            </div>
            <div>
              <div className="brand-title">Pecualia</div>
              <div className="brand-subtitle">Gestión ganadera digital, clara y siempre al día.</div>
            </div>
          </div>
          <div className="hero-chip-row">
            <span className="hero-chip">Trazabilidad total</span>
            <span className="hero-chip">Libro oficial</span>
            <span className="hero-chip">Importación TXT</span>
          </div>
        </div>
      </section>

      <section className="auth-panel">
        <div className="auth-card">
          <div className="auth-panel-copy">
            <h1>{title}</h1>
            <p>{subtitle}</p>
          </div>
          {children}
          {footer && <div className="auth-footer">{footer}</div>}
        </div>
        {showBackToLogin && <Link className="link-muted" to="/login">Volver al acceso</Link>}
      </section>
    </div>
  );
}
