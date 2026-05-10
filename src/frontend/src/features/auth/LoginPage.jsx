import { useState } from 'react';
import { Link } from 'react-router-dom';
import { BriefcaseBusiness, Eye, EyeOff, UserRound } from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { useAuth } from '../../shared/auth/AuthContext';
import { AuthLayout } from './AuthLayout';

export function LoginPage() {
  const { login } = useAuth();
  const [form, setForm] = useState({ identifier: '', password: '', remember: false });
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  const handleSubmit = async (event) => {
    event.preventDefault();
    setSubmitting(true);
    setError('');

    try {
      const response = await apiRequest('/api/auth/login', {
        method: 'POST',
        body: form
      });
      login(response);
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthLayout
      title="Bienvenido"
      subtitle="Accede a tu cuenta para gestionar tus explotaciones ganaderas."
      footer={(
        <>
          <Link className="auth-choice-card" to="/register/farmer">
            <UserRound className="auth-choice-icon" size={24} strokeWidth={2} />
            <strong>Soy Ganader@</strong>
            <span>particular</span>
          </Link>
          <Link className="auth-choice-card" to="/register/manager">
            <BriefcaseBusiness className="auth-choice-icon" size={24} strokeWidth={2} />
            <strong>Soy gestor</strong>
            <span>profesional</span>
          </Link>
        </>
      )}
    >
      <form className="stack" onSubmit={handleSubmit}>
        <label>
          Correo electrónico o usuario
          <input
            value={form.identifier}
            onChange={(event) => setForm({ ...form, identifier: event.target.value })}
            placeholder="tucorreo@mail.com"
            required
          />
        </label>

        <label>
          Contraseña
          <div className="password-field">
            <input
              type={showPassword ? 'text' : 'password'}
              value={form.password}
              onChange={(event) => setForm({ ...form, password: event.target.value })}
              placeholder="••••••••"
              required
            />
            <button className="password-toggle" type="button" onClick={() => setShowPassword((current) => !current)}>
              {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
            </button>
          </div>
        </label>

        <div className="auth-row">
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={form.remember}
              onChange={(event) => setForm({ ...form, remember: event.target.checked })}
            />
            <span>Recordar sesión</span>
          </label>
          <Link className="auth-inline-link" to="/forgot-password">¿Olvidaste tu contraseña?</Link>
        </div>

        {error && <div className="error-banner">{error}</div>}
        <button className="primary-button" disabled={submitting}>{submitting ? 'Entrando...' : 'Entrar a Pecualia'}</button>
      </form>
    </AuthLayout>
  );
}
