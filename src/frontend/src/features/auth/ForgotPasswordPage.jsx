import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeft, CheckCircle, Mail } from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { AuthLayout } from './AuthLayout';

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [sent, setSent] = useState(false);

  const handleSubmit = async (event) => {
    event.preventDefault();
    setSubmitting(true);
    setError('');

    try {
      await apiRequest('/api/auth/forgot-password', {
        method: 'POST',
        body: { email }
      });
      setSent(true);
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthLayout
      title="Recuperar contraseña"
      subtitle="Introduce tu correo electrónico y te enviaremos un enlace para restablecer tu contraseña."
    >
      {sent ? (
        <div className="stack">
          <div className="auth-confirm-card" id="forgot-password-success">
            <div className="auth-confirm-icon auth-confirm-icon-success">
              <Mail size={26} />
            </div>
            <h3>Revisa tu bandeja de entrada</h3>
            <p>
              Si existe una cuenta asociada a <strong>{email}</strong>, recibirás un correo con un enlace para restablecer tu contraseña.
            </p>
          </div>
        </div>
      ) : (
        <form className="stack" onSubmit={handleSubmit}>
          <label>
            Correo electrónico
            <input
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              placeholder="tucorreo@mail.com"
              required
              id="forgot-password-email"
            />
          </label>

          {error && <div className="error-banner">{error}</div>}

          <button className="primary-button" disabled={submitting} id="forgot-password-submit">
            {submitting ? 'Enviando...' : 'Enviar enlace de recuperación'}
          </button>
        </form>
      )}
    </AuthLayout>
  );
}
