import { useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { AuthLayout } from './AuthLayout';
import { apiRequest } from '../../shared/api/client';

const MIN_PASSWORD_LENGTH = 10;

export function ActivateAccountPage() {
  const [searchParams] = useSearchParams();
  const token = useMemo(() => searchParams.get('token') ?? '', [searchParams]);
  const [form, setForm] = useState({ username: '', password: '' });
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (event) => {
    event.preventDefault();
    setSubmitting(true);
    setError('');
    setSuccess('');

    if (form.password.length < MIN_PASSWORD_LENGTH) {
      setError(`La contraseña debe tener al menos ${MIN_PASSWORD_LENGTH} caracteres.`);
      setSubmitting(false);
      return;
    }

    try {
      const response = await apiRequest('/api/auth/activate-account', {
        method: 'POST',
        body: {
          token,
          username: form.username,
          password: form.password
        }
      });
      setSuccess(response.message);
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthLayout
      title="Activación de cuenta"
      subtitle="Completa tu primer acceso fijando usuario y contraseña."
    >
      <form className="stack" onSubmit={handleSubmit}>
        <label>
          Nombre de usuario
          <input value={form.username} onChange={(event) => setForm({ ...form, username: event.target.value })} required />
        </label>

        <label>
          Contraseña
          <input type="password" value={form.password} onChange={(event) => setForm({ ...form, password: event.target.value })} required minLength={MIN_PASSWORD_LENGTH} />
        </label>

        {!token && <div className="error-banner">No se ha encontrado el token de activación.</div>}
        {error && <div className="error-banner">{error}</div>}
        {success && <div className="success-banner">{success}</div>}
        <button className="primary-button" disabled={!token || submitting}>{submitting ? 'Activando...' : 'Activar cuenta'}</button>
      </form>
    </AuthLayout>
  );
}
