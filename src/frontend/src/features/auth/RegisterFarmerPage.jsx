import { useState } from 'react';
import { Check, Eye, EyeOff } from 'lucide-react';
import { AuthLayout } from './AuthLayout';
import { apiRequest } from '../../shared/api/client';
import { useAuth } from '../../shared/auth/AuthContext';
import { isValidTaxIdentifier, normalizeTaxIdentifier } from '../../shared/validation/identifiers';

const STEPS = [
  { label: 'Acceso' },
  { label: 'Información' },
  { label: 'Confirmar' }
];

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

function RequiredLabel({ children }) {
  return (
    <span>{children} <span className="field-required">*</span></span>
  );
}

export function RegisterFarmerPage() {
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
      if (form.password.length < 6) { setError('La contraseña debe tener al menos 6 caracteres.'); return false; }
    }
    if (targetStep === 1) {
      if (!form.nifCif.trim()) { setError('El NIF/CIF es obligatorio.'); return false; }
      if (!isValidTaxIdentifier(form.personType, form.nifCif)) {
        setError(form.personType === 'Company' ? 'El NIF indicado no es válido.' : 'El DNI/NIF indicado no es válido.');
        return false;
      }
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
      const response = await apiRequest('/api/auth/register/farmer', {
        method: 'POST',
        body: {
          ...form,
          nifCif: normalizeTaxIdentifier(form.nifCif),
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
      subtitle="Crea tu acceso o asóciate a un gestor existente."
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
          <label>
            <RequiredLabel>Contraseña</RequiredLabel>
            <div className="password-field">
              <input
                type={showPassword ? 'text' : 'password'}
                value={form.password}
                onChange={set('password')}
                placeholder="Mín. 6 caracteres"
              />
              <button className="password-toggle" type="button" onClick={() => setShowPassword((v) => !v)}>
                {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
              </button>
            </div>
          </label>
          <label>
            <span>Tipo de persona</span>
            <select value={form.personType} onChange={set('personType')}>
              <option value="Individual">Persona física</option>
              <option value="Company">Persona jurídica</option>
            </select>
          </label>
        </div>
      )}

      {step === 1 && (
        <div className="grid-form">
          <label>
            <RequiredLabel>{form.personType === 'Company' ? 'NIF de empresa' : 'DNI / NIF'}</RequiredLabel>
            <input value={form.nifCif} onChange={set('nifCif')} placeholder="12345678A" />
          </label>
          <label>
            <span>Teléfono</span>
            <input value={form.phoneNumber} onChange={set('phoneNumber')} placeholder="600 123 456" />
          </label>
          <label>
            <span>Domicilio</span>
            <input value={form.residence} onChange={set('residence')} placeholder="C/ Mayor, 1" />
          </label>
          <label>
            <span>Localidad</span>
            <input value={form.town} onChange={set('town')} placeholder="Tu localidad" />
          </label>
          <label>
            <span>Provincia</span>
            <input value={form.province} onChange={set('province')} placeholder="Tu provincia" />
          </label>
          <label>
            <span>Código postal</span>
            <input value={form.zipCode} onChange={set('zipCode')} placeholder="06400" />
          </label>
          <label className="form-full">
            <span>Fecha de nacimiento</span>
            <input type="date" value={form.birthDate} onChange={set('birthDate')} />
          </label>
        </div>
      )}

      {step === 2 && (
        <div className="stack">
          <p className="reg-hint">Si tu gestor te ha facilitado un código o su correo, introdúcelo aquí. Si no, puedes dejarlo en blanco.</p>
          <div className="grid-form">
            <label>
              <span>Código de invitación</span>
              <input value={form.managerInvitationCode} onChange={set('managerInvitationCode')} placeholder="ABC123" />
            </label>
            <label>
              <span>Correo del gestor</span>
              <input type="email" value={form.managerEmail} onChange={set('managerEmail')} placeholder="gestor@ejemplo.com" />
            </label>
          </div>
          <div className="reg-summary-inline">
            <span><strong>{form.name} {form.surname}</strong> · {form.email}</span>
            <span>{form.personType === 'Individual' ? 'Persona física' : 'Persona jurídica'} · {form.nifCif}</span>
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
