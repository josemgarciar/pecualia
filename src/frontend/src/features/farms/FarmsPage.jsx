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
  Search
} from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { useAuth } from '../../shared/auth/AuthContext';
import { ModalBody, ModalDialog, ModalFooter, ModalHeader, ModalStepper } from '../../shared/components/modal/Modal';
import { isValidRegaCode, normalizeRegaCode } from '../../shared/validation/identifiers';

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
  porcineRegistryNumber: '',
  livestockType: '',
  porcineMothersCapacity: '',
  porcineFatteningCapacity: '',
  responsible: '',
  zootechnicClassification: '',
  spindle: '',
  xCoordinate: '',
  yCoordinate: ''
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
    } else if (!isValidRegaCode(form.regaCode)) {
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
    if (form.livestockSpecies === 'Porcine' && form.porcineMothersCapacity !== '') {
      const porcineMothersCapacity = Number(form.porcineMothersCapacity);
      if (!Number.isInteger(porcineMothersCapacity) || porcineMothersCapacity < 0) {
        errors.porcineMothersCapacity = 'Debe ser un número entero válido';
      }
    }
    if (form.livestockSpecies === 'Porcine' && form.porcineFatteningCapacity !== '') {
      const porcineFatteningCapacity = Number(form.porcineFatteningCapacity);
      if (!Number.isInteger(porcineFatteningCapacity) || porcineFatteningCapacity < 0) {
        errors.porcineFatteningCapacity = 'Debe ser un número entero válido';
      }
    }
    if (form.spindle !== '') {
      const spindle = Number(form.spindle);
      if (!Number.isInteger(spindle) || spindle < 1) {
        errors.spindle = 'Debe ser un número entero válido';
      }
    }
    if (form.xCoordinate !== '' && Number.isNaN(Number(form.xCoordinate))) {
      errors.xCoordinate = 'Debe ser un número válido';
    }
    if (form.yCoordinate !== '' && Number.isNaN(Number(form.yCoordinate))) {
      errors.yCoordinate = 'Debe ser un número válido';
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
      <ModalDialog size="wide" shellClassName="farm-success-shell">
        <ModalHeader
          icon={<CheckCircle2 size={18} />}
          title="¡Explotación registrada!"
          subtitle="La alta se ha completado correctamente."
          onClose={onClose}
        />
        <ModalBody className="farm-success-body">
          <div className="farm-success-card">
            <div className="farm-success-icon">
              <CheckCircle2 size={40} />
            </div>
            <div className="farm-success-copy">
              <h2>Registro completado</h2>
              <p>
                <strong>{form.name}</strong> ha sido creada correctamente en el sistema.
              </p>
              <span>Ya puedes seguir gestionando el listado y crear más explotaciones desde esta vista.</span>
            </div>
          </div>
        </ModalBody>
        <ModalFooter align="end">
          <button className="primary-button farm-success-button" type="button" onClick={onClose}>
            Entendido
          </button>
        </ModalFooter>
      </ModalDialog>
    );
  }

  return (
    <ModalDialog size="wide">
      <ModalHeader
        icon={<Building2 size={18} />}
        title="Nueva explotación"
        subtitle={`Paso ${step} de ${wizardSteps.length}`}
        onClose={onClose}
      />
      <ModalStepper steps={wizardSteps} currentStep={step} />
      <ModalBody>
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
              <div className="grid-form">
                <InputField
                  label="Tipo de explotación"
                  value={form.livestockType}
                  onChange={(value) => onChange('livestockType', value)}
                  placeholder="ej: Reproductora"
                />
                <InputField
                  label="Responsable"
                  value={form.responsible}
                  onChange={(value) => onChange('responsible', value)}
                  placeholder="ej: María Pérez"
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
                </div>
              )}
              {form.livestockSpecies === 'Porcine' && (
                <div className="grid-form">
                  <InputField
                    label="Capacidad máxima madres"
                    value={form.porcineMothersCapacity}
                    onChange={(value) => onChange('porcineMothersCapacity', value)}
                    placeholder="ej: 180"
                    type="number"
                    min="0"
                    error={currentStepErrors.porcineMothersCapacity}
                  />
                  <InputField
                    label="Capacidad máxima cebo"
                    value={form.porcineFatteningCapacity}
                    onChange={(value) => onChange('porcineFatteningCapacity', value)}
                    placeholder="ej: 950"
                    type="number"
                    min="0"
                    error={currentStepErrors.porcineFatteningCapacity}
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
              <InputField
                label="Clasificación zootécnica"
                value={form.zootechnicClassification}
                onChange={(value) => onChange('zootechnicClassification', value)}
                placeholder="ej: Cebo"
              />
              <div className="grid-form">
                <InputField
                  label="Huso"
                  value={form.spindle}
                  onChange={(value) => onChange('spindle', value)}
                  placeholder="ej: 30"
                  type="number"
                  min="1"
                  error={currentStepErrors.spindle}
                />
                <InputField
                  label="Coordenada X"
                  value={form.xCoordinate}
                  onChange={(value) => onChange('xCoordinate', value)}
                  placeholder="ej: 726541.32"
                  error={currentStepErrors.xCoordinate}
                />
              </div>
              <InputField
                label="Coordenada Y"
                value={form.yCoordinate}
                onChange={(value) => onChange('yCoordinate', value)}
                placeholder="ej: 4378123.11"
                error={currentStepErrors.yCoordinate}
              />
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
                  {form.livestockType && <SummaryRow label="Tipo de explotación" value={form.livestockType} />}
                  {form.responsible && <SummaryRow label="Responsable" value={form.responsible} />}
                  {form.livestockSpecies === 'Porcine' && form.porcineRegistryNumber && (
                    <SummaryRow label="Registro porcino" value={form.porcineRegistryNumber} />
                  )}
                  {form.livestockSpecies === 'Porcine' && form.porcineMothersCapacity && (
                    <SummaryRow label="Capacidad máxima madres" value={form.porcineMothersCapacity} />
                  )}
                  {form.livestockSpecies === 'Porcine' && form.porcineFatteningCapacity && (
                    <SummaryRow label="Capacidad máxima cebo" value={form.porcineFatteningCapacity} />
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
                  {form.zootechnicClassification && <SummaryRow label="Clasificación zootécnica" value={form.zootechnicClassification} />}
                  {form.spindle && <SummaryRow label="Huso" value={form.spindle} />}
                  {form.xCoordinate && <SummaryRow label="Coordenada X" value={form.xCoordinate} />}
                  {form.yCoordinate && <SummaryRow label="Coordenada Y" value={form.yCoordinate} />}
                </div>
              </div>

              <div className="farm-confirmation-note">
                <CheckCircle2 size={16} />
                <p>Revisa los datos antes de confirmar. La explotación quedará activa inmediatamente tras guardar.</p>
              </div>
            </div>
          )}
      </ModalBody>

      <ModalFooter>
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
      </ModalFooter>
    </ModalDialog>
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
      const matchesFarmer = !selectedFarmerId || String(farm.farmerId) === selectedFarmerId;

      return matchesSearch && matchesSpecies && matchesFarmer;
    });
  }, [farms, filterSpecies, search, selectedFarmerId]);

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
        return {
          ...current,
          [field]: value,
          porcineRegistryNumber: '',
          porcineMothersCapacity: '',
          porcineFatteningCapacity: ''
        };
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

    const porcineMothersCapacity = modalForm.livestockSpecies === 'Porcine' && modalForm.porcineMothersCapacity !== ''
      ? Number(modalForm.porcineMothersCapacity)
      : null;
    const porcineFatteningCapacity = modalForm.livestockSpecies === 'Porcine' && modalForm.porcineFatteningCapacity !== ''
      ? Number(modalForm.porcineFatteningCapacity)
      : null;

    try {
      await apiRequest('/api/farms', {
        method: 'POST',
        token,
        body: {
          farmerId: Number(modalForm.farmerId || user.id),
          name: modalForm.name.trim(),
          regaCode: normalizeRegaCode(modalForm.regaCode),
          livestockSpecies: modalForm.livestockSpecies,
          regime: modalForm.regime,
          town: emptyToNull(modalForm.town),
          province: emptyToNull(modalForm.province),
          address: emptyToNull(modalForm.address),
          zipCode: emptyToNull(modalForm.zipCode),
          authorisedCapacity: modalForm.livestockSpecies === 'Porcine' && (porcineMothersCapacity != null || porcineFatteningCapacity != null)
            ? (porcineMothersCapacity ?? 0) + (porcineFatteningCapacity ?? 0)
            : null,
          porcineRegistryNumber: modalForm.livestockSpecies === 'Porcine'
            ? emptyToNull(modalForm.porcineRegistryNumber)
            : null,
          livestockType: emptyToNull(modalForm.livestockType),
          porcineMothersCapacity,
          porcineFatteningCapacity,
          responsible: emptyToNull(modalForm.responsible),
          zootechnicClassification: emptyToNull(modalForm.zootechnicClassification),
          spindle: modalForm.spindle === '' ? null : Number(modalForm.spindle),
          xCoordinate: modalForm.xCoordinate === '' ? null : Number(modalForm.xCoordinate),
          yCoordinate: modalForm.yCoordinate === '' ? null : Number(modalForm.yCoordinate)
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
          <p>{farms.length} explotaciones registradas</p>
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

      <section className="panel-card stack listing-panel">
        <div className="panel-header-inline">
          <div>
            <h2>Listado</h2>
            <p>{`${filteredFarms.length} explotaciones visibles`}</p>
          </div>
        </div>

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
          <button
            className="secondary-button"
            type="button"
            onClick={() => {
              setSearch('');
              setFilterSpecies('');
              if (selectedFarmerId) {
                setSearchParams({});
              }
            }}
          >
            Limpiar filtros
          </button>
        </div>

        <div className="table-card farm-list-shell">

          <div className="farm-card-list">
            {filteredFarms.map((farm) => {
              const speciesTone = speciesToneMap[farm.livestockSpecies] ?? { bg: '#F3F4F6', color: '#6B7280', label: farm.livestockSpecies };
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
                      </div>
                    </div>

                    <div className="farm-card-meta">
                      <span>
                        <MapPin size={12} />
                        {farm.town || 'Sin localidad'}, {farm.province || 'Sin provincia'}
                      </span>
                      <span>Régimen: {formatRegime(farm.regime) || 'No indicado'}</span>
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
        </div>
      </section>
    </div>
  );
}
