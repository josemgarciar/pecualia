import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertCircle, CheckCircle2, ChevronRight, Mail, MapPin, Phone, User, X } from 'lucide-react';
import { useAuth } from '../../shared/auth/AuthContext';
import { apiRequest } from '../../shared/api/client';

const initialForm = {
  personType: 'Individual',
  name: '',
  firstSurname: '',
  secondSurname: '',
  birthDate: '',
  companyName: '',
  legalRepresentative: '',
  email: '',
  nifCif: '',
  phoneNumber: '',
  residence: '',
  town: '',
  province: '',
  zipCode: ''
};

const statusOptions = [
  { value: '', label: 'Todos los estados' },
  { value: 'PendingActivation', label: 'Pendiente' },
  { value: 'Active', label: 'Activo' }
];

const wizardSteps = [
  { label: 'Identificación', description: 'Tipo y datos fiscales', icon: User },
  { label: 'Contacto', description: 'Canales y dirección', icon: Phone },
  { label: 'Confirmación', description: 'Revisión final', icon: CheckCircle2 }
];

function emptyToNull(value) {
  return value.trim() ? value.trim() : null;
}

function toPayload(form) {
  return {
    personType: form.personType,
    name: form.personType === 'Individual' ? form.name.trim() : null,
    firstSurname: form.personType === 'Individual' ? form.firstSurname.trim() : null,
    secondSurname: form.personType === 'Individual' ? emptyToNull(form.secondSurname) : null,
    birthDate: form.personType === 'Individual' && form.birthDate ? form.birthDate : null,
    companyName: form.personType === 'Company' ? form.companyName.trim() : null,
    legalRepresentative: form.personType === 'Company' ? form.legalRepresentative.trim() : null,
    email: form.email.trim(),
    nifCif: form.nifCif.trim(),
    phoneNumber: form.phoneNumber.trim(),
    residence: emptyToNull(form.residence),
    town: form.town.trim(),
    province: form.province.trim(),
    zipCode: emptyToNull(form.zipCode)
  };
}

function mapDetailToForm(detail) {
  return {
    personType: detail.personType,
    name: detail.name ?? '',
    firstSurname: detail.firstSurname ?? '',
    secondSurname: detail.secondSurname ?? '',
    birthDate: detail.birthDate ?? '',
    companyName: detail.companyName ?? '',
    legalRepresentative: detail.legalRepresentative ?? '',
    email: detail.email ?? '',
    nifCif: detail.nifCif ?? '',
    phoneNumber: detail.phoneNumber ?? '',
    residence: detail.residence ?? '',
    town: detail.town ?? '',
    province: detail.province ?? '',
    zipCode: detail.zipCode ?? ''
  };
}

function formatStatus(status) {
  return status === 'PendingActivation' ? 'Pendiente' : 'Activo';
}

function formatPersonType(personType) {
  return personType === 'Company' ? 'Persona jurídica' : 'Persona física';
}

function buildValidationMessage(form, step) {
  if (step === 1) {
    if (form.personType === 'Individual') {
      if (!form.name.trim() || !form.firstSurname.trim() || !form.nifCif.trim()) {
        return 'Completa nombre, primer apellido y NIF.';
      }
    } else if (!form.companyName.trim() || !form.legalRepresentative.trim() || !form.nifCif.trim()) {
      return 'Completa razón social, representante legal y CIF.';
    }
  }

  if (step === 2) {
    if (!form.email.trim() || !form.phoneNumber.trim() || !form.town.trim() || !form.province.trim()) {
      return 'Completa email, teléfono, localidad y provincia.';
    }
  }

  return '';
}

function FormField({ label, required, children }) {
  return (
    <div className="farm-form-field">
      <span className="farm-field-label">
        {label.toUpperCase()}
        {required && <span className="farm-field-label-required"> *</span>}
      </span>
      {children}
    </div>
  );
}

