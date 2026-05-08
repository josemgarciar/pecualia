import { useEffect, useMemo, useState } from 'react';
import { useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import {
  AlertCircle,
  ArrowLeft,
  Building2,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  MapPin,
  Plus,
  Search,
  X
} from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { useAuth } from '../../shared/auth/AuthContext';

const initialForm = {
  farmerId: '',
  name: '',
  regaCode: '',
  livestockSpecies: '',
  regime: '',
  town: '',
  province: '',
  address: '',
  zipCode: '',
  authorisedCapacity: '',
  porcineRegistryNumber: '',
  responsible: '',
  zootechnicClassification: '',
  notes: ''
};

const speciesOptions = [
  { value: 'Ovine', label: 'Ovino' },
  { value: 'Caprine', label: 'Caprino' },
  { value: 'Porcine', label: 'Porcino' }
];

const regimeOptions = [
  { value: 'Extensive', label: 'Extensivo' },
  { value: 'SemiExtensive', label: 'Semiextensivo' },
  { value: 'Intensive', label: 'Intensivo' }
];

const statusOptions = [
  { value: '', label: 'Todos los estados' },
  { value: 'Active', label: 'Activa' },
  { value: 'Pending', label: 'Pendiente' },
  { value: 'Inactive', label: 'Inactiva' }
];

const provinceOptions = [
  'Álava', 'Albacete', 'Alicante', 'Almería', 'Asturias', 'Ávila', 'Badajoz', 'Barcelona', 'Burgos', 'Cáceres',
  'Cádiz', 'Cantabria', 'Castellón', 'Ciudad Real', 'Córdoba', 'Cuenca', 'Girona', 'Granada', 'Guadalajara',
  'Guipúzcoa', 'Huelva', 'Huesca', 'Islas Baleares', 'Jaén', 'La Coruña', 'La Rioja', 'Las Palmas', 'León',
  'Lleida', 'Lugo', 'Madrid', 'Málaga', 'Murcia', 'Navarra', 'Ourense', 'Palencia', 'Pontevedra', 'Salamanca',
  'Santa Cruz de Tenerife', 'Segovia', 'Sevilla', 'Soria', 'Tarragona', 'Teruel', 'Toledo', 'Valencia',
  'Valladolid', 'Vizcaya', 'Zamora', 'Zaragoza'
];

const wizardSteps = [
  { label: 'Datos básicos', icon: Building2 },
  { label: 'Ubicación', icon: MapPin },
  { label: 'Confirmación', icon: CheckCircle2 }
];

const speciesToneMap = {
  Ovine: { bg: '#DDEBDF', color: '#2F6B4F', label: 'Ovino' },
  Caprine: { bg: '#DBEAFE', color: '#2563EB', label: 'Caprino' },
  Porcine: { bg: '#FCE7F3', color: '#9D174D', label: 'Porcino' }
};

const statusToneMap = {
  Active: { bg: '#DDEBDF', color: '#2F6B4F', label: 'Activa' },
  Pending: { bg: '#FEF3C7', color: '#D97706', label: 'Pendiente' },
  Inactive: { bg: '#F3F4F6', color: '#6B7280', label: 'Inactiva' }
};

function formatSpecies(value) {
  return speciesToneMap[value]?.label ?? value;
}

function formatRegime(value) {
  return regimeOptions.find((option) => option.value === value)?.label ?? value;
}

function emptyToNull(value) {
  return value.trim() ? value.trim() : null;
}

function buildFarmErrors(form, step, isManager) {
  const errors = {};

  if (step === 1) {
    if (!form.name.trim()) {
      errors.name = 'Campo obligatorio';
    }
    if (!form.regaCode.trim()) {
      errors.regaCode = 'Campo obligatorio';
    } else if (!/^ES\d{12}$/i.test(form.regaCode.trim())) {
      errors.regaCode = 'Formato REGA inválido (ej: ES061230000145)';
    }
    if (!form.livestockSpecies) {
      errors.livestockSpecies = 'Selecciona una especie';
    }
    if (!form.regime) {
      errors.regime = 'Selecciona un régimen';
    }
    if (form.livestockSpecies === 'Porcine' && !form.porcineRegistryNumber.trim()) {
      errors.porcineRegistryNumber = 'Campo obligatorio para porcino';
    }
    if (isManager && !form.farmerId) {
      errors.farmerId = 'Selecciona un Ganader@';
    }
  }

  if (step === 2) {
    if (!form.town.trim()) {
      errors.town = 'Campo obligatorio';
    }
    if (!form.province) {
      errors.province = 'Selecciona una provincia';
    }
  }

  return errors;
}

function FormField({ label, required, error, children }) {
  return (
    <div className="farm-form-field">
      <span className="farm-field-label">
        {label.toUpperCase()}
        {required && <span className="farm-field-label-required"> *</span>}
      </span>
      {children}
      {error && (
        <p className="farm-field-error">
          <AlertCircle size={11} />
          {error}
        </p>
      )}
    </div>
  );
}

function InputField({ label, value, onChange, placeholder, required, type = 'text', error, min }) {
  return (
    <FormField label={label} required={required} error={error}>
      <input
        type={type}
        min={min}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        className={error ? 'farm-input farm-input-error' : 'farm-input'}
      />
    </FormField>
  );
}

function SelectField({ label, value, onChange, options, placeholder, required, error }) {
  return (
    <FormField label={label} required={required} error={error}>
      <div className="select-wrapper">
        <select
          value={value}
          onChange={(event) => onChange(event.target.value)}
          className={error ? 'farm-input farm-input-error' : 'farm-input'}
        >
          <option value="">{placeholder}</option>
          {options.map((option) => (
            <option key={option.value} value={option.value}>{option.label}</option>
          ))}
        </select>
        <ChevronDown size={16} />
      </div>
    </FormField>
  );
}

function SummaryRow({ label, value }) {
  return (
    <div className="summary-row">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function FarmModal({
  form,
  step,
  showValidation,
  requestError,
  success,
  submitting,
  farmers,
  user,
  onChange,
  onClose,
  onNext,
  onBack,
  onSubmit
}) {
  const currentStepErrors = showValidation ? buildFarmErrors(form, step, user?.role === 'Manager') : {};
  const selectedFarmer = farmers.find((farmer) => String(farmer.id) === form.farmerId);
  const ownerName = selectedFarmer?.displayName ?? (`${user?.name ?? ''} ${user?.surname ?? ''}`.trim() || '—');

  if (success) {
    return (
      <div className="modal-backdrop" role="dialog" aria-modal="true">
        <div className="farm-success-card">
          <div className="farm-success-icon">
            <CheckCircle2 size={40} />
          </div>
          <div className="farm-success-copy">
            <h2>¡Explotación registrada!</h2>
            <p>
              <strong>{form.name}</strong> ha sido creada correctamente en el sistema.
            </p>
            <span>Ya puedes seguir gestionando el listado y crear más explotaciones desde esta vista.</span>
          </div>
          <button className="primary-button farm-success-button" type="button" onClick={onClose}>
            Entendido
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <div className="modal-card modal-wide farm-modal-shell">
        <div className="farm-modal-header">
          <div className="farm-modal-title">
            <div className="modal-panel-icon">
              <Building2 size={18} />
            </div>
            <div>
              <h2>Nueva explotación</h2>
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

        <div className="farm-modal-body">
          {requestError && <div className="error-banner">{requestError}</div>}

          {step === 1 && (
            <div className="stack">
              <InputField
                label="Nombre de la explotación"
                value={form.name}
                onChange={(value) => onChange('name', value)}
                placeholder="ej: Dehesa El Robledal"
                required
                error={currentStepErrors.name}
              />
              <InputField
                label="Código REGA"
                value={form.regaCode}
                onChange={(value) => onChange('regaCode', value)}
                placeholder="ej: ES061230000145"
                required
                error={currentStepErrors.regaCode}
              />
              <div className="grid-form">
                <SelectField
                  label="Especie"
                  value={form.livestockSpecies}
                  onChange={(value) => onChange('livestockSpecies', value)}
                  options={speciesOptions}
                  placeholder="Selecciona especie"
                  required
                  error={currentStepErrors.livestockSpecies}
                />
                <SelectField
                  label="Régimen"
                  value={form.regime}
                  onChange={(value) => onChange('regime', value)}
                  options={regimeOptions}
                  placeholder="Selecciona régimen"
                  required
                  error={currentStepErrors.regime}
                />
              </div>
              {user?.role === 'Manager' && (
                <SelectField
                  label="Ganader@ titular"
                  value={form.farmerId}
                  onChange={(value) => onChange('farmerId', value)}
                  options={farmers.map((farmer) => ({ value: String(farmer.id), label: farmer.displayName }))}
                  placeholder="Selecciona un Ganader@"
                  required
                  error={currentStepErrors.farmerId}
                />
              )}
              {form.livestockSpecies === 'Porcine' && (
                <div className="grid-form">
                  <InputField
                    label="Nº registro porcino"
                    value={form.porcineRegistryNumber}
                    onChange={(value) => onChange('porcineRegistryNumber', value)}
                    placeholder="ej: 018BA0020"
                    required
                    error={currentStepErrors.porcineRegistryNumber}
                  />
                  <InputField
                    label="Capacidad autorizada (plazas)"
                    value={form.authorisedCapacity}
                    onChange={(value) => onChange('authorisedCapacity', value)}
                    placeholder="ej: 1200"
                    type="number"
                    min="0"
                  />
                </div>
              )}
            </div>
          )}

          {step === 2 && (
            <div className="stack">
              <InputField
                label="Dirección / paraje"
                value={form.address}
                onChange={(value) => onChange('address', value)}
                placeholder="Calle, paraje, polígono..."
              />
              <div className="grid-form">
                <InputField
                  label="Localidad"
                  value={form.town}
                  onChange={(value) => onChange('town', value)}
                  placeholder="ej: Cáceres"
                  required
                  error={currentStepErrors.town}
                />
                <InputField
                  label="Código postal"
                  value={form.zipCode}
                  onChange={(value) => onChange('zipCode', value)}
                  placeholder="ej: 10001"
                />
              </div>
              <SelectField
                label="Provincia"
                value={form.province}
                onChange={(value) => onChange('province', value)}
                options={provinceOptions.map((province) => ({ value: province, label: province }))}
                placeholder="Selecciona una provincia"
                required
                error={currentStepErrors.province}
              />
              <div className="farm-form-divider" />
              <FormField label="Notas internas">
                <textarea
                  rows="3"
                  value={form.notes}
                  onChange={(event) => onChange('notes', event.target.value)}
                  placeholder="Observaciones sobre la explotación..."
                  className="farm-input"
                />
              </FormField>
            </div>
          )}

          {step === 3 && (
            <div className="stack">
              <div className="confirmation-hero">
                <div className="confirmation-hero-icon">
                  <Building2 size={30} />
                </div>
                <strong>{form.name || '—'}</strong>
                <span>{form.regaCode || 'Sin REGA'}</span>
              </div>

              <div className="farm-summary-card">
                <div className="farm-summary-card-header">
                  <p>DATOS DE LA EXPLOTACIÓN</p>
                </div>
                <div className="summary-list">
                  <SummaryRow label="Nombre" value={form.name || '—'} />
                  <SummaryRow label="Código REGA" value={form.regaCode || '—'} />
                  <SummaryRow label="Especie" value={formatSpecies(form.livestockSpecies) || '—'} />
                  <SummaryRow label="Régimen" value={formatRegime(form.regime) || '—'} />
                  <SummaryRow label="Ganader@ titular" value={ownerName} />
                  {form.livestockSpecies === 'Porcine' && form.porcineRegistryNumber && (
                    <SummaryRow label="Registro porcino" value={form.porcineRegistryNumber} />
                  )}
                  {form.livestockSpecies === 'Porcine' && form.authorisedCapacity && (
                    <SummaryRow label="Capacidad" value={`${form.authorisedCapacity} plazas`} />
                  )}
                </div>
              </div>

              <div className="farm-summary-card">
                <div className="farm-summary-card-header">
                  <p>UBICACIÓN</p>
                </div>
                <div className="summary-list">
                  {form.address && <SummaryRow label="Dirección" value={form.address} />}
                  <SummaryRow label="Localidad" value={[form.town, form.zipCode].filter(Boolean).join(' · ') || '—'} />
                  <SummaryRow label="Provincia" value={form.province || '—'} />
                  {form.notes && <SummaryRow label="Notas" value={form.notes} />}
                </div>
              </div>

              <div className="farm-confirmation-note">
                <CheckCircle2 size={16} />
                <p>Revisa los datos antes de confirmar. La explotación quedará activa inmediatamente tras guardar.</p>
              </div>
            </div>
          )}
        </div>

        <div className="farm-modal-footer">
          <button className="secondary-button" type="button" onClick={step === 1 ? onClose : onBack}>
            <ArrowLeft size={15} />
            {step === 1 ? 'Cancelar' : 'Anterior'}
          </button>

          <div className="farm-step-dots" aria-hidden="true">
            {wizardSteps.map((item, index) => {
              const currentStep = index + 1;
              const className = currentStep === step
                ? 'farm-step-dot farm-step-dot-active'
                : currentStep < step
                  ? 'farm-step-dot farm-step-dot-done'
                  : 'farm-step-dot';

              return <span className={className} key={item.label} />;
            })}
          </div>

          {step < 3 ? (
            <button className="primary-button" type="button" onClick={onNext}>
              Siguiente
              <ChevronRight size={15} />
            </button>
          ) : (
            <button className="primary-button" type="button" onClick={onSubmit} disabled={submitting}>
              <CheckCircle2 size={15} />
              {submitting ? 'Guardando...' : 'Guardar explotación'}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

export function FarmsPage() {
  const location = useLocation();
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const { token, user } = useAuth();
  const [farms, setFarms] = useState([]);
  const [farmers, setFarmers] = useState([]);
  const [search, setSearch] = useState('');
  const [filterSpecies, setFilterSpecies] = useState('');
  const [filterStatus, setFilterStatus] = useState('');
  const [modalOpen, setModalOpen] = useState(false);
  const [modalStep, setModalStep] = useState(1);
  const [modalForm, setModalForm] = useState(initialForm);
  const [modalRequestError, setModalRequestError] = useState('');
  const [modalSubmitting, setModalSubmitting] = useState(false);
  const [modalValidationStep, setModalValidationStep] = useState(0);
  const [modalSuccess, setModalSuccess] = useState(false);
  const [error, setError] = useState('');
  const selectedFarmerId = searchParams.get('farmerId') ?? '';

  const loadData = async () => {
    setError('');

    try {
      const [farmResponse, farmerResponse] = await Promise.all([
        apiRequest('/api/farms', { token }),
        user?.role === 'Manager' ? apiRequest('/api/farmers', { token }) : Promise.resolve([])
      ]);

      setFarms(farmResponse);
      setFarmers(farmerResponse);
    } catch (requestError) {
      setError(requestError.message);
    }
  };

  useEffect(() => {
    loadData();
  }, [token, user?.role]);

  const selectedFarmer = farmers.find((farmer) => String(farmer.id) === selectedFarmerId) ?? null;

  const filteredFarms = useMemo(() => {
    return farms.filter((farm) => {
      const matchesSearch =
        !search ||
        farm.name.toLowerCase().includes(search.toLowerCase()) ||
        farm.regaCode.toLowerCase().includes(search.toLowerCase());
      const matchesSpecies = !filterSpecies || farm.livestockSpecies === filterSpecies;
      const matchesStatus = !filterStatus || farm.status === filterStatus;
      const matchesFarmer = !selectedFarmerId || String(farm.farmerId) === selectedFarmerId;

      return matchesSearch && matchesSpecies && matchesStatus && matchesFarmer;
    });
  }, [farms, filterSpecies, filterStatus, search, selectedFarmerId]);

  const resetModal = () => {
    setModalOpen(false);
    setModalStep(1);
    setModalForm(initialForm);
    setModalRequestError('');
    setModalSubmitting(false);
    setModalValidationStep(0);
    setModalSuccess(false);
  };

  const openModal = () => {
    setModalForm((current) => ({
      ...initialForm,
      farmerId: user?.role === 'Manager' ? (selectedFarmerId || current.farmerId) : String(user?.id ?? '')
    }));
    setModalStep(1);
    setModalRequestError('');
    setModalSubmitting(false);
    setModalValidationStep(0);
    setModalSuccess(false);
    setModalOpen(true);
  };

  useEffect(() => {
    if (!location.state?.openCreateModal) {
      return;
    }

    openModal();
    navigate(`${location.pathname}${location.search}`, { replace: true, state: {} });
  }, [location.pathname, location.search, location.state, navigate, selectedFarmerId, user?.id, user?.role]);

  const handleModalChange = (field, value) => {
    setModalForm((current) => {
      if (field === 'livestockSpecies' && value !== 'Porcine') {
        return { ...current, [field]: value, authorisedCapacity: '', porcineRegistryNumber: '' };
      }

      return { ...current, [field]: value };
    });
    setModalRequestError('');
  };

  const handleNextStep = () => {
    const stepErrors = buildFarmErrors(modalForm, modalStep, user?.role === 'Manager');
    if (Object.keys(stepErrors).length > 0) {
      setModalValidationStep(modalStep);
      return;
    }

    setModalValidationStep(0);
    setModalRequestError('');
    setModalStep((current) => current + 1);
  };

  const handlePreviousStep = () => {
    setModalValidationStep(0);
    setModalRequestError('');
    setModalStep((current) => current - 1);
  };

  const handleSubmit = async () => {
    setModalSubmitting(true);
    setModalRequestError('');

    try {
      await apiRequest('/api/farms', {
        method: 'POST',
        token,
        body: {
          farmerId: Number(modalForm.farmerId || user.id),
          name: modalForm.name.trim(),
          regaCode: modalForm.regaCode.trim(),
          livestockSpecies: modalForm.livestockSpecies,
          regime: modalForm.regime,
          town: emptyToNull(modalForm.town),
          province: emptyToNull(modalForm.province),
          address: emptyToNull(modalForm.address),
          zipCode: emptyToNull(modalForm.zipCode),
          authorisedCapacity: modalForm.livestockSpecies === 'Porcine' && modalForm.authorisedCapacity
            ? Number(modalForm.authorisedCapacity)
            : null,
          porcineRegistryNumber: modalForm.livestockSpecies === 'Porcine'
            ? emptyToNull(modalForm.porcineRegistryNumber)
            : null,
          responsible: null,
          zootechnicClassification: null,
          xCoordinate: null,
          yCoordinate: null
        }
      });

      await loadData();
      setModalSuccess(true);
    } catch (requestError) {
      setModalRequestError(requestError.message);
    } finally {
      setModalSubmitting(false);
    }
  };

  return (
    <div className="page-stack">
      {modalOpen && (
        <FarmModal
          form={modalForm}
          step={modalStep}
          showValidation={modalValidationStep === modalStep}
          requestError={modalRequestError}
          success={modalSuccess}
          submitting={modalSubmitting}
          farmers={farmers}
          user={user}
          onChange={handleModalChange}
          onClose={resetModal}
          onNext={handleNextStep}
          onBack={handlePreviousStep}
          onSubmit={handleSubmit}
        />
      )}

      <header className="page-header page-header-actions">
        <div>
          <h1>Explotaciones</h1>
          <p>
            {farms.length} explotaciones registradas · {farms.filter((farm) => farm.status === 'Active').length} activas
          </p>
        </div>
        <button className="primary-button" onClick={openModal} type="button">
          <Plus size={16} />
          Nueva explotación
        </button>
      </header>

      {selectedFarmer && (
        <div className="filter-summary">
          <div>
            <strong>Filtro activo:</strong> {selectedFarmer.displayName}
          </div>
          <button className="secondary-button" type="button" onClick={() => setSearchParams({})}>
            Ver todas
          </button>
        </div>
      )}

      {error && <div className="error-banner">{error}</div>}

      <section className="panel-card stack farm-panel-card">
        <div className="toolbar-row toolbar-row-farms farm-toolbar-row">
          <label className="toolbar-search farm-toolbar-search">
            Buscar
            <div className="search-input-wrapper">
              <Search size={16} />
              <input
                placeholder="Buscar por nombre o código REGA..."
                value={search}
                onChange={(event) => setSearch(event.target.value)}
              />
            </div>
          </label>
          <label className="toolbar-field">
            Especie
            <div className="select-wrapper">
              <select value={filterSpecies} onChange={(event) => setFilterSpecies(event.target.value)}>
                <option value="">Todas las especies</option>
                {speciesOptions.map((option) => (
                  <option key={option.value} value={option.value}>{option.label}</option>
                ))}
              </select>
              <ChevronDown size={16} />
            </div>
          </label>
          <label className="toolbar-field">
            Estado
            <div className="select-wrapper">
              <select value={filterStatus} onChange={(event) => setFilterStatus(event.target.value)}>
                {statusOptions.map((option) => (
                  <option key={option.value || 'all'} value={option.value}>{option.label}</option>
                ))}
              </select>
              <ChevronDown size={16} />
            </div>
          </label>
          <button
            className="secondary-button"
            type="button"
            onClick={() => {
              setSearch('');
              setFilterSpecies('');
              setFilterStatus('');
              if (selectedFarmerId) {
                setSearchParams({});
              }
            }}
          >
            Limpiar filtros
          </button>
        </div>

        <div className="farm-list-meta">
          <span>{filteredFarms.length} de {farms.length} explotaciones</span>
        </div>

        <div className="farm-card-list">
          {filteredFarms.map((farm) => {
            const speciesTone = speciesToneMap[farm.livestockSpecies] ?? { bg: '#F3F4F6', color: '#6B7280', label: farm.livestockSpecies };
            const statusTone = statusToneMap[farm.status] ?? statusToneMap.Inactive;
            const isPorcine = farm.livestockSpecies === 'Porcine';
            const occupancy = isPorcine && farm.authorisedCapacity ? Math.round((farm.animalCount / farm.authorisedCapacity) * 100) : null;
            const occupancyTone = occupancy !== null && occupancy > 90 ? '#DC2626' : '#2F6B4F';

            return (
              <article
                className="farm-card farm-card-static farm-card-interactive"
                key={farm.id}
                role="button"
                tabIndex={0}
                onClick={() => navigate(`/app/farms/${farm.id}`)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    navigate(`/app/farms/${farm.id}`);
                  }
                }}
              >
                <div className="farm-card-icon">
                  <Building2 size={20} />
                </div>
                <div className="farm-card-copy">
                  <div className="farm-card-header">
                    <div>
                      <h3>{farm.name}</h3>
                      <p>REGA: {farm.regaCode}</p>
                    </div>
                    <div className="farm-card-badges">
                      <span className="farm-badge" style={{ background: speciesTone.bg, color: speciesTone.color }}>
                        {speciesTone.label}
                      </span>
                      <span className="farm-badge" style={{ background: statusTone.bg, color: statusTone.color }}>
                        {statusTone.label}
                      </span>
                    </div>
                  </div>

                  <div className="farm-card-meta">
                    <span>
                      <MapPin size={12} />
                      {farm.town || 'Sin localidad'}, {farm.province || 'Sin provincia'}
                    </span>
                    <span>Régimen: {formatRegime(farm.regime) || 'No indicado'}</span>
                    {isPorcine && farm.authorisedCapacity && <span>Cap.: {farm.authorisedCapacity}</span>}
                    <span className="farm-card-highlight">{farm.animalCount} animales</span>
                    <span>{farm.farmerName}</span>
                  </div>

                  {occupancy !== null && (
                    <div className="occupancy-block">
                      <div className="occupancy-copy">
                        <span>Ocupación</span>
                        <strong style={{ color: occupancyTone }}>{occupancy}%</strong>
                      </div>
                      <div className="occupancy-bar">
                        <div
                          className="occupancy-bar-fill"
                          style={{ width: `${Math.min(occupancy, 100)}%`, background: occupancyTone }}
                        />
                      </div>
                    </div>
                  )}

                  <div className="farm-card-link">
                    <span>Ver detalle</span>
                    <ChevronRight size={14} />
                  </div>
                </div>
              </article>
            );
          })}

          {filteredFarms.length === 0 && (
            <div className="empty-state farm-empty-state">
              <Building2 size={36} />
              <div>No se encontraron explotaciones con los filtros aplicados.</div>
            </div>
          )}
        </div>
      </section>
    </div>
  );
}
