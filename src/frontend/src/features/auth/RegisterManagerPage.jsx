import { useState } from 'react';
import { AuthLayout } from './AuthLayout';
import { apiRequest } from '../../shared/api/client';
import { useAuth } from '../../shared/auth/AuthContext';

const initialForm = {
  name: '',
  surname: '',
  email: '',
  username: '',
  password: '',
  organizationName: '',
  professionalIdentifier: '',
  phoneNumber: '',
  province: '',
  town: '',
  planType: 'Basic'
};

export function RegisterManagerPage() {
  const { login } = useAuth();
  const [form, setForm] = useState(initialForm);
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (event) => {
    event.preventDefault();
    setSubmitting(true);
    setError('');

    try {
      const response = await apiRequest('/api/auth/register/manager', {
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
      title="Registro de gestor"
      subtitle="Crea la cuenta profesional y activa automáticamente tu espacio de trabajo."
    >
      <form className="grid-form" onSubmit={handleSubmit}>
        <label><span>Nombre</span><input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} required /></label>
        <label><span>Apellidos</span><input value={form.surname} onChange={(event) => setForm({ ...form, surname: event.target.value })} required /></label>
        <label><span>Correo</span><input type="email" value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} required /></label>
        <label><span>Usuario</span><input value={form.username} onChange={(event) => setForm({ ...form, username: event.target.value })} required /></label>
        <label><span>Contraseña</span><input type="password" value={form.password} onChange={(event) => setForm({ ...form, password: event.target.value })} required /></label>
        <label><span>Asesoría o entidad</span><input value={form.organizationName} onChange={(event) => setForm({ ...form, organizationName: event.target.value })} required /></label>
        <label><span>NIF/CIF profesional</span><input value={form.professionalIdentifier} onChange={(event) => setForm({ ...form, professionalIdentifier: event.target.value })} required /></label>
        <label><span>Teléfono</span><input value={form.phoneNumber} onChange={(event) => setForm({ ...form, phoneNumber: event.target.value })} /></label>
        <label><span>Provincia</span><input value={form.province} onChange={(event) => setForm({ ...form, province: event.target.value })} /></label>
        <label><span>Localidad</span><input value={form.town} onChange={(event) => setForm({ ...form, town: event.target.value })} /></label>
        <label>
          <span>Plan inicial</span>
          <select value={form.planType} onChange={(event) => setForm({ ...form, planType: event.target.value })}>
            <option value="Basic">Free</option>
            <option value="Professional">Pro</option>
            <option value="Enterprise">Max</option>
          </select>
        </label>

        {error && <div className="error-banner form-full">{error}</div>}
        <button className="primary-button form-full" disabled={submitting}>{submitting ? 'Creando cuenta...' : 'Crear cuenta de gestor'}</button>
      </form>
    </AuthLayout>
  );
}