function FarmerWizardModal({
  mode,
  step,
  form,
  error,
  submitting,
  onClose,
  onChange,
  onStepChange,
  onSubmit
}) {
  const validationMessage = buildValidationMessage(form, step);
  const isCreate = mode === 'create';
  const displayName = form.personType === 'Company'
    ? form.companyName || 'Ganadero sin nombre'
    : [form.name, form.firstSurname, form.secondSurname].filter(Boolean).join(' ') || 'Ganadero sin nombre';
  const idDocument = form.nifCif || 'Sin NIF/CIF';

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <div className="modal-card modal-wide farm-modal-shell farmer-wizard-shell">
        <div className="farm-modal-header">
          <div className="farm-modal-title">
            <div className="modal-panel-icon">
              <User size={18} />
            </div>
            <div>
              <h2>{isCreate ? 'Nuevo ganadero' : 'Editar ganadero'}</h2>
              <p>Paso {step} de {wizardSteps.length}</p>
            </div>
          </div>
          <button className="farm-modal-close" type="button" onClick={onClose} aria-label="Cerrar modal">
            <X size={18} />
          </button>
        </div>

        <div className="farm-stepper">
          {wizardSteps.map((item, index) => {
            const currentStep = index + 1;
            const isDone = step > currentStep;
            const isActive = step === currentStep;
            const Icon = item.icon;

            return (
              <div className="farm-stepper-item" key={item.label}>
                <div className="farm-stepper-marker-group">
                  <div className={isDone ? 'farm-stepper-marker farm-stepper-marker-done' : isActive ? 'farm-stepper-marker farm-stepper-marker-active' : 'farm-stepper-marker'}>
                    {isDone ? <CheckCircle2 size={16} /> : <Icon size={15} />}
                  </div>
                  {index < wizardSteps.length - 1 && (
                    <div className={isDone ? 'farm-stepper-connector farm-stepper-connector-done' : 'farm-stepper-connector'} />
                  )}
                </div>
                <span className={isDone || isActive ? 'farm-stepper-label farm-stepper-label-active' : 'farm-stepper-label'}>
                  {item.label}
                </span>
              </div>
            );
          })}
        </div>

        <div className="farm-modal-body farmer-modal-body">
          {error && <div className="error-banner">{error}</div>}

          {step === 1 && (
            <div className="stack">
              <FormField label="Tipo de persona" required>
                <div className="farmer-person-grid">
                  <button
                    className={form.personType === 'Individual' ? 'farmer-person-card farmer-person-card-active' : 'farmer-person-card'}
                    type="button"
                    onClick={() => onChange('personType', 'Individual')}
                  >
                    <strong>Persona física</strong>
                    <span>Ganadero individual (NIF)</span>
                  </button>
                  <button
                    className={form.personType === 'Company' ? 'farmer-person-card farmer-person-card-active' : 'farmer-person-card'}
                    type="button"
                    onClick={() => onChange('personType', 'Company')}
                  >
                    <strong>Persona jurídica</strong>
                    <span>Empresa o sociedad (CIF)</span>
                  </button>
                </div>
              </FormField>

              <div className="farm-form-divider" />

              <div className="grid-form">
                {form.personType === 'Individual' ? (
                  <>
                    <FormField label="Nombre" required>
                      <input
                        className="farm-input"
                        value={form.name}
                        onChange={(event) => onChange('name', event.target.value)}
                        placeholder="ej: Miguel"
                      />
                    </FormField>
                    <FormField label="Primer apellido" required>
                      <input
                        className="farm-input"
                        value={form.firstSurname}
                        onChange={(event) => onChange('firstSurname', event.target.value)}
                        placeholder="ej: Torres"
                      />
                    </FormField>
                    <FormField label="Segundo apellido">
                      <input
                        className="farm-input"
                        value={form.secondSurname}
                        onChange={(event) => onChange('secondSurname', event.target.value)}
                        placeholder="ej: Vega"
                      />
                    </FormField>
                    <FormField label="NIF" required>
                      <input
                        className="farm-input"
                        value={form.nifCif}
                        onChange={(event) => onChange('nifCif', event.target.value)}
                        placeholder="ej: 12345678A"
                      />
                    </FormField>
                    <div className="form-full">
                      <FormField label="Fecha de nacimiento">
                        <input
                          className="farm-input"
                          type="date"
                          value={form.birthDate}
                          onChange={(event) => onChange('birthDate', event.target.value)}
                        />
                      </FormField>
                    </div>
                  </>
                ) : (
                  <>
                    <FormField label="Razón social" required>
                      <input
                        className="farm-input"
                        value={form.companyName}
                        onChange={(event) => onChange('companyName', event.target.value)}
                        placeholder="ej: Ganados Sierra Norte S.L."
                      />
                    </FormField>
                    <FormField label="CIF" required>
                      <input
                        className="farm-input"
                        value={form.nifCif}
                        onChange={(event) => onChange('nifCif', event.target.value)}
                        placeholder="ej: B12345678"
                      />
                    </FormField>
                    <div className="form-full">
                      <FormField label="Representante legal" required>
                        <input
                          className="farm-input"
                          value={form.legalRepresentative}
                          onChange={(event) => onChange('legalRepresentative', event.target.value)}
                          placeholder="Nombre y apellidos del representante"
                        />
                      </FormField>
                    </div>
                  </>
                )}
              </div>
            </div>
          )}

          {step === 2 && (
            <div className="stack">
              <div className="farmer-contact-intro">
                <div className="farmer-contact-pill">
                  <Phone size={14} />
                  <span>Contacto principal</span>
                </div>
                <div className="farmer-contact-pill">
                  <Mail size={14} />
                  <span>Invitación por correo</span>
                </div>
                <div className="farmer-contact-pill">
                  <MapPin size={14} />
                  <span>Ubicación administrativa</span>
                </div>
              </div>

              <div className="grid-form">
                <FormField label="Teléfono principal" required>
                  <input
                    className="farm-input"
                    value={form.phoneNumber}
                    onChange={(event) => onChange('phoneNumber', event.target.value)}
                    placeholder="ej: 627 891 234"
                  />
                </FormField>
                <FormField label="Email" required>
                  <input
                    className="farm-input"
                    type="email"
                    value={form.email}
                    onChange={(event) => onChange('email', event.target.value)}
                    placeholder="ej: miguel@example.com"
                  />
                </FormField>
                <div className="form-full">
                  <FormField label="Dirección">
                    <input
                      className="farm-input"
                      value={form.residence}
                      onChange={(event) => onChange('residence', event.target.value)}
                      placeholder="Calle, número, piso..."
                    />
                  </FormField>
                </div>
                <FormField label="Localidad" required>
                  <input
                    className="farm-input"
                    value={form.town}
                    onChange={(event) => onChange('town', event.target.value)}
                    placeholder="ej: Cáceres"
                  />
                </FormField>
                <FormField label="Provincia" required>
                  <input
                    className="farm-input"
                    value={form.province}
                    onChange={(event) => onChange('province', event.target.value)}
                    placeholder="ej: Cáceres"
                  />
                </FormField>
                <FormField label="Código postal">
                  <input
                    className="farm-input"
                    value={form.zipCode}
                    onChange={(event) => onChange('zipCode', event.target.value)}
                    placeholder="ej: 10001"
                  />
                </FormField>
              </div>
            </div>
          )}

          {step === 3 && (
            <div className="stack">
              <div className="confirmation-hero">
                <div className="confirmation-hero-icon">
                  <User size={18} />
                </div>
                <strong>{displayName}</strong>
                <span>{idDocument}</span>
              </div>

              <div className="summary-grid">
                <div className="farm-summary-card">
                  <div className="farm-summary-card-header">
                    <p>IDENTIFICACIÓN</p>
                  </div>
                  <div className="summary-list">
                    <div className="summary-row">
                      <span>Tipo</span>
                      <strong>{formatPersonType(form.personType)}</strong>
                    </div>
                    {form.personType === 'Individual' ? (
                      <>
                        <div className="summary-row">
                          <span>Nombre completo</span>
                          <strong>{displayName}</strong>
                        </div>
                        <div className="summary-row">
                          <span>NIF</span>
                          <strong>{form.nifCif || 'Sin definir'}</strong>
                        </div>
                        <div className="summary-row">
                          <span>Nacimiento</span>
                          <strong>{form.birthDate || 'No informado'}</strong>
                        </div>
                      </>
                    ) : (
                      <>
                        <div className="summary-row">
                          <span>Razón social</span>
                          <strong>{form.companyName || 'Sin definir'}</strong>
                        </div>
                        <div className="summary-row">
                          <span>Representante</span>
                          <strong>{form.legalRepresentative || 'Sin definir'}</strong>
                        </div>
                        <div className="summary-row">
                          <span>CIF</span>
                          <strong>{form.nifCif || 'Sin definir'}</strong>
                        </div>
                      </>
                    )}
                  </div>
                </div>

                <div className="farm-summary-card">
                  <div className="farm-summary-card-header">
                    <p>CONTACTO Y UBICACIÓN</p>
                  </div>
                  <div className="summary-list">
                    <div className="summary-row">
                      <span>Email</span>
                      <strong>{form.email || 'Sin definir'}</strong>
                    </div>
                    <div className="summary-row">
                      <span>Teléfono</span>
                      <strong>{form.phoneNumber || 'Sin definir'}</strong>
                    </div>
                    <div className="summary-row">
                      <span>Dirección</span>
                      <strong>{form.residence || 'No informada'}</strong>
                    </div>
                    <div className="summary-row">
                      <span>Localidad</span>
                      <strong>{form.town || 'Sin definir'}</strong>
                    </div>
                    <div className="summary-row">
                      <span>Provincia</span>
                      <strong>{form.province || 'Sin definir'}</strong>
                    </div>
                    <div className="summary-row">
                      <span>Código postal</span>
                      <strong>{form.zipCode || 'No informado'}</strong>
                    </div>
                  </div>
                </div>
              </div>

              <div className="farm-confirmation-note">
                <CheckCircle2 size={15} />
                <p>Revisa los datos antes de confirmar. Después podrás editar la ficha desde el detalle del ganadero.</p>
              </div>
            </div>
          )}
        </div>

        <div className="farm-modal-footer">
          <div className="validation-hint">
            {validationMessage ? (
              <span>
                <AlertCircle size={15} />
                {validationMessage}
              </span>
            ) : (
              <span className="farmer-wizard-progress-copy">
                Paso {step} de {wizardSteps.length}
              </span>
            )}
          </div>
          <div className="farm-step-dots" aria-hidden="true">
            {wizardSteps.map((item, index) => {
              const currentStep = index + 1;
              const dotClassName = currentStep === step
                ? 'farm-step-dot farm-step-dot-active'
                : currentStep < step
                  ? 'farm-step-dot farm-step-dot-done'
                  : 'farm-step-dot';

              return <span className={dotClassName} key={item.label} />;
            })}
          </div>
          <div className="wizard-actions">
            <button className="secondary-button" type="button" onClick={step > 1 ? () => onStepChange(step - 1) : onClose}>
              {step > 1 ? 'Atrás' : 'Cancelar'}
            </button>
            {step < 3 ? (
              <button
                className="primary-button"
                type="button"
                onClick={() => onStepChange(step + 1)}
                disabled={Boolean(validationMessage)}
              >
                Siguiente
                <ChevronRight size={15} />
              </button>
            ) : (
              <button className="primary-button" type="button" onClick={onSubmit} disabled={submitting}>
                <CheckCircle2 size={15} />
                {submitting ? 'Guardando...' : isCreate ? 'Guardar ganadero' : 'Guardar cambios'}
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

export function FarmersPage() {
  const navigate = useNavigate();
  const { token, user } = useAuth();
  const [farmers, setFarmers] = useState([]);
  const [selectedFarmerId, setSelectedFarmerId] = useState(null);
  const [selectedFarmer, setSelectedFarmer] = useState(null);
  const [search, setSearch] = useState('');
  const [province, setProvince] = useState('');
  const [status, setStatus] = useState('');
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [modalMode, setModalMode] = useState('create');
  const [modalOpen, setModalOpen] = useState(false);
  const [modalStep, setModalStep] = useState(1);
  const [modalForm, setModalForm] = useState(initialForm);
  const [modalError, setModalError] = useState('');
  const [modalSubmitting, setModalSubmitting] = useState(false);

  const provinceOptions = useMemo(
    () => [...new Set(farmers.map((farmer) => farmer.province).filter(Boolean))].sort((left, right) => left.localeCompare(right)),
    [farmers]
  );

  const loadFarmerDetail = async (farmerId) => {
    if (!farmerId) {
      setSelectedFarmer(null);
      return;
    }

    setDetailLoading(true);
    setError('');

    try {
      const response = await apiRequest(`/api/farmers/${farmerId}`, { token });
      setSelectedFarmer(response);
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setDetailLoading(false);
    }
  };

  const loadFarmers = async (preferredFarmerId = null) => {
    setLoading(true);
    setError('');

    try {
      const params = new URLSearchParams();
      if (search.trim()) {
        params.set('search', search.trim());
      }
      if (province) {
        params.set('province', province);
      }
      if (status) {
        params.set('status', status);
      }

      const response = await apiRequest(`/api/farmers${params.toString() ? `?${params.toString()}` : ''}`, { token });
      setFarmers(response);

      const candidateId = preferredFarmerId ?? selectedFarmerId;
      if (candidateId && response.some((farmer) => farmer.id === candidateId)) {
        setSelectedFarmerId(candidateId);
      } else if (!selectedFarmerId && response.length > 0) {
        setSelectedFarmerId(response[0].id);
      } else if (selectedFarmerId && !response.some((farmer) => farmer.id === selectedFarmerId)) {
        setSelectedFarmerId(response[0]?.id ?? null);
      } else if (response.length === 0) {
        setSelectedFarmerId(null);
        setSelectedFarmer(null);
      }
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (user?.role === 'Manager') {
      loadFarmers();
    }
  }, [token, user, search, province, status]);

  useEffect(() => {
    if (!selectedFarmerId || user?.role !== 'Manager') {
      setSelectedFarmer(null);
      return;
    }

    loadFarmerDetail(selectedFarmerId);
  }, [selectedFarmerId, token, user]);

  const closeModal = () => {
    setModalOpen(false);
    setModalStep(1);
    setModalForm(initialForm);
    setModalError('');
    setModalSubmitting(false);
  };

  const openCreateModal = () => {
    setModalMode('create');
    setModalStep(1);
    setModalForm(initialForm);
    setModalError('');
    setModalOpen(true);
  };

  const openEditModal = () => {
    if (!selectedFarmer) {
      return;
    }

    setModalMode('edit');
    setModalStep(1);
    setModalForm(mapDetailToForm(selectedFarmer));
    setModalError('');
    setModalOpen(true);
  };

  const handleModalChange = (field, value) => {
    setModalForm((current) => {
      if (field === 'personType' && value === 'Individual') {
        return {
          ...current,
          personType: value,
          companyName: '',
          legalRepresentative: ''
        };
      }

      if (field === 'personType' && value === 'Company') {
        return {
          ...current,
          personType: value,
          name: '',
          firstSurname: '',
          secondSurname: '',
          birthDate: ''
        };
      }

      return { ...current, [field]: value };
    });
    setModalError('');
  };

  const handleModalSubmit = async () => {
    setModalSubmitting(true);
    setModalError('');
    setError('');
    setSuccess('');

    try {
      const payload = toPayload(modalForm);
      const response = modalMode === 'create'
        ? await apiRequest('/api/farmers', { method: 'POST', token, body: payload })
        : await apiRequest(`/api/farmers/${selectedFarmerId}`, { method: 'PUT', token, body: payload });

      closeModal();
      setSuccess(modalMode === 'create' ? 'Ganadero creado correctamente.' : 'Ganadero actualizado correctamente.');
      setSelectedFarmerId(response.id);
      await loadFarmers(response.id);
      await loadFarmerDetail(response.id);
    } catch (requestError) {
      setModalError(requestError.message);
    } finally {
      setModalSubmitting(false);
    }
  };

  const handleResend = async () => {
    if (!selectedFarmerId) {
      return;
    }

    setError('');
    setSuccess('');

    try {
      const response = await apiRequest(`/api/farmers/${selectedFarmerId}/send-activation`, {
        method: 'POST',
        token
      });

      setSuccess(response.resent ? 'Invitación reenviada correctamente.' : 'La cuenta ya está activa.');
      await loadFarmers(selectedFarmerId);
      await loadFarmerDetail(selectedFarmerId);
    } catch (requestError) {
      setError(requestError.message);
    }
  };

  if (user?.role !== 'Manager') {
    return <div className="panel-card">Esta sección solo está disponible para gestores.</div>;
  }

  return (
    <div className="page-stack">
      <header className="page-header page-header-actions">
        <div>
          <h1>Ganaderos</h1>
          <p>Gestión de titulares, activaciones pendientes y acceso a sus explotaciones.</p>
        </div>
        <button className="primary-button" onClick={openCreateModal}>Nuevo ganadero</button>
      </header>

      {error && <div className="error-banner">{error}</div>}
      {success && <div className="success-banner">{success}</div>}

      <section className="panel-card stack">
        <div className="panel-header-inline">
          <div>
            <h2>Listado</h2>
            <p>{loading ? 'Cargando ganaderos...' : `${farmers.length} ganaderos visibles`}</p>
          </div>
        </div>

        <div className="toolbar-row">
          <label className="toolbar-field toolbar-search">
            <span>Buscar</span>
            <input
              placeholder="Nombre, NIF/CIF o localidad"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </label>
          <label className="toolbar-field">
            <span>Provincia</span>
            <select value={province} onChange={(event) => setProvince(event.target.value)}>
              <option value="">Todas las provincias</option>
              {provinceOptions.map((option) => (
                <option key={option} value={option}>{option}</option>
              ))}
            </select>
          </label>
          <label className="toolbar-field">
            <span>Estado</span>
            <select value={status} onChange={(event) => setStatus(event.target.value)}>
              {statusOptions.map((option) => (
                <option key={option.value || 'all'} value={option.value}>{option.label}</option>
              ))}
            </select>
          </label>
          <button
            className="secondary-button"
            type="button"
            onClick={() => {
              setSearch('');
              setProvince('');
              setStatus('');
            }}
          >
            Limpiar filtros
          </button>
        </div>

        <div className="farmers-layout">
          <div className="table-card">
            <div className="farmer-table">
              <div className="farmer-table-header">
                <span>Ganadero</span>
                <span>NIF/CIF</span>
                <span>Teléfono</span>
                <span>Localidad</span>
                <span>Provincia</span>
                <span>Explot.</span>
                <span>Estado</span>
              </div>

              <div className="farmer-table-body">
                {farmers.map((farmer) => (
                  <button
                    className={farmer.id === selectedFarmerId ? 'farmer-table-row farmer-table-row-active' : 'farmer-table-row'}
                    key={farmer.id}
                    type="button"
                    onClick={() => {
                      setSelectedFarmerId(farmer.id);
                      setSuccess('');
                    }}
                  >
                    <span>
                      <strong>{farmer.displayName}</strong>
                      <small>{formatPersonType(farmer.personType)}</small>
                    </span>
                    <span>{farmer.nifCif}</span>
                    <span>{farmer.phoneNumber || 'Sin teléfono'}</span>
                    <span>{farmer.town || 'Sin localidad'}</span>
                    <span>{farmer.province || 'Sin provincia'}</span>
                    <span>{farmer.farmCount}</span>
                    <span>
                      <span className={`status-chip status-${farmer.status}`}>{formatStatus(farmer.status)}</span>
                    </span>
                  </button>
                ))}

                {!loading && farmers.length === 0 && (
                  <div className="empty-state">No hay ganaderos para los filtros actuales.</div>
                )}
              </div>
            </div>
          </div>

          <aside className="panel-card detail-panel">
            {detailLoading ? (
              <div className="empty-state">Cargando ficha del ganadero...</div>
            ) : !selectedFarmer ? (
              <div className="empty-state">Selecciona un ganadero para ver el detalle.</div>
            ) : (
              <div className="stack">
                <div className="detail-header">
                  <div>
                    <h2>{selectedFarmer.displayName}</h2>
                    <p>{formatPersonType(selectedFarmer.personType)}</p>
                  </div>
                  <span className={`status-chip status-${selectedFarmer.status}`}>{formatStatus(selectedFarmer.status)}</span>
                </div>

                <div className="detail-grid">
                  <div><span>Email</span><strong>{selectedFarmer.email}</strong></div>
                  <div><span>NIF/CIF</span><strong>{selectedFarmer.nifCif}</strong></div>
                  <div><span>Teléfono</span><strong>{selectedFarmer.phoneNumber || 'No informado'}</strong></div>
                  <div><span>Provincia</span><strong>{selectedFarmer.province || 'No informada'}</strong></div>
                  <div><span>Localidad</span><strong>{selectedFarmer.town || 'No informada'}</strong></div>
                  <div><span>Código postal</span><strong>{selectedFarmer.zipCode || 'No informado'}</strong></div>
                  <div className="detail-full"><span>Dirección</span><strong>{selectedFarmer.residence || 'No informada'}</strong></div>
                  {selectedFarmer.personType === 'Individual' ? (
                    <>
                      <div><span>Nombre</span><strong>{selectedFarmer.name || 'No informado'}</strong></div>
                      <div><span>Primer apellido</span><strong>{selectedFarmer.firstSurname || 'No informado'}</strong></div>
                      <div><span>Segundo apellido</span><strong>{selectedFarmer.secondSurname || 'No informado'}</strong></div>
                      <div><span>Nacimiento</span><strong>{selectedFarmer.birthDate || 'No informado'}</strong></div>
                    </>
                  ) : (
                    <>
                      <div><span>Razón social</span><strong>{selectedFarmer.companyName || 'No informada'}</strong></div>
                      <div className="detail-full"><span>Representante legal</span><strong>{selectedFarmer.legalRepresentative || 'No informado'}</strong></div>
                    </>
                  )}
                </div>

                <div className="detail-actions">
                  <button
                    className="primary-button"
                    type="button"
                    onClick={() => navigate(`/app/farms?farmerId=${selectedFarmer.id}`)}
                  >
                    Ver explotaciones
                  </button>
                  <button className="secondary-button" type="button" onClick={openEditModal}>
                    Editar ganadero
                  </button>
                  {selectedFarmer.canResendActivation && (
                    <button className="secondary-button" type="button" onClick={handleResend}>
                      Reenviar invitación
                    </button>
                  )}
                </div>

                <section className="stack">
                  <div className="panel-header-inline">
                    <h3>Explotaciones asociadas</h3>
                    <span>{selectedFarmer.farms.length}</span>
                  </div>
                  {selectedFarmer.farms.length === 0 ? (
                    <div className="empty-state">Este ganadero todavía no tiene explotaciones registradas.</div>
                  ) : (
                    <div className="table-list">
                      {selectedFarmer.farms.map((farm) => (
                        <article className="list-row" key={farm.id}>
                          <div>
                            <strong>{farm.name}</strong>
                            <div className="muted-text">{farm.regaCode} · {farm.livestockSpecies}</div>
                          </div>
                          <div className="row-actions">
                            <span className={`status-chip status-${farm.status}`}>{farm.status}</span>
                            <span className="muted-text">{farm.animalCount} animales</span>
                          </div>
                        </article>
                      ))}
                    </div>
                  )}
                </section>
              </div>
            )}
          </aside>
        </div>
      </section>

      {modalOpen && (
        <FarmerWizardModal
          mode={modalMode}
          step={modalStep}
          form={modalForm}
          error={modalError}
          submitting={modalSubmitting}
          onClose={closeModal}
          onChange={handleModalChange}
          onStepChange={(nextStep) => {
            const validationMessage = buildValidationMessage(modalForm, modalStep);
            if (nextStep > modalStep && validationMessage) {
              setModalError(validationMessage);
              return;
            }

            setModalError('');
            setModalStep(nextStep);
          }}
          onSubmit={handleModalSubmit}
        />
      )}
    </div>
  );
}
