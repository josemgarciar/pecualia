import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Eye, EyeOff, CheckCircle, ShieldAlert } from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { AuthLayout } from './AuthLayout';

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') || '';

  const [form, setForm] = useState({ password: '', confirmPassword: '' });
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [success, setSuccess] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError('');

    if (form.password.length < 8) {
      setError('La contraseña debe tener al menos 8 caracteres.');
      return;
    }

    if (form.password !== form.confirmPassword) {
      setError('Las contraseñas no coinciden.');
      return;
    }

    setSubmitting(true);

    try {
      await apiRequest('/api/auth/reset-password', {
        method: 'POST',
        body: { token, newPassword: form.password }
      });
      setSuccess(true);
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setSubmitting(false);
    }
  };

  if (!token) {
    return (
      <AuthLayout
        title="Enlace no válido"
        subtitle="El enlace de recuperación no contiene un token válido."
      >
        <div className="stack">
          <div className="auth-confirm-card" id="reset-password-invalid-token">
            <div className="auth-confirm-icon auth-confirm-icon-error">
              <ShieldAlert size={26} />
            </div>
            <h3>Enlace no válido</h3>
            <p>No se ha encontrado un token de recuperación en la URL. Solicita un nuevo enlace desde la página de recuperación.</p>
          </div>
          <Link className="primary-button" to="/forgot-password" style={{ textAlign: 'center' }}>
            Solicitar nuevo enlace
          </Link>
        </div>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout
      title="Nueva contraseña"
      subtitle="Introduce tu nueva contraseña para restablecer el acceso a tu cuenta."
    >
      {success ? (
        <div className="stack">
          <div className="auth-confirm-card" id="reset-password-success">
            <div className="auth-confirm-icon auth-confirm-icon-success">
              <CheckCircle size={26} />
            </div>
            <h3>Contraseña restablecida</h3>
            <p>Tu contraseña se ha actualizado correctamente. Ya puedes acceder a tu cuenta con la nueva contraseña.</p>
          </div>
          <Link className="primary-button" to="/login" style={{ textAlign: 'center' }}>
            Iniciar sesión
          </Link>
        </div>
      ) : (
        <form className="stack" onSubmit={handleSubmit}>
          <label>
            Nueva contraseña
            <div className="password-field">
              <input
                type={showPassword ? 'text' : 'password'}
                value={form.password}
                onChange={(event) => setForm({ ...form, password: event.target.value })}
                placeholder="Mínimo 8 caracteres"
                required
                minLength={8}
                id="reset-password-new"
              />
              <button className="password-toggle" type="button" onClick={() => setShowPassword((current) => !current)}>
                {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
              </button>
            </div>
          </label>

          <label>
            Confirmar contraseña
            <div className="password-field">
              <input
                type={showConfirm ? 'text' : 'password'}
                value={form.confirmPassword}
                onChange={(event) => setForm({ ...form, confirmPassword: event.target.value })}
                placeholder="Repite la contraseña"
                required
                minLength={8}
                id="reset-password-confirm"
              />
              <button className="password-toggle" type="button" onClick={() => setShowConfirm((current) => !current)}>
                {showConfirm ? <EyeOff size={18} /> : <Eye size={18} />}
              </button>
            </div>
          </label>

          {error && <div className="error-banner">{error}</div>}

          <button className="primary-button" disabled={submitting} id="reset-password-submit">
            {submitting ? 'Restableciendo...' : 'Restablecer contraseña'}
          </button>
        </form>
      )}
    </AuthLayout>
  );
}
