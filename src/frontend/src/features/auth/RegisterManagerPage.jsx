import { useState } from 'react';
import { Check, Eye, EyeOff } from 'lucide-react';
import { AuthLayout } from './AuthLayout';
import { apiRequest } from '../../shared/api/client';
import { useAuth } from '../../shared/auth/AuthContext';

const MIN_PASSWORD_LENGTH = 10;
const STEPS = [
  { label: 'Acceso' },
  { label: 'Profesional' },
  { label: 'Confirmar' }
];

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

function RequiredLabel({ children }) {
  return (
    <span>{children} <span className="field-required">*</span></span>
  );
}

export function RegisterManagerPage() {
  const { login } = useAuth();
  const [form, setForm] = useState(initialForm);
  const [step, setStep] = useState(0);
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  const set = (field) => (event) => setForm({ ...form, [field]: event.target.value });

  const validateStep = (targetStep) => {
    setError('');
    if (targetStep === 0) {
      if (!form.name.trim()) { setError('El nombre es obligatorio.'); return false; }
      if (!form.surname.trim()) { setError('Los apellidos son obligatorios.'); return false; }
      if (!form.email.trim()) { setError('El correo es obligatorio.'); return false; }
      if (!form.username.trim()) { setError('El usuario es obligatorio.'); return false; }
      if (!form.password.trim()) { setError('La contraseña es obligatoria.'); return false; }
      if (form.password.length < MIN_PASSWORD_LENGTH) { setError(`La contraseña debe tener al menos ${MIN_PASSWORD_LENGTH} caracteres.`); return false; }
    }
    if (targetStep === 1) {
      if (!form.organizationName.trim()) { setError('El nombre de la asesoría es obligatorio.'); return false; }
      if (!form.professionalIdentifier.trim()) { setError('El NIF/CIF profesional es obligatorio.'); return false; }
    }
    return true;
  };

  const goNext = () => {
    if (validateStep(step)) {
      setStep((s) => Math.min(s + 1, STEPS.length - 1));
    }
  };

  const goPrev = () => {
    setError('');
    setStep((s) => Math.max(s - 1, 0));
  };

  const handleSubmit = async () => {
    if (!validateStep(0) || !validateStep(1)) return;
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
      title="Registro de Gestor"
      subtitle="Crea la cuenta profesional. Empieza en Free, después podrás escalar."
    >

      <div className="reg-stepper">
        {STEPS.map((s, i) => {
          const isActive = i === step;
          const isDone = i < step;
          let cls = 'reg-stepper-item';
          if (isActive) cls += ' reg-stepper-item-active';
          if (isDone) cls += ' reg-stepper-item-done';
          return (
            <div key={s.label} className={cls}>
              <span className="reg-stepper-badge">
                {isDone ? <Check size={12} strokeWidth={3} /> : i + 1}
              </span>
              <span className="reg-stepper-label">{s.label}</span>
            </div>
          );
        })}
      </div>

      {step === 0 && (
        <div className="grid-form">
          <label>
            <RequiredLabel>Nombre</RequiredLabel>
            <input value={form.name} onChange={set('name')} placeholder="Tu nombre" />
          </label>
          <label>
            <RequiredLabel>Apellidos</RequiredLabel>
            <input value={form.surname} onChange={set('surname')} placeholder="Tus apellidos" />
          </label>
          <label>
            <RequiredLabel>Correo</RequiredLabel>
            <input type="email" value={form.email} onChange={set('email')} placeholder="correo@ejemplo.com" />
          </label>
          <label>
            <RequiredLabel>Usuario</RequiredLabel>
            <input value={form.username} onChange={set('username')} placeholder="mi_usuario" />
          </label>
          <label className="form-full">
            <RequiredLabel>Contraseña</RequiredLabel>
            <div className="password-field">
              <input
                type={showPassword ? 'text' : 'password'}
                value={form.password}
                onChange={set('password')}
                placeholder={`Mín. ${MIN_PASSWORD_LENGTH} caracteres`}
              />
              <button className="password-toggle" type="button" onClick={() => setShowPassword((v) => !v)}>
                {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
              </button>
            </div>
          </label>
        </div>
      )}

      {step === 1 && (
        <div className="grid-form">
          <label>
            <RequiredLabel>Asesoría o entidad</RequiredLabel>
            <input value={form.organizationName} onChange={set('organizationName')} placeholder="Nombre de tu asesoría" />
          </label>
          <label>
            <RequiredLabel>NIF/CIF profesional</RequiredLabel>
            <input value={form.professionalIdentifier} onChange={set('professionalIdentifier')} placeholder="B12345678" />
          </label>
          <label>
            <span>Teléfono</span>
            <input value={form.phoneNumber} onChange={set('phoneNumber')} placeholder="600 123 456" />
          </label>
          <label>
            <span>Provincia</span>
            <input value={form.province} onChange={set('province')} placeholder="Tu provincia" />
          </label>
          <label className="form-full">
            <span>Localidad</span>
            <input value={form.town} onChange={set('town')} placeholder="Tu localidad" />
          </label>
        </div>
      )}

      {step === 2 && (
        <div className="stack">
          <div className="reg-summary-inline">
            <span><strong>{form.name} {form.surname}</strong> · {form.email}</span>
            <span>{form.organizationName} · {form.professionalIdentifier}</span>
            {form.phoneNumber && <span>Tel. {form.phoneNumber}</span>}
            {(form.province || form.town) && (
              <span>{[form.town, form.province].filter(Boolean).join(', ')}</span>
            )}
          </div>
        </div>
      )}

      {error && <div className="error-banner">{error}</div>}

      <div className="reg-nav">
        {step > 0 ? (
          <button type="button" className="secondary-button" onClick={goPrev}>Anterior</button>
        ) : <span />}
        {step < STEPS.length - 1 ? (
          <button type="button" className="primary-button" onClick={goNext}>Siguiente</button>
        ) : (
          <button type="button" className="primary-button" disabled={submitting} onClick={handleSubmit}>
            {submitting ? 'Creando...' : 'Crear cuenta'}
          </button>
        )}
      </div>
    </AuthLayout>
  );
}
