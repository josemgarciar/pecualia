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
  nifCif: '',
  phoneNumber: '',
  residence: '',
  town: '',
  province: '',
  zipCode: '',
  personType: 'Individual',
  birthDate: '',
  managerInvitationCode: '',
  managerEmail: ''
};

export function RegisterFarmerPage() {
  const { login } = useAuth();
  const [form, setForm] = useState(initialForm);
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (event) => {
    event.preventDefault();
    setSubmitting(true);
    setError('');

    try {
      const response = await apiRequest('/api/auth/register/farmer', {
        method: 'POST',
        body: {
          ...form,
          birthDate: form.birthDate || null,
          managerInvitationCode: form.managerInvitationCode || null,
          managerEmail: form.managerEmail || null
        }
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
      title="Registro de Ganader@"
      subtitle="Crea tu acceso directamente o asóciate a un gestor existente mediante código o correo."
    >
      <form className="grid-form" onSubmit={handleSubmit}>
        <label><span>Nombre</span><input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} required /></label>
        <label><span>Apellidos</span><input value={form.surname} onChange={(event) => setForm({ ...form, surname: event.target.value })} required /></label>
        <label><span>Correo</span><input type="email" value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} required /></label>
        <label><span>Usuario</span><input value={form.username} onChange={(event) => setForm({ ...form, username: event.target.value })} required /></label>
        <label><span>Contraseña</span><input type="password" value={form.password} onChange={(event) => setForm({ ...form, password: event.target.value })} required /></label>
        <label><span>NIF/CIF</span><input value={form.nifCif} onChange={(event) => setForm({ ...form, nifCif: event.target.value })} required /></label>
        <label><span>Teléfono</span><input value={form.phoneNumber} onChange={(event) => setForm({ ...form, phoneNumber: event.target.value })} /></label>
        <label><span>Domicilio</span><input value={form.residence} onChange={(event) => setForm({ ...form, residence: event.target.value })} /></label>
        <label><span>Localidad</span><input value={form.town} onChange={(event) => setForm({ ...form, town: event.target.value })} /></label>
        <label><span>Provincia</span><input value={form.province} onChange={(event) => setForm({ ...form, province: event.target.value })} /></label>
        <label><span>Código postal</span><input value={form.zipCode} onChange={(event) => setForm({ ...form, zipCode: event.target.value })} /></label>
        <label>
          <span>Tipo de persona</span>
          <select value={form.personType} onChange={(event) => setForm({ ...form, personType: event.target.value })}>
            <option value="Individual">Persona física</option>
            <option value="Company">Persona jurídica</option>
          </select>
        </label>
        <label><span>Fecha de nacimiento</span><input type="date" value={form.birthDate} onChange={(event) => setForm({ ...form, birthDate: event.target.value })} /></label>
        <label><span>Código de invitación</span><input value={form.managerInvitationCode} onChange={(event) => setForm({ ...form, managerInvitationCode: event.target.value })} /></label>
        <label><span>Correo del gestor</span><input type="email" value={form.managerEmail} onChange={(event) => setForm({ ...form, managerEmail: event.target.value })} /></label>

        {error && <div className="error-banner form-full">{error}</div>}
        <button className="primary-button form-full" disabled={submitting}>{submitting ? 'Creando cuenta...' : 'Crear cuenta de Ganader@'}</button>
      </form>
    </AuthLayout>
  );
}
