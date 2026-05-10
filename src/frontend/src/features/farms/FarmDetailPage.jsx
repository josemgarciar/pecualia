import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  ArrowLeft,
  BarChart3,
  BookOpen,
  Building2,
  ArrowLeftRight,
  Check,
  ChevronDown,
  ClipboardCheck,
  Edit3,
  MapPin,
  Plus,
  Search,
  Shield,
  Skull,
  Sprout,
  Tag,
  Trash2,
  TrendingDown,
  TrendingUp,
  TriangleAlert
} from 'lucide-react';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer
} from 'recharts';
import { apiBlobRequest, apiRequest } from '../../shared/api/client';
import { useAuth } from '../../shared/auth/AuthContext';
import { ModalBody, ModalDialog, ModalFooter, ModalHeader } from '../../shared/components/modal/Modal';
import {
  getAnimalIdentificationFormatMessage,
  isValidAnimalIdentification,
  isValidRegaCode,
  normalizeAnimalIdentification,
  normalizeRegaCode
} from '../../shared/validation/identifiers';
import { FarmMovementsSection } from '../movements/FarmMovementsSection';

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

const regimeLabelMap = {
  Extensive: 'Extensivo',
  SemiExtensive: 'Semiextensivo',
  Intensive: 'Intensivo'
};

const animalSexLabelMap = {
  Female: 'Hembra',
  Male: 'Macho'
};

const animalRegistrationCauseLabelMap = {
  Entrada: 'Entrada (E)',
  Autorreposicion: 'Autorreposición (A)'
};

const animalDischargeCauseLabelMap = {
  Salida: 'Salida (S)',
  Muerte: 'Muerte (M)'
};

const detailTabs = [
  { key: 'summary', label: 'Resumen', icon: Building2, enabled: true },
  { key: 'animals', label: 'Animales', icon: Tag, enabled: true },
  { key: 'movements', label: 'Movimientos', icon: ArrowLeftRight, enabled: true },
  { key: 'births', label: 'Nacimientos', icon: Sprout, enabled: true },
  { key: 'deaths', label: 'Muertes', icon: Skull, enabled: true },
  { key: 'vaccinations', label: 'Vacunación', icon: Shield, enabled: true },
  { key: 'balances', label: 'Censos y balances', icon: BarChart3, enabled: true },
  { key: 'book', label: 'Libro', icon: BookOpen, enabled: true },
  { key: 'incidents', label: 'Incidencias', icon: TriangleAlert, enabled: true },
  { key: 'inspections', label: 'Inspecciones', icon: ClipboardCheck, enabled: true }
];

const currentYear = new Date().getFullYear();
const monthLabels = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];
const BOOK_PREVIEW_MAX_PAGES = 3;
const BOOK_PREVIEW_DEBOUNCE_MS = 450;
const BOOK_PREVIEW_TARGET_WIDTH = 760;
const FARM_ANIMALS_SEARCH_DEBOUNCE_MS = 300;
const FARM_ANIMALS_DEFAULT_PAGE_SIZE = 25;
const FARM_ANIMALS_PAGE_SIZE_OPTIONS = [10, 25, 50];
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

function formatText(value, fallback = 'No informado') {
  return value ?? fallback;
}

function formatRegime(value) {
  if (!value) {
    return 'No informado';
  }

  return regimeLabelMap[value] ?? value;
}

function formatCoordinate(value) {
  if (value == null) {
    return 'No informada';
  }

  return new Intl.NumberFormat('es-ES', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  }).format(value);
}

function formatDate(value) {
  return value ? new Intl.DateTimeFormat('es-ES').format(new Date(`${value}T00:00:00`)) : '—';
}

function formatAnimalSex(value) {
  return animalSexLabelMap[value] ?? value ?? 'No informado';
}

function formatAnimalCause(value) {
  return animalRegistrationCauseLabelMap[value] ?? animalDischargeCauseLabelMap[value] ?? value ?? 'No informada';
}

function getDeathDestinationOptions(species) {
  return species === 'Porcine'
    ? [{ value: 'MER', label: 'MER' }]
    : [
        { value: 'SANDACH', label: 'SANDACH' },
        { value: 'MER', label: 'MER' }
      ];
}

function parsePositiveNumber(value) {
  return value === '' ? null : Number(value);
}

function parseOptionalInteger(value) {
  return value === '' ? null : Number(value);
}

function emptyToNull(value) {
  const normalized = value?.trim();
  return normalized ? normalized : null;
}

function createAutorrepositionForm(farm) {
  const today = new Date().toISOString().slice(0, 10);

  return {
    startIdentification: '',
    numberOfAnimals: '1',
    breed: '',
    sex: '',
    birthYear: '',
    registrationDate: today,
    genotyping: '',
    dominantAllele: '',
    lowAllele: '',
    animalType: '',
    identificationDate: farm?.livestockSpecies === 'Porcine' ? today : '',
    pigRegistrationNumber: '',
    tag: ''
  };
}

function buildConsecutiveIdentificationPreview(startIdentification, numberOfAnimals) {
  const normalizedIdentification = startIdentification.trim().toUpperCase();
  const count = Number(numberOfAnimals);

  if (!normalizedIdentification || !Number.isInteger(count) || count <= 0) {
    return null;
  }

  let numericStartIndex = normalizedIdentification.length;
  while (numericStartIndex > 0 && /\d/.test(normalizedIdentification[numericStartIndex - 1])) {
    numericStartIndex -= 1;
  }

  if (numericStartIndex === normalizedIdentification.length) {
    throw new Error('La identificación inicial debe terminar en una secuencia numérica.');
  }

  const prefix = normalizedIdentification.slice(0, numericStartIndex);
  const numericPart = normalizedIdentification.slice(numericStartIndex);
  const initialNumber = BigInt(numericPart);
  const width = numericPart.length;
  const lastNumber = initialNumber + BigInt(count - 1);
  const lastDigits = lastNumber.toString().padStart(width, '0');

  if (lastDigits.length > width) {
    throw new Error('El rango solicitado desborda la longitud numérica de la identificación inicial.');
  }

  return {
    firstIdentification: `${prefix}${initialNumber.toString().padStart(width, '0')}`,
    lastIdentification: `${prefix}${lastDigits}`,
    count
  };
}

function validateAutorrepositionForm(form, species) {
  const errors = {};
  const count = Number(form.numberOfAnimals);

  if (!form.startIdentification.trim()) {
    errors.startIdentification = 'Campo obligatorio';
  } else if (!isValidAnimalIdentification(species, form.startIdentification)) {
    errors.startIdentification = getAnimalIdentificationFormatMessage(species);
  }

  if (!Number.isInteger(count) || count <= 0) {
    errors.numberOfAnimals = 'Debe ser un número entero mayor que cero';
  }

  if (!form.breed.trim()) {
    errors.breed = 'Campo obligatorio';
  }

  if (!form.sex.trim()) {
    errors.sex = 'Campo obligatorio';
  }

  if (form.birthYear === '') {
    errors.birthYear = 'Campo obligatorio';
  }

  if (form.birthYear !== '') {
    const birthYear = Number(form.birthYear);
    if (!Number.isInteger(birthYear) || birthYear < 1900 || birthYear > 2100) {
      errors.birthYear = 'Debe ser un año válido';
    }
  }

  if (!form.registrationDate) {
    errors.registrationDate = 'Campo obligatorio';
  }

  if (species === 'Porcine' && !form.animalType.trim()) {
    errors.animalType = 'Campo obligatorio para porcino';
  }

  if (Object.keys(errors).length > 0) {
    return errors;
  }

  try {
    buildConsecutiveIdentificationPreview(form.startIdentification, form.numberOfAnimals);
  } catch (error) {
    errors.startIdentification = error.message;
  }

  return errors;
}

function createAnimalDetailForm(animal) {
  return {
    identification: animal?.identification ?? '',
    birthYear: animal?.birthYear != null ? String(animal.birthYear) : '',
    breed: animal?.breed ?? '',
    sex: animal?.sex ?? '',
    registrationDate: animal?.registrationDate ?? '',
    registrationCause: animal?.registrationCauseValue ?? '',
    originCode: animal?.originCode ?? '',
    healthDocumentNumber: animal?.healthDocumentNumber ?? '',
    genotyping: animal?.ovinoCaprino?.genotyping ?? '',
    dominantAllele: animal?.ovinoCaprino?.dominantAllele ?? '',
    lowAllele: animal?.ovinoCaprino?.lowAllele ?? '',
    animalType: animal?.porcino?.animalType ?? '',
    identificationDate: animal?.porcino?.identificationDate ?? '',
    pigRegistrationNumber: animal?.porcino?.pigRegistrationNumber ?? '',
    tag: animal?.porcino?.tag ?? ''
  };
}

function resolveAnimalGuideField(animal, form) {
  const registrationCause = form.registrationCause || animal?.registrationCauseValue || '';

  if (registrationCause === 'Autorreposicion') {
    return { value: '', disabled: true };
  }

  if (registrationCause === 'Entrada') {
    return { value: animal?.entryGuideSerie ?? '', disabled: true };
  }

  return { value: form.healthDocumentNumber, disabled: false };
}

function validateAnimalDetailForm(form, species) {
  const errors = {};

  if (!form.identification.trim()) {
    errors.identification = 'Campo obligatorio';
  } else if (!isValidAnimalIdentification(species, form.identification)) {
    errors.identification = getAnimalIdentificationFormatMessage(species);
  }

  if (form.originCode.trim() && !isValidRegaCode(form.originCode)) {
    errors.originCode = 'Código REGA inválido';
  }

  if (species === 'Porcine' && !form.animalType.trim()) {
    errors.animalType = 'Campo obligatorio para porcino';
  }

  if (form.birthYear !== '') {
    const birthYear = Number(form.birthYear);
    if (!Number.isInteger(birthYear) || birthYear < 1900 || birthYear > 2100) {
      errors.birthYear = 'Debe ser un año válido';
    }
  }

  return errors;
}

function createFarmSettingsForm(farm) {
  return {
    name: farm?.name ?? '',
    regaCode: farm?.regaCode ?? '',
    regime: farm?.regime ?? '',
    town: farm?.town ?? '',
    province: farm?.province ?? '',
    address: farm?.address ?? '',
    zipCode: farm?.zipCode ?? '',
    authorisedCapacity: farm?.authorisedCapacity != null ? String(farm.authorisedCapacity) : '',
    porcineRegistryNumber: farm?.porcineRegistryNumber ?? '',
    responsible: farm?.responsible ?? '',
    zootechnicClassification: farm?.zootechnicClassification ?? '',
    xCoordinate: farm?.xCoordinate != null ? String(farm.xCoordinate) : '',
    yCoordinate: farm?.yCoordinate != null ? String(farm.yCoordinate) : ''
  };
}

function validateFarmSettingsForm(form, species) {
  const errors = {};

  if (!form.name.trim()) {
    errors.name = 'Campo obligatorio';
  }
  if (!form.regaCode.trim()) {
    errors.regaCode = 'Campo obligatorio';
  } else if (!isValidRegaCode(form.regaCode)) {
    errors.regaCode = 'Formato REGA inválido (ej: ES061230000145)';
  }
  if (!form.regime) {
    errors.regime = 'Selecciona un régimen';
  }
  if (!form.town.trim()) {
    errors.town = 'Campo obligatorio';
  }
  if (!form.province) {
    errors.province = 'Selecciona una provincia';
  }
  if (species === 'Porcine' && !form.porcineRegistryNumber.trim()) {
    errors.porcineRegistryNumber = 'Campo obligatorio para porcino';
  }
  if (form.authorisedCapacity !== '' && Number(form.authorisedCapacity) < 0) {
    errors.authorisedCapacity = 'Debe ser igual o mayor que cero';
  }
  if (form.xCoordinate !== '' && Number.isNaN(Number(form.xCoordinate))) {
    errors.xCoordinate = 'Debe ser un número válido';
  }
  if (form.yCoordinate !== '' && Number.isNaN(Number(form.yCoordinate))) {
    errors.yCoordinate = 'Debe ser un número válido';
  }

  return errors;
}

function buildBookPdfPath(farmId, sectionIds) {
  const params = new URLSearchParams();
  sectionIds.forEach((sectionId) => params.append('sectionIds', sectionId));
  const query = params.toString();
  return `/api/farms/${farmId}/book/pdf${query ? `?${query}` : ''}`;
}

function createVaccinationFormState() {
  return {
    animalIdentification: '',
    vaccinationDate: new Date().toISOString().slice(0, 10),
    nextDose: '',
    vaccinationType: '',
    observations: ''
  };
}

function DetailField({ label, value, fullWidth = false }) {
  return (
    <div className={fullWidth ? 'detail-full' : undefined}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function SummaryMetric({ label, value, tone = 'default' }) {
  return (
    <article className={`farm-detail-metric-card${tone === 'success' ? ' farm-detail-metric-card-success' : ''}`}>
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  );
}

function FarmSettingsModal({ farm, form, errors, requestError, submitting, onChange, onClose, onSubmit }) {
  return (
    <ModalDialog size="wide">
      <ModalHeader
        icon={<Edit3 size={18} />}
        title="Ajustes de la explotación"
        subtitle={`Edita los datos administrativos y operativos de ${farm.name}.`}
        onClose={onClose}
      />
      <ModalBody className="farm-settings-body">
          {requestError && <div className="error-banner">{requestError}</div>}

          <div className="farm-settings-grid">
            <div className="farm-form-field">
              <span className="farm-field-label">NOMBRE DE LA EXPLOTACIÓN <span className="farm-field-label-required">*</span></span>
              <input className={errors.name ? 'farm-input farm-input-error' : 'farm-input'} value={form.name} onChange={(event) => onChange('name', event.target.value)} />
              {errors.name && <p className="farm-field-error">{errors.name}</p>}
            </div>

            <div className="farm-form-field">
              <span className="farm-field-label">CÓDIGO REGA <span className="farm-field-label-required">*</span></span>
              <input className={errors.regaCode ? 'farm-input farm-input-error' : 'farm-input'} value={form.regaCode} onChange={(event) => onChange('regaCode', event.target.value)} />
              {errors.regaCode && <p className="farm-field-error">{errors.regaCode}</p>}
            </div>

            <div className="farm-form-field">
              <span className="farm-field-label">ESPECIE</span>
              <input className="farm-input" value={speciesToneMap[farm.livestockSpecies]?.label ?? farm.livestockSpecies} disabled />
            </div>

            <div className="farm-form-field">
              <span className="farm-field-label">RÉGIMEN <span className="farm-field-label-required">*</span></span>
              <div className="select-wrapper">
                <select className={errors.regime ? 'farm-input farm-input-error' : 'farm-input'} value={form.regime} onChange={(event) => onChange('regime', event.target.value)}>
                  <option value="">Selecciona régimen</option>
                  {regimeOptions.map((option) => (
                    <option key={option.value} value={option.value}>{option.label}</option>
                  ))}
                </select>
              </div>
              {errors.regime && <p className="farm-field-error">{errors.regime}</p>}
            </div>

            {farm.livestockSpecies === 'Porcine' && (
              <>
                <div className="farm-form-field">
                  <span className="farm-field-label">Nº REGISTRO PORCINO <span className="farm-field-label-required">*</span></span>
                  <input className={errors.porcineRegistryNumber ? 'farm-input farm-input-error' : 'farm-input'} value={form.porcineRegistryNumber} onChange={(event) => onChange('porcineRegistryNumber', event.target.value)} />
                  {errors.porcineRegistryNumber && <p className="farm-field-error">{errors.porcineRegistryNumber}</p>}
                </div>

                <div className="farm-form-field">
                  <span className="farm-field-label">CAPACIDAD AUTORIZADA</span>
                  <input type="number" min="0" className={errors.authorisedCapacity ? 'farm-input farm-input-error' : 'farm-input'} value={form.authorisedCapacity} onChange={(event) => onChange('authorisedCapacity', event.target.value)} />
                  {errors.authorisedCapacity && <p className="farm-field-error">{errors.authorisedCapacity}</p>}
                </div>
              </>
            )}

            <div className="farm-form-field">
              <span className="farm-field-label">RESPONSABLE</span>
              <input className="farm-input" value={form.responsible} onChange={(event) => onChange('responsible', event.target.value)} />
            </div>

            <div className="farm-form-field">
              <span className="farm-field-label">CLASIFICACIÓN ZOOTÉCNICA</span>
              <input className="farm-input" value={form.zootechnicClassification} onChange={(event) => onChange('zootechnicClassification', event.target.value)} />
            </div>

            <div className="farm-form-field farm-settings-grid-full">
              <span className="farm-field-label">DIRECCIÓN / PARAJE</span>
              <input className="farm-input" value={form.address} onChange={(event) => onChange('address', event.target.value)} />
            </div>

            <div className="farm-form-field">
              <span className="farm-field-label">LOCALIDAD <span className="farm-field-label-required">*</span></span>
              <input className={errors.town ? 'farm-input farm-input-error' : 'farm-input'} value={form.town} onChange={(event) => onChange('town', event.target.value)} />
              {errors.town && <p className="farm-field-error">{errors.town}</p>}
            </div>

            <div className="farm-form-field">
              <span className="farm-field-label">PROVINCIA <span className="farm-field-label-required">*</span></span>
              <div className="select-wrapper">
                <select className={errors.province ? 'farm-input farm-input-error' : 'farm-input'} value={form.province} onChange={(event) => onChange('province', event.target.value)}>
                  <option value="">Selecciona una provincia</option>
                  {provinceOptions.map((province) => (
                    <option key={province} value={province}>{province}</option>
                  ))}
                </select>
              </div>
              {errors.province && <p className="farm-field-error">{errors.province}</p>}
            </div>

            <div className="farm-form-field">
              <span className="farm-field-label">CÓDIGO POSTAL</span>
              <input className="farm-input" value={form.zipCode} onChange={(event) => onChange('zipCode', event.target.value)} />
            </div>

            <div className="farm-form-field">
              <span className="farm-field-label">COORDENADA X</span>
              <input className={errors.xCoordinate ? 'farm-input farm-input-error' : 'farm-input'} value={form.xCoordinate} onChange={(event) => onChange('xCoordinate', event.target.value)} />
              {errors.xCoordinate && <p className="farm-field-error">{errors.xCoordinate}</p>}
            </div>

            <div className="farm-form-field">
              <span className="farm-field-label">COORDENADA Y</span>
              <input className={errors.yCoordinate ? 'farm-input farm-input-error' : 'farm-input'} value={form.yCoordinate} onChange={(event) => onChange('yCoordinate', event.target.value)} />
              {errors.yCoordinate && <p className="farm-field-error">{errors.yCoordinate}</p>}
            </div>
          </div>
      </ModalBody>

      <ModalFooter align="end">
          <button className="secondary-button" type="button" onClick={onClose}>Cancelar</button>
          <button className="primary-button" type="button" onClick={onSubmit} disabled={submitting}>
            {submitting ? 'Guardando...' : 'Guardar cambios'}
          </button>
      </ModalFooter>
    </ModalDialog>
  );
}

function AnimalDetailModal({
  animal,
  form,
  errors,
  loading,
  saving,
  deleting,
  requestError,
  onChange,
  onClose,
  onSave,
  onDelete
}) {
  const guideField = resolveAnimalGuideField(animal, form);

  return (
    <ModalDialog size="wide" shellClassName="animal-modal">
      <ModalHeader
        icon={<Tag size={18} />}
        title="Detalle del animal"
        subtitle={animal ? `${animal.identification} · ${animal.farmName}` : 'Cargando datos del animal...'}
        onClose={onClose}
      />
      <ModalBody>
          {requestError && <div className="error-banner">{requestError}</div>}
          {loading || !animal ? (
            <div className="empty-state">Cargando detalle del animal...</div>
          ) : (
            <>
              <div className="profile-grid">
                <div>
                  <span>Explotación</span>
                  <strong>{animal.farmName}</strong>
                </div>
                <div>
                  <span>Especie</span>
                  <strong>{speciesToneMap[animal.livestockSpecies]?.label ?? animal.livestockSpecies}</strong>
                </div>
              </div>

              <div className="grid-form">
                <label>
                  Identificación / crotal
                  <input value={form.identification} onChange={(event) => onChange('identification', event.target.value)} />
                  {errors.identification && <span className="farm-inline-error">{errors.identification}</span>}
                </label>
                <label>
                  Raza
                  <input value={form.breed} onChange={(event) => onChange('breed', event.target.value)} />
                </label>
                <label>
                  Sexo
                  <div className="select-wrapper">
                    <select value={form.sex} onChange={(event) => onChange('sex', event.target.value)}>
                      <option value="">No informado</option>
                      <option value="Female">Hembra</option>
                      <option value="Male">Macho</option>
                    </select>
                    <ChevronDown size={16} />
                  </div>
                </label>
                <label>
                  Año nacimiento
                  <input type="number" min="1900" max="2100" value={form.birthYear} onChange={(event) => onChange('birthYear', event.target.value)} />
                  {errors.birthYear && <span className="farm-inline-error">{errors.birthYear}</span>}
                </label>
                <label>
                  Fecha alta
                  <input type="date" value={form.registrationDate} onChange={(event) => onChange('registrationDate', event.target.value)} />
                </label>
                <label>
                  Causa alta
                  <div className="select-wrapper">
                    <select value={form.registrationCause} onChange={(event) => onChange('registrationCause', event.target.value)}>
                      <option value="">No informada</option>
                      <option value="Entrada">Entrada (E)</option>
                      <option value="Autorreposicion">Autorreposición (A)</option>
                    </select>
                    <ChevronDown size={16} />
                  </div>
                </label>
                <label>
                  Procedencia
                  <input value={form.originCode} onChange={(event) => onChange('originCode', event.target.value)} />
                  {errors.originCode && <span className="farm-inline-error">{errors.originCode}</span>}
                </label>
                <label className="form-full">
                  Documento sanitario / guía
                  <input
                    value={guideField.value}
                    onChange={(event) => onChange('healthDocumentNumber', event.target.value)}
                    disabled={guideField.disabled}
                    placeholder={guideField.disabled ? 'No aplica' : undefined}
                  />
                </label>
              </div>

              {animal.ovinoCaprino && (
                <div className="animal-specific-block">
                  <h3>Datos ovino/caprino</h3>
                  <div className="grid-form">
                    <label>
                      Genotipado
                      <input value={form.genotyping} onChange={(event) => onChange('genotyping', event.target.value)} />
                    </label>
                    <label>
                      Alelo dominante
                      <input value={form.dominantAllele} onChange={(event) => onChange('dominantAllele', event.target.value)} />
                    </label>
                    <label>
                      Alelo bajo
                      <input value={form.lowAllele} onChange={(event) => onChange('lowAllele', event.target.value)} />
                    </label>
                  </div>
                </div>
              )}

              {animal.porcino && (
                <div className="animal-specific-block">
                  <h3>Datos porcino</h3>
                  <div className="grid-form">
                    <label>
                      Tipo de animal
                      <input value={form.animalType} onChange={(event) => onChange('animalType', event.target.value)} />
                      {errors.animalType && <span className="farm-inline-error">{errors.animalType}</span>}
                    </label>
                    <label>
                      Fecha identificación
                      <input type="date" value={form.identificationDate} onChange={(event) => onChange('identificationDate', event.target.value)} />
                    </label>
                    <label>
                      Nº registro porcino
                      <input value={form.pigRegistrationNumber} onChange={(event) => onChange('pigRegistrationNumber', event.target.value)} />
                    </label>
                    <label>
                      Marca / crotal
                      <input value={form.tag} onChange={(event) => onChange('tag', event.target.value)} />
                    </label>
                  </div>
                </div>
              )}

              <div className="animal-specific-block">
                <h3>Histórico de baja</h3>
                <div className="grid-form">
                  <label>
                    Causa de baja
                    <input value={formatAnimalCause(animal.dischargeCause)} disabled />
                  </label>
                  <label>
                    Fecha de baja
                    <input value={formatDate(animal.dischargeDate)} disabled />
                  </label>
                  <label>
                    Destino
                    <input value={animal.destinationCode ?? 'No informado'} disabled />
                  </label>
                </div>
              </div>
            </>
          )}
      </ModalBody>

      <ModalFooter>
          <button className="danger-button" type="button" onClick={onDelete} disabled={!animal || loading || saving || deleting}>
            <Trash2 size={15} />
            {deleting ? 'Eliminando...' : 'Eliminar registro'}
          </button>
          <div className="animal-modal-actions">
            <button className="secondary-button" type="button" onClick={onClose}>Cerrar</button>
            <button className="primary-button" type="button" onClick={onSave} disabled={!animal || loading || saving || deleting}>
              {saving ? 'Guardando...' : 'Guardar cambios'}
            </button>
          </div>
      </ModalFooter>
    </ModalDialog>
  );
}

function AnimalAutorrepositionModal({
  farm,
  form,
  errors,
  requestError,
  submitting,
  breedOptions,
  loadingBreedOptions,
  onChange,
  onClose,
  onSubmit
}) {
  let rangePreview = null;
  let rangePreviewError = '';

  try {
    rangePreview = buildConsecutiveIdentificationPreview(form.startIdentification, form.numberOfAnimals);
  } catch (error) {
    rangePreviewError = error.message;
  }

  return (
    <ModalDialog cardAs="form" size="wide" onSubmit={onSubmit}>
      <ModalHeader
        icon={<Tag size={18} />}
        title="Autorreposición"
        subtitle={`Alta múltiple con causa Autorreposición en ${farm.name}.`}
        onClose={onClose}
      />
      <ModalBody className="operation-modal-body">
          {requestError && <div className="error-banner">{requestError}</div>}

          <div className="info-callout">
            <Tag size={16} />
            <p>Todos los animales se registrarán con la causa de alta <strong>Autorreposición</strong> y compartirán las mismas características.</p>
          </div>

          <div className="grid-form">
            <label className="farm-form-field">
              <span className="farm-field-label">Identificación inicial <span className="farm-field-label-required">*</span></span>
              <input value={form.startIdentification} onChange={(event) => onChange('startIdentification', event.target.value)} placeholder="ES100003542349" required />
              {errors.startIdentification && <span className="farm-inline-error">{errors.startIdentification}</span>}
            </label>
            <label className="farm-form-field">
              <span className="farm-field-label">Número de animales <span className="farm-field-label-required">*</span></span>
              <input type="number" min="1" step="1" value={form.numberOfAnimals} onChange={(event) => onChange('numberOfAnimals', event.target.value)} required />
              {errors.numberOfAnimals && <span className="farm-inline-error">{errors.numberOfAnimals}</span>}
            </label>
            <label className="farm-form-field">
              <span className="farm-field-label">Raza <span className="farm-field-label-required">*</span></span>
              <div className="select-wrapper">
                <select value={form.breed} onChange={(event) => onChange('breed', event.target.value)} disabled={loadingBreedOptions} required>
                  <option value="">{loadingBreedOptions ? 'Cargando razas...' : 'Selecciona una raza'}</option>
                  {breedOptions.map((option) => (
                    <option key={option.name} value={option.name}>
                      {option.name} ({option.code})
                    </option>
                  ))}
                </select>
                <ChevronDown size={16} />
              </div>
              {errors.breed && <span className="farm-inline-error">{errors.breed}</span>}
            </label>
            <label className="farm-form-field">
              <span className="farm-field-label">Sexo <span className="farm-field-label-required">*</span></span>
              <div className="select-wrapper">
                <select value={form.sex} onChange={(event) => onChange('sex', event.target.value)} required>
                  <option value="">Selecciona sexo</option>
                  <option value="Female">Hembra</option>
                  <option value="Male">Macho</option>
                </select>
                <ChevronDown size={16} />
              </div>
              {errors.sex && <span className="farm-inline-error">{errors.sex}</span>}
            </label>
            <label className="farm-form-field">
              <span className="farm-field-label">Año nacimiento <span className="farm-field-label-required">*</span></span>
              <input type="number" min="1900" max="2100" value={form.birthYear} onChange={(event) => onChange('birthYear', event.target.value)} required />
              {errors.birthYear && <span className="farm-inline-error">{errors.birthYear}</span>}
            </label>
            <label className="farm-form-field">
              <span className="farm-field-label">Fecha alta <span className="farm-field-label-required">*</span></span>
              <input type="date" value={form.registrationDate} onChange={(event) => onChange('registrationDate', event.target.value)} required />
              {errors.registrationDate && <span className="farm-inline-error">{errors.registrationDate}</span>}
            </label>
          </div>

          <div className={rangePreview ? 'farm-settings-note' : 'info-callout info-callout-danger'}>
            {rangePreview ? (
              <>
                <strong>Rango generado</strong>
                <p>{rangePreview.count} animales: {rangePreview.firstIdentification} a {rangePreview.lastIdentification}</p>
              </>
            ) : (
              <p>{rangePreviewError || 'Introduce una identificación inicial y el número de animales para previsualizar la serie.'}</p>
            )}
          </div>

          {farm.livestockSpecies === 'Porcine' ? (
            <div className="animal-specific-block">
              <h3>Datos porcino</h3>
              <div className="grid-form">
                <label>
                  Tipo de animal
                  <input value={form.animalType} onChange={(event) => onChange('animalType', event.target.value)} />
                  {errors.animalType && <span className="farm-inline-error">{errors.animalType}</span>}
                </label>
                <label>
                  Fecha identificación
                  <input type="date" value={form.identificationDate} onChange={(event) => onChange('identificationDate', event.target.value)} />
                </label>
                <label>
                  Nº registro porcino
                  <input value={form.pigRegistrationNumber} onChange={(event) => onChange('pigRegistrationNumber', event.target.value)} />
                </label>
                <label>
                  Marca / tag
                  <input value={form.tag} onChange={(event) => onChange('tag', event.target.value)} />
                </label>
              </div>
            </div>
          ) : (
            <div className="animal-specific-block">
              <h3>Datos ovino/caprino</h3>
              <div className="grid-form">
                <label>
                  Genotipado
                  <input value={form.genotyping} onChange={(event) => onChange('genotyping', event.target.value)} />
                </label>
                <label>
                  Alelo dominante
                  <input value={form.dominantAllele} onChange={(event) => onChange('dominantAllele', event.target.value)} />
                </label>
                <label>
                  Alelo bajo
                  <input value={form.lowAllele} onChange={(event) => onChange('lowAllele', event.target.value)} />
                </label>
              </div>
            </div>
          )}
      </ModalBody>

      <ModalFooter align="end">
          <button className="secondary-button" type="button" onClick={onClose}>Cancelar</button>
          <button className="primary-button" type="submit" disabled={submitting}>
            {submitting ? 'Registrando...' : 'Crear animales'}
          </button>
      </ModalFooter>
    </ModalDialog>
  );
}

function FarmAnimalsSection({ farm, token, movementFilter, onClearMovementFilter }) {
  const [animals, setAnimals] = useState([]);
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(FARM_ANIMALS_DEFAULT_PAGE_SIZE);
  const [totalCount, setTotalCount] = useState(0);
  const [activeCount, setActiveCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [reloadKey, setReloadKey] = useState(0);
  const [detailOpen, setDetailOpen] = useState(false);
  const [selectedAnimal, setSelectedAnimal] = useState(null);
  const [animalForm, setAnimalForm] = useState(createAnimalDetailForm(null));
  const [animalFormErrors, setAnimalFormErrors] = useState({});
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailSaving, setDetailSaving] = useState(false);
  const [detailDeleting, setDetailDeleting] = useState(false);
  const [detailError, setDetailError] = useState('');
  const [success, setSuccess] = useState('');
  const [autorrepositionOpen, setAutorrepositionOpen] = useState(false);
  const [autorrepositionSubmitting, setAutorrepositionSubmitting] = useState(false);
  const [autorrepositionError, setAutorrepositionError] = useState('');
  const [autorrepositionFormErrors, setAutorrepositionFormErrors] = useState({});
  const [autorrepositionForm, setAutorrepositionForm] = useState(() => createAutorrepositionForm(farm));
  const [breedOptions, setBreedOptions] = useState([]);
  const [loadingBreedOptions, setLoadingBreedOptions] = useState(false);
  const identificationLabel = farm.livestockSpecies === 'Porcine' ? 'Lote' : 'Crotal';
  const isInitialLoading = loading && animals.length === 0 && !error;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const visiblePageNumbers = useMemo(() => {
    const maxVisiblePages = 5;
    const startPage = Math.max(1, Math.min(page - 2, totalPages - maxVisiblePages + 1));
    const endPage = Math.min(totalPages, startPage + maxVisiblePages - 1);
    return Array.from({ length: endPage - startPage + 1 }, (_, index) => startPage + index);
  }, [page, totalPages]);
  const currentRangeStart = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const currentRangeEnd = totalCount === 0 ? 0 : Math.min(page * pageSize, totalCount);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setPage(1);
      setDebouncedSearch(search.trim());
    }, FARM_ANIMALS_SEARCH_DEBOUNCE_MS);

    return () => {
      window.clearTimeout(timeoutId);
    };
  }, [search]);

  useEffect(() => {
    setPage(1);
  }, [movementFilter?.movementId]);

  useEffect(() => {
    setAutorrepositionForm(createAutorrepositionForm(farm));
  }, [farm.id, farm.livestockSpecies]);

  useEffect(() => {
    if (!autorrepositionOpen) {
      return undefined;
    }

    let cancelled = false;

    async function loadBreedOptions() {
      setLoadingBreedOptions(true);

      try {
        const response = await apiRequest(`/api/movements/breeds/${farm.livestockSpecies}`, { token });
        if (!cancelled) {
          setBreedOptions(response);
        }
      } catch (requestError) {
        if (!cancelled) {
          setAutorrepositionError(requestError.message);
          setBreedOptions([]);
        }
      } finally {
        if (!cancelled) {
          setLoadingBreedOptions(false);
        }
      }
    }

    loadBreedOptions();
    return () => {
      cancelled = true;
    };
  }, [autorrepositionOpen, farm.livestockSpecies, token]);

  async function loadAnimals() {
    setLoading(true);
    setError('');

    try {
      const params = new URLSearchParams();
      if (debouncedSearch) {
        params.set('search', debouncedSearch);
      }
      if (movementFilter?.movementId) {
        params.set('movementId', String(movementFilter.movementId));
      }
      params.set('page', String(page));
      params.set('pageSize', String(pageSize));

      const response = await apiRequest(`/api/farms/${farm.id}/animals${params.toString() ? `?${params}` : ''}`, { token });
      setAnimals(response.items);
      setTotalCount(response.totalCount);
      setActiveCount(response.activeCount);
      setPage(response.page);
    } catch (requestError) {
      setError(requestError.message);
      setAnimals([]);
      setTotalCount(0);
      setActiveCount(0);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadAnimals();
  }, [debouncedSearch, farm.id, movementFilter?.movementId, page, pageSize, reloadKey, token]);

  function updateAnimalField(field, value) {
    setAnimalForm((current) => ({ ...current, [field]: value }));
    setAnimalFormErrors((current) => {
      if (!current[field]) {
        return current;
      }

      const next = { ...current };
      delete next[field];
      return next;
    });
  }

  function openAutorrepositionModal() {
    setAutorrepositionForm(createAutorrepositionForm(farm));
    setAutorrepositionFormErrors({});
    setAutorrepositionError('');
    setAutorrepositionOpen(true);
  }

  function closeAutorrepositionModal() {
    setAutorrepositionOpen(false);
    setAutorrepositionSubmitting(false);
    setAutorrepositionFormErrors({});
    setAutorrepositionError('');
  }

  function updateAutorrepositionField(field, value) {
    setAutorrepositionForm((current) => ({ ...current, [field]: value }));
    setAutorrepositionFormErrors((current) => {
      if (!current[field]) {
        return current;
      }

      const next = { ...current };
      delete next[field];
      return next;
    });
    setAutorrepositionError('');
  }

  async function openAnimalModal(animalId) {
    setDetailOpen(true);
    setDetailLoading(true);
    setDetailSaving(false);
    setDetailDeleting(false);
    setDetailError('');
    setAnimalFormErrors({});

    try {
      const response = await apiRequest(`/api/animals/${animalId}`, { token });
      setSelectedAnimal(response);
      setAnimalForm(createAnimalDetailForm(response));
    } catch (requestError) {
      setDetailError(requestError.message);
      setSelectedAnimal(null);
      setAnimalForm(createAnimalDetailForm(null));
    } finally {
      setDetailLoading(false);
    }
  }

  function closeAnimalModal() {
    setDetailOpen(false);
    setSelectedAnimal(null);
    setAnimalForm(createAnimalDetailForm(null));
    setAnimalFormErrors({});
    setDetailError('');
    setDetailLoading(false);
    setDetailSaving(false);
    setDetailDeleting(false);
  }

  async function saveAnimalChanges() {
    if (!selectedAnimal) {
      return;
    }

    const validationErrors = validateAnimalDetailForm(animalForm, selectedAnimal.livestockSpecies);
    if (Object.keys(validationErrors).length > 0) {
      setAnimalFormErrors(validationErrors);
      return;
    }

    setDetailSaving(true);
    setDetailError('');

      try {
      const normalizedHealthDocumentNumber = animalForm.registrationCause === 'Autorreposicion'
        ? null
        : emptyToNull(animalForm.healthDocumentNumber);

      const updatedAnimal = await apiRequest(`/api/animals/${selectedAnimal.id}`, {
        method: 'PUT',
        token,
        body: {
          identification: normalizeAnimalIdentification(animalForm.identification),
          birthYear: animalForm.birthYear === '' ? null : Number(animalForm.birthYear),
          breed: emptyToNull(animalForm.breed),
          sex: emptyToNull(animalForm.sex),
          registrationDate: animalForm.registrationDate || null,
          registrationCause: animalForm.registrationCause || null,
          originCode: animalForm.originCode.trim() ? normalizeRegaCode(animalForm.originCode) : null,
          healthDocumentNumber: normalizedHealthDocumentNumber,
          ovinoCaprino: selectedAnimal.ovinoCaprino
            ? {
                speciesType: selectedAnimal.ovinoCaprino.speciesType,
                genotyping: emptyToNull(animalForm.genotyping),
                dominantAllele: emptyToNull(animalForm.dominantAllele),
                lowAllele: emptyToNull(animalForm.lowAllele)
              }
            : null,
          porcino: selectedAnimal.porcino
            ? {
                animalType: animalForm.animalType,
                identificationDate: animalForm.identificationDate || null,
                pigRegistrationNumber: emptyToNull(animalForm.pigRegistrationNumber),
                tag: emptyToNull(animalForm.tag)
              }
            : null
        }
      });

      setSelectedAnimal(updatedAnimal);
      setAnimalForm(createAnimalDetailForm(updatedAnimal));
      setReloadKey((current) => current + 1);
    } catch (requestError) {
      setDetailError(requestError.message);
    } finally {
      setDetailSaving(false);
    }
  }

  async function deleteAnimal() {
    if (!selectedAnimal) {
      return;
    }

    const confirmed = window.confirm(`Se eliminará el animal ${selectedAnimal.identification}. Esta acción no se puede deshacer.`);
    if (!confirmed) {
      return;
    }

    setDetailDeleting(true);
    setDetailError('');

    try {
      await apiRequest(`/api/animals/${selectedAnimal.id}`, { method: 'DELETE', token });
      closeAnimalModal();
      setReloadKey((current) => current + 1);
    } catch (requestError) {
      setDetailError(requestError.message);
    } finally {
      setDetailDeleting(false);
    }
  }

  async function submitAutorreposition(event) {
    event.preventDefault();

    const validationErrors = validateAutorrepositionForm(autorrepositionForm, farm.livestockSpecies);
    if (Object.keys(validationErrors).length > 0) {
      setAutorrepositionFormErrors(validationErrors);
      return;
    }

    setAutorrepositionSubmitting(true);
    setAutorrepositionError('');
    setSuccess('');

    try {
      const response = await apiRequest(`/api/farms/${farm.id}/animals/autorreposition`, {
        method: 'POST',
        token,
        body: {
          startIdentification: normalizeAnimalIdentification(autorrepositionForm.startIdentification),
          numberOfAnimals: Number(autorrepositionForm.numberOfAnimals),
          birthYear: autorrepositionForm.birthYear === '' ? null : Number(autorrepositionForm.birthYear),
          breed: emptyToNull(autorrepositionForm.breed),
          sex: emptyToNull(autorrepositionForm.sex),
          registrationDate: autorrepositionForm.registrationDate || null,
          ovinoCaprino: farm.livestockSpecies === 'Porcine'
            ? null
            : {
                speciesType: farm.livestockSpecies,
                genotyping: emptyToNull(autorrepositionForm.genotyping),
                dominantAllele: emptyToNull(autorrepositionForm.dominantAllele),
                lowAllele: emptyToNull(autorrepositionForm.lowAllele)
              },
          porcino: farm.livestockSpecies !== 'Porcine'
            ? null
            : {
                animalType: autorrepositionForm.animalType.trim(),
                identificationDate: autorrepositionForm.identificationDate || null,
                pigRegistrationNumber: emptyToNull(autorrepositionForm.pigRegistrationNumber),
                tag: emptyToNull(autorrepositionForm.tag)
              }
        }
      });

      setSuccess(`Se han creado ${response.createdAnimals} animales desde ${response.firstIdentification} hasta ${response.lastIdentification}.`);
      closeAutorrepositionModal();
      setPage(1);
      setReloadKey((current) => current + 1);
    } catch (requestError) {
      setAutorrepositionError(requestError.message);
    } finally {
      setAutorrepositionSubmitting(false);
    }
  }

  return (
    <section className="panel-card stack">
      <div className="farm-animals-header">
        <div>
          <p>{loading && !isInitialLoading ? 'Actualizando animales...' : `${activeCount} activos · ${totalCount} en total`}</p>
        </div>
        <div className="movement-toolbar-actions">
          <button className="primary-button" type="button" onClick={openAutorrepositionModal}>
            <Plus size={16} />
            Autorreposición
          </button>
        </div>
      </div>

      {movementFilter && (
        <div className="filter-summary">
          <div>
            <strong>Filtro activo:</strong> guía {movementFilter.codRemo}
          </div>
          <button className="secondary-button" type="button" onClick={onClearMovementFilter}>
            Eliminar filtro
          </button>
        </div>
      )}

      {error && <div className="error-banner">{error}</div>}
      {success && <div className="success-banner">{success}</div>}

      <div className="animal-filters farm-animals-filters">
        <div className="animal-search">
          <Search size={14} />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={`Buscar ${identificationLabel.toLowerCase()} o raza...`} />
        </div>
      </div>

      {isInitialLoading ? (
        <div className="empty-state">Cargando animales de la explotación...</div>
      ) : animals.length === 0 ? (
        <div className="empty-state">
          <Tag size={28} />
          <div>No hay animales que coincidan con los filtros.</div>
        </div>
      ) : (
        <div className="animal-table-card">
          <div className="table-scroll">
            <table className="animal-table">
              <thead>
                <tr>
                  {[identificationLabel, 'Año', 'Raza', 'Sexo', 'Causa alta', 'Procedencia', 'Causa baja', 'Destino', 'Guía entrada/salida'].map((header) => (
                    <th key={header}>{header}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {animals.map((animal) => {
                  const sexLabel = formatAnimalSex(animal.sex);
                  const breedValue = animal.breed ?? '—';
                  const registrationCauseValue = animal.registrationCause ?? '—';
                  const dischargeCauseValue = animal.dischargeCause ?? '—';
                  const guideSeriesValue = animal.entryGuideSerie || animal.exitGuideSerie
                    ? `${animal.entryGuideSerie ?? '—'} / ${animal.exitGuideSerie ?? '—'}`
                    : '—';

                  return (
                    <tr key={animal.id} onClick={() => openAnimalModal(animal.id)}>
                      <td>
                        <div className="animal-identification-cell">
                          <Tag size={13} />
                          <strong>{animal.identification}</strong>
                        </div>
                      </td>
                      <td>{animal.birthYear != null ? String(animal.birthYear) : '—'}</td>
                      <td>{breedValue}</td>
                      <td>{sexLabel}</td>
                      <td>{registrationCauseValue}</td>
                      <td>{animal.originCode ?? '—'}</td>
                      <td>{dischargeCauseValue}</td>
                      <td>{animal.destinationCode ?? '—'}</td>
                      <td>{guideSeriesValue}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <div className="animal-table-footer animal-table-footer-paginated">
            <span>{`Mostrando ${currentRangeStart}-${currentRangeEnd} de ${totalCount} animales`}</span>
            <div className="animal-pagination">
              <label className="animal-pagination-size">
                <span>Filas</span>
                <select value={pageSize} onChange={(event) => {
                  setPage(1);
                  setPageSize(Number(event.target.value));
                }}>
                  {FARM_ANIMALS_PAGE_SIZE_OPTIONS.map((option) => (
                    <option key={option} value={option}>{option}</option>
                  ))}
                </select>
              </label>
              <button type="button" className="animal-pagination-button" onClick={() => setPage((current) => Math.max(1, current - 1))} disabled={page <= 1}>
                Anterior
              </button>
              {visiblePageNumbers.map((pageNumber) => (
                <button
                  key={pageNumber}
                  type="button"
                  className={pageNumber === page ? 'animal-pagination-button animal-pagination-button-active' : 'animal-pagination-button'}
                  onClick={() => setPage(pageNumber)}
                >
                  {pageNumber}
                </button>
              ))}
              <button type="button" className="animal-pagination-button" onClick={() => setPage((current) => Math.min(totalPages, current + 1))} disabled={page >= totalPages}>
                Siguiente
              </button>
            </div>
          </div>
        </div>
      )}

      {detailOpen && (
        <AnimalDetailModal
          animal={selectedAnimal}
          form={animalForm}
          errors={animalFormErrors}
          loading={detailLoading}
          saving={detailSaving}
          deleting={detailDeleting}
          requestError={detailError}
          onChange={updateAnimalField}
          onClose={closeAnimalModal}
          onSave={saveAnimalChanges}
          onDelete={deleteAnimal}
        />
      )}

      {autorrepositionOpen && (
        <AnimalAutorrepositionModal
          farm={farm}
          form={autorrepositionForm}
          errors={autorrepositionFormErrors}
          requestError={autorrepositionError}
          submitting={autorrepositionSubmitting}
          breedOptions={breedOptions}
          loadingBreedOptions={loadingBreedOptions}
          onChange={updateAutorrepositionField}
          onClose={closeAutorrepositionModal}
          onSubmit={submitAutorreposition}
        />
      )}
    </section>
  );
}

function FarmBirthsSection({ farm, token }) {
  const [births, setBirths] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [form, setForm] = useState({
    birthDate: new Date().toISOString().slice(0, 10),
    offspringNumber: '1',
    birthWeight: '',
    observations: ''
  });

  async function loadBirths() {
    setLoading(true);
    setError('');

    try {
      const response = await apiRequest(`/api/farms/${farm.id}/births`, { token });
      setBirths(response);
    } catch (requestError) {
      setError(requestError.message);
      setBirths([]);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadBirths();
  }, [farm.id, token]);

  async function handleSubmit(event) {
    event.preventDefault();
    setError('');
    setSuccess('');

    const offspringNumber = Number(form.offspringNumber);
    const birthWeight = parsePositiveNumber(form.birthWeight);
    if (!form.birthDate || !Number.isInteger(offspringNumber) || offspringNumber <= 0 || (birthWeight !== null && birthWeight < 0)) {
      setError('Revisa fecha, número de crías y peso. El número de crías debe ser positivo.');
      return;
    }

    setSubmitting(true);
    try {
      await apiRequest(`/api/farms/${farm.id}/births`, {
        method: 'POST',
        token,
        body: {
          birthDate: form.birthDate,
          offspringNumber,
          birthWeight,
          observations: form.observations.trim() || null
        }
      });
      setSuccess('Nacimiento registrado correctamente.');
      setModalOpen(false);
      setForm({ birthDate: new Date().toISOString().slice(0, 10), offspringNumber: '1', birthWeight: '', observations: '' });
      await loadBirths();
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setSubmitting(false);
    }
  }

  const totalOffspring = births.reduce((sum, birth) => sum + birth.offspringNumber, 0);

  if (loading) {
    return <div className="panel-card empty-state">Cargando nacimientos...</div>;
  }

  return (
    <section className="farm-operations-layout">
      <article className="panel-card stack">
        <div className="section-heading-row">
        <div>
          <h2>Nacimientos</h2>
          <p>{births.length} partos registrados · {totalOffspring} crías declaradas</p>
        </div>
        <button className="primary-button" type="button" onClick={() => setModalOpen(true)}>
          <Plus size={16} />
          Registrar nacimiento
        </button>
      </div>

        {error && <div className="error-banner">{error}</div>}
        {success && <div className="success-banner">{success}</div>}

        {births.length === 0 ? (
          <div className="empty-state">
            <Sprout size={28} />
            <div>No hay nacimientos registrados para esta explotación.</div>
          </div>
        ) : (
          <div className="birth-card-list">
            {births.map((birth) => (
              <article key={birth.id} className="operation-record-card">
                <div>
                  <strong>Parto registrado · {formatDate(birth.birthDate)}</strong>
                  <span>{birth.offspringNumber} cría{birth.offspringNumber === 1 ? '' : 's'}</span>
                </div>
                <p>Peso medio: {birth.birthWeight == null ? 'No informado' : `${birth.birthWeight} kg`}</p>
                {birth.observations && <p>{birth.observations}</p>}
              </article>
            ))}
          </div>
        )}

        {modalOpen && (
          <ModalDialog cardAs="form" size="wide" onSubmit={handleSubmit}>
            <ModalHeader
              icon={<Sprout size={18} />}
              title="Nuevo nacimiento"
              subtitle={farm.name}
              onClose={() => setModalOpen(false)}
            />
            <ModalBody className="operation-modal-body">
                <label>
                  <span>Fecha de parto *</span>
                  <input type="date" value={form.birthDate} onChange={(event) => setForm({ ...form, birthDate: event.target.value })} />
                </label>
                <label>
                  <span>Número de crías *</span>
                  <input type="number" min="1" value={form.offspringNumber} onChange={(event) => setForm({ ...form, offspringNumber: event.target.value })} />
                </label>
                <label>
                  <span>Peso medio al nacimiento</span>
                  <input type="number" min="0" step="0.001" value={form.birthWeight} onChange={(event) => setForm({ ...form, birthWeight: event.target.value })} placeholder="3.0 kg" />
                </label>
                <label className="operation-form-wide">
                  <span>Observaciones</span>
                  <textarea value={form.observations} onChange={(event) => setForm({ ...form, observations: event.target.value })} placeholder="Notas sobre el parto, complicaciones, etc." />
                </label>
            </ModalBody>
            <ModalFooter align="end">
                <button className="secondary-button" type="button" onClick={() => setModalOpen(false)}>Cancelar</button>
                <button className="primary-button" type="submit" disabled={submitting}>{submitting ? 'Guardando...' : 'Guardar nacimiento'}</button>
            </ModalFooter>
          </ModalDialog>
        )}
      </article>
    </section>
  );
}

function FarmDeathsSection({ farm, token }) {
  const isPorcineFarm = farm.livestockSpecies === 'Porcine';
  const [deaths, setDeaths] = useState([]);
  const [search, setSearch] = useState('');
  const [destination, setDestination] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [form, setForm] = useState({
    identification: '',
    dischargeDate: new Date().toISOString().slice(0, 10),
    destinationCode: ''
  });

  async function loadDeaths() {
    setLoading(true);
    setError('');

    try {
      const response = await apiRequest(`/api/farms/${farm.id}/deaths`, { token });
      setDeaths(response);
    } catch (requestError) {
      setError(requestError.message);
      setDeaths([]);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadDeaths();
  }, [farm.id, token]);

  async function handleSubmit(event) {
    event.preventDefault();
    setError('');
    setSuccess('');

    if (!form.identification.trim() || !form.dischargeDate || !form.destinationCode) {
      setError('Crotal, fecha y destino son obligatorios.');
      return;
    }

    if (!isValidAnimalIdentification(farm.livestockSpecies, form.identification)) {
      setError(getAnimalIdentificationFormatMessage(farm.livestockSpecies));
      return;
    }

    setSubmitting(true);
    try {
      await apiRequest(`/api/farms/${farm.id}/deaths`, {
        method: 'POST',
        token,
        body: {
          identification: normalizeAnimalIdentification(form.identification),
          dischargeDate: form.dischargeDate,
          destinationCode: form.destinationCode
        }
      });
      setSuccess('Baja por muerte registrada correctamente.');
      setModalOpen(false);
      setForm({ identification: '', dischargeDate: new Date().toISOString().slice(0, 10), destinationCode: '' });
      await loadDeaths();
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setSubmitting(false);
    }
  }

  const filteredDeaths = deaths.filter((death) => {
    const matchesSearch = !search.trim() || `${death.identification} ${death.breed ?? ''}`.toLowerCase().includes(search.trim().toLowerCase());
    const matchesDestination = !destination || death.destinationCode === destination;
    return matchesSearch && matchesDestination;
  });
  const destinationOptions = getDeathDestinationOptions(farm.livestockSpecies);
  const sandachCount = deaths.filter((death) => death.destinationCode === 'SANDACH').length;
  const merCount = deaths.filter((death) => death.destinationCode === 'MER').length;

  if (loading) {
    return <div className="panel-card empty-state">Cargando bajas por muerte...</div>;
  }

  return (
    <section className="panel-card stack">

      <div className="farm-detail-metrics">
        <SummaryMetric label="Total bajas por muerte" value={deaths.length} />
        {!isPorcineFarm && <SummaryMetric label="SANDACH" value={sandachCount} />}
        <SummaryMetric label="MER" value={merCount} />
        <SummaryMetric label="Explotación" value={farm.name} />
      </div>

      {error && <div className="error-banner">{error}</div>}
      {success && <div className="success-banner">{success}</div>}

      <div className="section-heading-row">
        <div className="animal-filters farm-animals-filters">
          <div className="animal-search">
            <Search size={14} />
            <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Buscar por crotal o raza..." />
          </div>
          <select value={destination} onChange={(event) => setDestination(event.target.value)}>
            <option value="">Todos los destinos</option>
            {destinationOptions.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
        </div>
        <button className="primary-button" type="button" onClick={() => setModalOpen(true)}>
          <Plus size={16} />
          Registrar baja
        </button>
      </div>

      <div className="animal-table-card">
        <div className="table-scroll">
          <table className="animal-table">
            <thead>
              <tr>
                {['Crotal', 'Raza / sexo / año', 'Fecha baja', 'Causa baja', 'Destino'].map((header) => <th key={header}>{header}</th>)}
              </tr>
            </thead>
            <tbody>
              {filteredDeaths.map((death) => (
                <tr key={death.animalId}>
                  <td><div className="animal-identification-cell"><Tag size={13} /><strong>{death.identification}</strong></div></td>
                  <td>{death.breed ?? '—'} · {death.sex ?? 'Sexo no informado'} · {death.birthYear ?? 'Año no informado'}</td>
                  <td>{formatDate(death.dischargeDate)}</td>
                  <td><span className="animal-chip death-chip">Muerte</span></td>
                  <td>{death.destinationCode ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="animal-table-footer">{filteredDeaths.length} de {deaths.length} bajas por muerte</div>
      </div>

      {modalOpen && (
        <ModalDialog cardAs="form" size="wide" onSubmit={handleSubmit}>
          <ModalHeader
            icon={<Skull size={18} />}
            title="Registrar baja por muerte"
            subtitle={farm.name}
            onClose={() => setModalOpen(false)}
          />
          <ModalBody className="operation-modal-body">
              <label>
                <span>Crotal / identificación *</span>
                <input value={form.identification} onChange={(event) => setForm({ ...form, identification: event.target.value })} placeholder={farm.livestockSpecies === 'Porcine' ? 'Ej: GT1800001004' : 'Ej: ES0600005831'} />
              </label>
              <label>
                <span>Fecha de baja *</span>
                <input type="date" value={form.dischargeDate} onChange={(event) => setForm({ ...form, dischargeDate: event.target.value })} />
              </label>
              <label>
                <span>Destino *</span>
                <select value={form.destinationCode} onChange={(event) => setForm({ ...form, destinationCode: event.target.value })}>
                  <option value="">Seleccionar...</option>
                  {destinationOptions.map((option) => (
                    <option key={option.value} value={option.value}>{option.label}</option>
                  ))}
                </select>
              </label>
              <div className="info-callout">
                <Skull size={18} />
                <p>
                  {farm.livestockSpecies === 'Porcine'
                    ? 'En porcino, la baja por muerte solo puede registrarse con destino MER.'
                  : 'La causa oficial guardada será Baja - Causa Muerte. No se modificarán animales dados de baja por Salida.'}
                </p>
              </div>
          </ModalBody>
          <ModalFooter align="end">
              <button className="secondary-button" type="button" onClick={() => setModalOpen(false)}>Cancelar</button>
              <button className="primary-button" type="submit" disabled={submitting}>{submitting ? 'Guardando...' : 'Guardar baja'}</button>
          </ModalFooter>
        </ModalDialog>
      )}
    </section>
  );
}

function FarmVaccinationsSection({ farm, token }) {
  const [vaccinations, setVaccinations] = useState([]);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [editingVaccination, setEditingVaccination] = useState(null);
  const [form, setForm] = useState(createVaccinationFormState);
  const identificationLabel = farm.livestockSpecies === 'Porcine' ? 'Lote' : 'Crotal';
  const todayIso = new Date().toISOString().slice(0, 10);

  async function loadVaccinations() {
    setLoading(true);
    setError('');

    try {
      const response = await apiRequest(`/api/farms/${farm.id}/vaccinations`, { token });
      setVaccinations(response);
    } catch (requestError) {
      setError(requestError.message);
      setVaccinations([]);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadVaccinations();
  }, [farm.id, token]);

  function openCreateModal() {
    setEditingVaccination(null);
    setForm(createVaccinationFormState());
    setModalOpen(true);
  }

  function openEditModal(vaccination) {
    setEditingVaccination(vaccination);
    setForm({
      animalIdentification: vaccination.animalIdentification ?? '',
      vaccinationDate: vaccination.vaccinationDate,
      nextDose: vaccination.nextDose ?? '',
      vaccinationType: vaccination.vaccinationType ?? '',
      observations: vaccination.observations ?? ''
    });
    setModalOpen(true);
  }

  function closeModal() {
    setModalOpen(false);
    setEditingVaccination(null);
    setForm(createVaccinationFormState());
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setError('');
    setSuccess('');

    if (!form.animalIdentification.trim() || !form.vaccinationDate || !form.vaccinationType.trim()) {
      setError(`Debes indicar ${identificationLabel.toLowerCase()}, fecha y tipo de vacunación.`);
      return;
    }

    if (!isValidAnimalIdentification(farm.livestockSpecies, form.animalIdentification)) {
      setError(getAnimalIdentificationFormatMessage(farm.livestockSpecies));
      return;
    }

    if (form.nextDose && form.nextDose < form.vaccinationDate) {
      setError('La próxima dosis no puede ser anterior a la fecha de vacunación.');
      return;
    }

    setSubmitting(true);
    try {
      const body = {
        animalIdentification: normalizeAnimalIdentification(form.animalIdentification),
        vaccinationDate: form.vaccinationDate,
        nextDose: emptyToNull(form.nextDose),
        vaccinationType: form.vaccinationType.trim(),
        observations: emptyToNull(form.observations)
      };

      if (editingVaccination) {
        await apiRequest(`/api/farms/${farm.id}/vaccinations/${editingVaccination.id}`, {
          method: 'PUT',
          token,
          body
        });
        setSuccess('Vacunación actualizada correctamente.');
      } else {
        await apiRequest(`/api/farms/${farm.id}/vaccinations`, {
          method: 'POST',
          token,
          body
        });
        setSuccess('Vacunación registrada correctamente.');
      }

      closeModal();
      await loadVaccinations();
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDelete(vaccination) {
    const confirmed = window.confirm(`Se eliminará la vacunación "${vaccination.vaccinationType}" del ${formatDate(vaccination.vaccinationDate)} para ${vaccination.animalIdentification}.`);
    if (!confirmed) {
      return;
    }

    setError('');
    setSuccess('');

    try {
      await apiRequest(`/api/farms/${farm.id}/vaccinations/${vaccination.id}`, {
        method: 'DELETE',
        token
      });
      setSuccess('Vacunación eliminada correctamente.');
      await loadVaccinations();
    } catch (requestError) {
      setError(requestError.message);
    }
  }

  const filteredVaccinations = vaccinations.filter((vaccination) => {
    const haystack = `${vaccination.animalIdentification} ${vaccination.breed ?? ''} ${vaccination.vaccinationType}`.toLowerCase();
    return !search.trim() || haystack.includes(search.trim().toLowerCase());
  });
  const scheduledDoses = vaccinations.filter((vaccination) => vaccination.nextDose).length;
  const overdueDoses = vaccinations.filter((vaccination) => vaccination.nextDose && vaccination.nextDose < todayIso).length;
  const vaccinatedAnimals = new Set(vaccinations.map((vaccination) => vaccination.animalId)).size;

  if (loading) {
    return <div className="panel-card empty-state">Cargando vacunaciones...</div>;
  }

  return (
    <section className="panel-card stack">
      <div className="info-callout">
        <Shield size={18} />
        <p>Histórico sanitario de la explotación. Registra cada pauta aplicada y, si procede, la fecha estimada de la siguiente dosis.</p>
      </div>

      <div className="farm-detail-metrics">
        <SummaryMetric label="Vacunaciones" value={vaccinations.length} />
        <SummaryMetric label="Animales vacunados" value={vaccinatedAnimals} />
        <SummaryMetric label="Próximas dosis" value={scheduledDoses} />
        <SummaryMetric label="Dosis vencidas" value={overdueDoses} />
      </div>

      <div className="section-heading-row">
        <div>
          <h2>Vacunación</h2>
          <p>Registro de vacunas aplicadas por animal dentro de la explotación.</p>
        </div>
        <button className="primary-button" type="button" onClick={openCreateModal}>
          <Plus size={16} />
          Registrar vacunación
        </button>
      </div>

      {error && <div className="error-banner">{error}</div>}
      {success && <div className="success-banner">{success}</div>}

      <div className="animal-filters farm-animals-filters">
        <div className="animal-search">
          <Search size={14} />
          <input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={`Buscar por ${identificationLabel.toLowerCase()}, raza o vacuna...`}
          />
        </div>
      </div>

      {filteredVaccinations.length === 0 ? (
        <div className="empty-state">
          <Shield size={28} />
          <div>{vaccinations.length === 0 ? 'No hay vacunaciones registradas.' : 'No hay vacunaciones que coincidan con la búsqueda.'}</div>
        </div>
      ) : (
        <div className="animal-table-card">
          <div className="table-scroll">
            <table className="animal-table">
              <thead>
                <tr>
                  {[identificationLabel, 'Raza', 'Vacuna', 'Fecha aplicada', 'Próxima dosis', 'Observaciones', 'Acciones'].map((header) => (
                    <th key={header}>{header}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {filteredVaccinations.map((vaccination) => (
                  <tr key={vaccination.id}>
                    <td><strong>{vaccination.animalIdentification}</strong></td>
                    <td>{vaccination.breed ?? '—'}</td>
                    <td><span className="vaccination-type-chip">{vaccination.vaccinationType}</span></td>
                    <td>{formatDate(vaccination.vaccinationDate)}</td>
                    <td>{formatDate(vaccination.nextDose)}</td>
                    <td>{vaccination.observations ?? '—'}</td>
                    <td>
                      <div className="table-row-actions">
                        <button className="secondary-button table-action-button" type="button" onClick={() => openEditModal(vaccination)}>
                          <Edit3 size={14} />
                          Editar
                        </button>
                        <button className="danger-button table-action-button" type="button" onClick={() => handleDelete(vaccination)}>
                          <Trash2 size={14} />
                          Eliminar
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="animal-table-footer">{filteredVaccinations.length} vacunaciones</div>
        </div>
      )}

      {modalOpen && (
        <ModalDialog cardAs="form" size="wide" onSubmit={handleSubmit}>
          <ModalHeader
            icon={<Shield size={18} />}
            title={editingVaccination ? 'Editar vacunación' : 'Nueva vacunación'}
            subtitle={farm.name}
            onClose={closeModal}
          />
          <ModalBody className="operation-modal-body">
              <label>
                <span>{identificationLabel} *</span>
                <input
                  value={form.animalIdentification}
                  onChange={(event) => setForm({ ...form, animalIdentification: event.target.value })}
                  placeholder={farm.livestockSpecies === 'Porcine' ? 'Ej: GT1800001004' : 'Ej: ES0600005831'}
                />
              </label>
              <label>
                <span>Tipo de vacunación *</span>
                <input
                  value={form.vaccinationType}
                  onChange={(event) => setForm({ ...form, vaccinationType: event.target.value })}
                  placeholder="Ej: Lengua azul, Aujeszky, clostridios..."
                />
              </label>
              <label>
                <span>Fecha aplicada *</span>
                <input type="date" value={form.vaccinationDate} onChange={(event) => setForm({ ...form, vaccinationDate: event.target.value })} />
              </label>
              <label>
                <span>Próxima dosis</span>
                <input type="date" value={form.nextDose} onChange={(event) => setForm({ ...form, nextDose: event.target.value })} />
              </label>
              <label className="operation-form-wide">
                <span>Observaciones</span>
                <textarea
                  value={form.observations}
                  onChange={(event) => setForm({ ...form, observations: event.target.value })}
                  placeholder="Lote de vacuna, reacción observada, pauta aplicada..."
                />
              </label>
          </ModalBody>
          <ModalFooter align="end">
              <button className="secondary-button" type="button" onClick={closeModal}>Cancelar</button>
              <button className="primary-button" type="submit" disabled={submitting}>
                {submitting ? 'Guardando...' : editingVaccination ? 'Guardar cambios' : 'Guardar vacunación'}
              </button>
          </ModalFooter>
        </ModalDialog>
      )}
    </section>
  );
}

function FarmCensusBalancesSection({ farm, token }) {
  const [activeSubTab, setActiveSubTab] = useState('census');
  const [year, setYear] = useState(currentYear);
  const [census, setCensus] = useState(null);
  const [balance, setBalance] = useState(null);
  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState({
    nonReproductiveUnder4Months: '0',
    nonReproductiveBetween4And12Months: '0',
    reproductiveFemales: '0',
    reproductiveMales: '0',
    boars: '0',
    sowsForLive: '0',
    sowsReposition: '0',
    malesReposition: '0',
    piglets: '0',
    rears: '0',
    baits: '0'
  });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [submitting, setSubmitting] = useState(false);

  async function loadData(targetYear = year) {
    setLoading(true);
    setError('');

    try {
      const [censusResponse, balanceResponse] = await Promise.all([
        apiRequest(`/api/farms/${farm.id}/census?year=${targetYear}`, { token }),
        apiRequest(`/api/farms/${farm.id}/balances?year=${targetYear}`, { token })
      ]);
      setCensus(censusResponse);
      setBalance(balanceResponse);
      setForm({
        nonReproductiveUnder4Months: String(censusResponse.nonReproductiveUnder4Months),
        nonReproductiveBetween4And12Months: String(censusResponse.nonReproductiveBetween4And12Months),
        reproductiveFemales: String(censusResponse.reproductiveFemales),
        reproductiveMales: String(censusResponse.reproductiveMales),
        boars: String(censusResponse.boars),
        sowsForLive: String(censusResponse.sowsForLive),
        sowsReposition: String(censusResponse.sowsReposition),
        malesReposition: String(censusResponse.malesReposition),
        piglets: String(censusResponse.piglets),
        rears: String(censusResponse.rears),
        baits: String(censusResponse.baits)
      });
    } catch (requestError) {
      setError(requestError.message);
      setCensus(null);
      setBalance(null);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData(year);
  }, [farm.id, token, year]);

  async function handleSave(event) {
    event.preventDefault();
    setError('');
    setSuccess('');

    const payload = {
      nonReproductiveUnder4Months: Number(form.nonReproductiveUnder4Months),
      nonReproductiveBetween4And12Months: Number(form.nonReproductiveBetween4And12Months),
      reproductiveFemales: Number(form.reproductiveFemales),
      reproductiveMales: Number(form.reproductiveMales),
      boars: Number(form.boars),
      sowsForLive: Number(form.sowsForLive),
      sowsReposition: Number(form.sowsReposition),
      malesReposition: Number(form.malesReposition),
      piglets: Number(form.piglets),
      rears: Number(form.rears),
      baits: Number(form.baits)
    };

    if (Object.values(payload).some((value) => !Number.isInteger(value) || value < 0)) {
      setError('Todas las categorías del censo deben ser enteros no negativos.');
      return;
    }

    setSubmitting(true);
    try {
      const response = await apiRequest(`/api/farms/${farm.id}/census?year=${year}`, {
        method: 'PUT',
        token,
        body: payload
      });
      setCensus(response);
      setSuccess(`Censo anual ${year} guardado correctamente.`);
      setEditing(false);
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) {
    return <div className="panel-card empty-state">Cargando censos y balances...</div>;
  }

  const isPorcine = farm.livestockSpecies === 'Porcine';
  const total = census?.total ?? 0;
  const censusCards = isPorcine
    ? [
        { label: 'Verracos', value: census?.boars ?? 0, color: '#1d4ed8', bg: '#dbeafe' },
        { label: 'Cerdas vida', value: census?.sowsForLive ?? 0, color: '#be185d', bg: '#fce7f3' },
        { label: 'Hembras reposición', value: census?.sowsReposition ?? 0, color: '#d97706', bg: '#fef3c7' },
        { label: 'Machos reposición', value: census?.malesReposition ?? 0, color: '#2563eb', bg: '#dbeafe' },
        { label: 'Lechones', value: census?.piglets ?? 0, color: '#7c3aed', bg: '#ede9fe' },
        { label: 'Recría', value: census?.rears ?? 0, color: '#0f766e', bg: '#ccfbf1' },
        { label: 'Cebo', value: census?.baits ?? 0, color: '#9d174d', bg: '#fce7f3' }
      ]
    : [
        { label: 'Reproductores macho', value: census?.reproductiveMales ?? 0, color: '#1d4ed8', bg: '#dbeafe' },
        { label: 'Reproductores hembra', value: census?.reproductiveFemales ?? 0, color: '#be185d', bg: '#fce7f3' },
        { label: 'Menores de 4 meses', value: census?.nonReproductiveUnder4Months ?? 0, color: '#d97706', bg: '#fef3c7' },
        { label: 'De 4 a 12 meses', value: census?.nonReproductiveBetween4And12Months ?? 0, color: '#7c3aed', bg: '#ede9fe' }
      ];
  const safeDivisor = total || 1;
  const censusCardsWithPct = censusCards.map((card) => ({
    ...card,
    pct: Math.round((card.value / safeDivisor) * 100)
  }));

  const balanceReg = balance?.registrations ?? 0;
  const balanceBirths = balance?.births ?? 0;
  const balanceDeaths = balance?.deaths ?? 0;
  const balanceDepartures = balance?.departures ?? 0;
  const balanceNet = balance?.balance ?? 0;
  const balanceEntries = balanceReg;
  const balanceExits = balanceDepartures;
  const censusFields = isPorcine
    ? [
        ['boars', 'Verracos'],
        ['sowsForLive', 'Cerdas vida'],
        ['sowsReposition', 'Hembras reposición'],
        ['malesReposition', 'Machos reposición'],
        ['piglets', 'Lechones'],
        ['rears', 'Recría'],
        ['baits', 'Cebo']
      ]
    : [
        ['nonReproductiveUnder4Months', 'No reproductores <4 meses'],
        ['nonReproductiveBetween4And12Months', 'No reproductores 4-12 meses'],
        ['reproductiveFemales', 'Hembras reproductoras'],
        ['reproductiveMales', 'Machos reproductores']
      ];

  const balanceMetrics = [
    { label: 'Altas', value: `+${balanceReg}`, color: '#2F6B4F', bg: '#DDEBDF' },
    { label: 'Bajas', value: `-${balanceDeaths}`, color: '#dc2626', bg: '#fee2e2' },
    { label: 'Nacimientos', value: `+${balanceBirths}`, color: '#d97706', bg: '#fef3c7' },
    { label: 'Mov. entrada', value: `+${balanceEntries}`, color: '#1d4ed8', bg: '#dbeafe' },
    { label: 'Mov. salida', value: `-${balanceExits}`, color: '#f97316', bg: '#ffedd5' },
    { label: 'Balance', value: balanceNet >= 0 ? `+${balanceNet}` : `${balanceNet}`, color: balanceNet >= 0 ? '#2F6B4F' : '#dc2626', bg: balanceNet >= 0 ? '#DDEBDF' : '#fee2e2' }
  ];

  const chartData = (balance?.months ?? []).map((month) => ({
    mes: monthLabels[month.month - 1],
    altas: month.registrations ?? 0,
    bajas: month.deaths ?? 0,
    nacimientos: month.births ?? 0
  }));

  return (
    <section className="panel-card stack">
      <div className="section-heading-row">
        <div>
          <h2>Censos y balances</h2>
          <p>Censo anual con histórico por año y balance derivado de eventos.</p>
        </div>
        <select value={year} onChange={(event) => setYear(Number(event.target.value))}>
          {Array.from(new Set([currentYear, currentYear - 1, currentYear - 2, ...(census?.availableYears ?? [])])).sort((a, b) => b - a).map((availableYear) => (
            <option key={availableYear} value={availableYear}>{availableYear}</option>
          ))}
        </select>
      </div>

      {error && <div className="error-banner">{error}</div>}
      {success && <div className="success-banner">{success}</div>}

      <div className="census-subtab-row">
        <button type="button" className={activeSubTab === 'census' ? 'census-subtab-active' : ''} onClick={() => setActiveSubTab('census')}>Censos</button>
        <button type="button" className={activeSubTab === 'balances' ? 'census-subtab-active' : ''} onClick={() => setActiveSubTab('balances')}>Balances</button>
      </div>

      {activeSubTab === 'census' && !editing && (
        <div className="census-visual-card">
          <div className="census-visual-header">
            <div>
              <h3 className="census-visual-title">Censo actual</h3>
              <p className="census-visual-subtitle">Año: {year} · {speciesToneMap[farm.livestockSpecies]?.label ?? farm.livestockSpecies}</p>
            </div>
            <div className="census-visual-total">
              <span className="census-visual-total-label">TOTAL ANIMALES</span>
              <span className="census-visual-total-value">{total}</span>
            </div>
          </div>

          <div className="census-visual-section">
            <span className="census-visual-section-label">{isPorcine ? 'TIPOS DE ANIMAL' : 'DISTRIBUCIÓN DEL CENSO'}</span>
            <div className="census-visual-categories">
              {censusCardsWithPct.map((cat) => (
                <div key={cat.label} className="census-category-card" style={{ background: cat.bg }}>
                  <div className="census-category-top">
                    <span className="census-category-value" style={{ color: cat.color }}>{cat.value}</span>
                    <span className="census-category-pct" style={{ color: cat.color }}>{cat.pct}%</span>
                  </div>
                  <span className="census-category-label" style={{ color: cat.color }}>{cat.label}</span>
                  <div className="census-category-bar" style={{ background: `${cat.color}30` }}>
                    <div className="census-category-bar-fill" style={{ width: `${cat.pct}%`, background: cat.color }} />
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="census-distribution">
            <span className="census-visual-section-label">DISTRIBUCIÓN VISUAL</span>
            <div className="census-distribution-bar">
              {censusCardsWithPct.map((cat) => (
                <div key={cat.label} style={{ width: `${cat.pct}%`, background: cat.color }} />
              ))}
            </div>
            <div className="census-distribution-legend">
              {censusCardsWithPct.map((cat) => (
                <div key={cat.label} className="census-legend-item">
                  <span className="census-legend-dot" style={{ background: cat.color }} />
                  <span>{cat.label}</span>
                </div>
              ))}
            </div>
          </div>

          <div className="census-edit-row">
            <button className="secondary-button" type="button" onClick={() => setEditing(true)}>
              <Edit3 size={14} />
              Editar censo
            </button>
          </div>
        </div>
      )}

      {activeSubTab === 'census' && editing && (
        <form className="census-grid" onSubmit={handleSave}>
          <article className="census-total-card">
            <span>Censo anual {year}</span>
            <strong>{total}</strong>
            <p>animales declarados</p>
          </article>
          {censusFields.map(([field, label]) => (
            <label key={field} className="census-input-card">
              <span>{label}</span>
              <input type="number" min="0" value={form[field]} onChange={(event) => setForm({ ...form, [field]: event.target.value })} />
            </label>
          ))}
          <div className="operation-form-actions census-actions">
            <button className="secondary-button" type="button" onClick={() => setEditing(false)}>Cancelar</button>
            <button className="primary-button" type="submit" disabled={submitting}>
              {submitting ? 'Guardando...' : 'Guardar censo anual'}
            </button>
          </div>
        </form>
      )}

      {activeSubTab === 'balances' && balance && (
        <div className="stack">
          <div className="census-visual-card">
            <h3 className="census-visual-title">Actividad mensual</h3>
            <div className="census-chart-wrapper">
              <ResponsiveContainer width="100%" height={200}>
                <BarChart data={chartData}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#F0F2F0" />
                  <XAxis dataKey="mes" tick={{ fill: '#637168', fontSize: 12 }} axisLine={false} tickLine={false} />
                  <YAxis tick={{ fill: '#637168', fontSize: 12 }} axisLine={false} tickLine={false} />
                  <Tooltip contentStyle={{ borderRadius: 8, border: '1px solid #D7DED8', fontSize: 12 }} />
                  <Bar dataKey="altas" name="Altas" fill="#2F6B4F" radius={[4, 4, 0, 0]} />
                  <Bar dataKey="bajas" name="Bajas" fill="#dc2626" radius={[4, 4, 0, 0]} />
                  <Bar dataKey="nacimientos" name="Nacimientos" fill="#E7B84C" radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>

          <div className="census-visual-card">
            <div className="balance-card-header">
              <h3 className="census-visual-title">Balance {monthLabels[new Date().getMonth()]} {year}</h3>
            </div>
            <div className="balance-metrics-grid">
              {balanceMetrics.map((item) => (
                <div key={item.label} className="balance-metric-tile" style={{ background: item.bg }}>
                  <span className="balance-metric-value" style={{ color: item.color }}>{item.value}</span>
                  <span className="balance-metric-label" style={{ color: item.color }}>{item.label}</span>
                </div>
              ))}
            </div>
            <div className="balance-trend-row">
              {balanceNet >= 0 ? <TrendingUp size={16} className="balance-trend-icon-positive" /> : <TrendingDown size={16} className="balance-trend-icon-negative" />}
              <span className={balanceNet >= 0 ? 'balance-trend-text-positive' : 'balance-trend-text-negative'}>
                Balance {balanceNet >= 0 ? 'positivo' : 'negativo'} este mes
              </span>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}

function FarmBookSection({ farm, token }) {
  const [preview, setPreview] = useState(null);
  const [selectedSectionIds, setSelectedSectionIds] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [pdfPreviewPages, setPdfPreviewPages] = useState([]);
  const [pdfPreviewTotalPages, setPdfPreviewTotalPages] = useState(0);
  const [pdfPreviewLoading, setPdfPreviewLoading] = useState(false);
  const [pdfPreviewError, setPdfPreviewError] = useState('');
  const [downloading, setDownloading] = useState(false);
  const [printing, setPrinting] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function loadPreview() {
      setLoading(true);
      setError('');

      try {
        const response = await apiRequest(`/api/farms/${farm.id}/book/preview`, { token });
        if (!cancelled) {
          setPreview(response);
          setSelectedSectionIds(response.sections.map((section) => section.id));
        }
      } catch (requestError) {
        if (!cancelled) {
          setError(requestError.message);
          setPreview(null);
          setSelectedSectionIds([]);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    loadPreview();

    return () => {
      cancelled = true;
    };
  }, [farm.id, token]);

  const orderedSelectedSectionIds = useMemo(
    () => preview?.sections.filter((section) => selectedSectionIds.includes(section.id)).map((section) => section.id) ?? [],
    [preview, selectedSectionIds]
  );

  const selectedSectionCount = orderedSelectedSectionIds.length;
  const totalSectionCount = preview?.sections.length ?? 0;
  const canGeneratePdf = selectedSectionCount > 0 && !loading;

  function toggleSection(sectionId) {
    setSelectedSectionIds((current) => (
      current.includes(sectionId)
        ? current.filter((entry) => entry !== sectionId)
        : [...current, sectionId]
    ));
  }

  function selectAllSections() {
    setSelectedSectionIds(preview?.sections.map((section) => section.id) ?? []);
  }

  function clearSelectedSections() {
    setSelectedSectionIds([]);
  }

  useEffect(() => {
    if (!preview) {
      setPdfPreviewPages([]);
      setPdfPreviewTotalPages(0);
      setPdfPreviewLoading(false);
      setPdfPreviewError('');
      return undefined;
    }

    if (orderedSelectedSectionIds.length === 0) {
      setPdfPreviewPages([]);
      setPdfPreviewTotalPages(0);
      setPdfPreviewLoading(false);
      setPdfPreviewError('');
      return undefined;
    }

    let cancelled = false;
    let loadingTask = null;
    let pdfDocument = null;
    const abortController = new AbortController();

    async function renderPdfPreview() {
      setPdfPreviewLoading(true);
      setPdfPreviewError('');

      try {
        const { blob } = await apiBlobRequest(buildBookPdfPath(farm.id, orderedSelectedSectionIds), {
          token,
          signal: abortController.signal
        });

        if (cancelled || abortController.signal.aborted) {
          return;
        }

        const [{ GlobalWorkerOptions, getDocument }, workerModule] = await Promise.all([
          import('pdfjs-dist'),
          import('pdfjs-dist/build/pdf.worker.min.mjs?url')
        ]);
        GlobalWorkerOptions.workerSrc = workerModule.default;

        const pdfBytes = await blob.arrayBuffer();
        loadingTask = getDocument({ data: pdfBytes });
        pdfDocument = await loadingTask.promise;

        if (cancelled || abortController.signal.aborted) {
          return;
        }

        const pageCount = pdfDocument.numPages;
        const pagesToRender = Math.min(pageCount, BOOK_PREVIEW_MAX_PAGES);
        const renderedPages = [];

        for (let pageNumber = 1; pageNumber <= pagesToRender; pageNumber += 1) {
          const page = await pdfDocument.getPage(pageNumber);
          const baseViewport = page.getViewport({ scale: 1 });
          const scale = Math.min(1.25, BOOK_PREVIEW_TARGET_WIDTH / baseViewport.width);
          const viewport = page.getViewport({ scale });
          const canvas = document.createElement('canvas');
          const context = canvas.getContext('2d', { alpha: false });

          if (!context) {
            throw new Error('No se pudo preparar la vista previa del PDF.');
          }

          canvas.width = Math.ceil(viewport.width);
          canvas.height = Math.ceil(viewport.height);

          await page.render({
            canvasContext: context,
            viewport
          }).promise;

          renderedPages.push({
            pageNumber,
            src: canvas.toDataURL('image/png'),
            width: canvas.width,
            height: canvas.height
          });

          page.cleanup();
          canvas.width = 0;
          canvas.height = 0;
        }

        if (!cancelled && !abortController.signal.aborted) {
          setPdfPreviewPages(renderedPages);
          setPdfPreviewTotalPages(pageCount);
        }
      } catch (requestError) {
        if (!cancelled && !abortController.signal.aborted) {
          setPdfPreviewPages([]);
          setPdfPreviewTotalPages(0);
          setPdfPreviewError(requestError.message ?? 'No se pudo generar la vista previa del PDF.');
        }
      } finally {
        if (pdfDocument) {
          await pdfDocument.destroy().catch(() => {});
        } else if (loadingTask) {
          await loadingTask.destroy().catch(() => {});
        }

        if (!cancelled) {
          setPdfPreviewLoading(false);
        }
      }
    }

    const timeoutId = window.setTimeout(() => {
      renderPdfPreview();
    }, BOOK_PREVIEW_DEBOUNCE_MS);

    return () => {
      cancelled = true;
      abortController.abort();
      window.clearTimeout(timeoutId);
      if (loadingTask) {
        loadingTask.destroy().catch(() => {});
      }
      if (pdfDocument) {
        pdfDocument.destroy().catch(() => {});
      }
    };
  }, [farm.id, orderedSelectedSectionIds, preview, token]);

  async function handlePdf(mode) {
    if (!canGeneratePdf) {
      setError('Debes seleccionar al menos un apartado del libro.');
      return;
    }

    if (mode === 'download') {
      setDownloading(true);
    } else {
      setPrinting(true);
    }
    setError('');

    try {
      const { blob, filename } = await apiBlobRequest(buildBookPdfPath(farm.id, orderedSelectedSectionIds), { token });
      const objectUrl = URL.createObjectURL(blob);

      if (mode === 'download') {
        const anchor = document.createElement('a');
        anchor.href = objectUrl;
        anchor.download = filename;
        anchor.click();
      } else {
        window.open(objectUrl, '_blank', 'noopener,noreferrer');
      }

      window.setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setDownloading(false);
      setPrinting(false);
    }
  }

  if (loading) {
    return <div className="panel-card empty-state">Preparando vista previa del libro...</div>;
  }

  return (
    <section className="panel-card stack">
      <div className="section-heading-row">
        <div>
          <h2>Libro de registro</h2>
          <p>Generación oficial imprimible a partir de los datos actuales de la explotación.</p>
        </div>
        <div className="operation-form-actions">
          <button className="secondary-button" type="button" onClick={() => handlePdf('print')} disabled={printing || downloading || !canGeneratePdf}>
            {printing ? 'Abriendo...' : 'Abrir / imprimir PDF'}
          </button>
          <button className="primary-button" type="button" onClick={() => handlePdf('download')} disabled={downloading || printing || !canGeneratePdf}>
            {downloading ? 'Descargando...' : 'Descargar PDF'}
          </button>
        </div>
      </div>

      {error && <div className="error-banner">{error}</div>}

      {preview && (
        <div className="book-layout">
          <aside className="book-config-card stack">
            <div className="detail-header">
              <div>
                <h2>Apartados a incluir</h2>
                <p>Selecciona qué secciones se imprimirán o exportarán en el libro.</p>
              </div>
              <strong>{selectedSectionCount}/{totalSectionCount}</strong>
            </div>

            <div className="book-toolbar">
              <button className="secondary-button" type="button" onClick={selectAllSections}>
                Seleccionar todo
              </button>
              <button className="secondary-button" type="button" onClick={clearSelectedSections} disabled={selectedSectionCount === 0}>
                Limpiar
              </button>
            </div>

            <div className="book-section-list">
              {preview.sections.map((section) => {
                const selected = selectedSectionIds.includes(section.id);

                return (
                  <button
                    key={section.id}
                    type="button"
                    className={selected ? 'book-section-option book-section-option-active' : 'book-section-option'}
                    onClick={() => toggleSection(section.id)}
                  >
                    <span className={selected ? 'book-section-check book-section-check-active' : 'book-section-check'}>
                      {selected ? <Check size={12} /> : null}
                    </span>
                    <span className="book-section-copy">
                      <strong>{section.title}</strong>
                      <small>{section.description}</small>
                    </span>
                    <span className="book-section-count">{section.items}</span>
                  </button>
                );
              })}
            </div>
          </aside>

          <div className="panel-card stack book-document-preview">
            <div className="detail-header">
              <div>
                <h2>Vista previa de impresión</h2>
                <p>Se muestran solo las tres primeras páginas del PDF real.</p>
              </div>
              <strong>
                {pdfPreviewTotalPages > 0
                  ? `${Math.min(pdfPreviewTotalPages, BOOK_PREVIEW_MAX_PAGES)} / ${pdfPreviewTotalPages} pág.`
                  : 'Sin páginas'}
              </strong>
            </div>

            {pdfPreviewError && <div className="error-banner">{pdfPreviewError}</div>}

            <div className="book-document-preview-body">
              {orderedSelectedSectionIds.length === 0 ? (
                <div className="empty-state">Selecciona al menos un apartado para generar la vista previa del PDF.</div>
              ) : (
                <>
                <div className="book-preview-status">
                  <span>{preview.template === 'official-porcino' ? 'Plantilla oficial porcino' : 'Plantilla oficial ovino/caprino'}</span>
                  {pdfPreviewLoading ? <strong>Actualizando vista previa...</strong> : null}
                </div>

                {pdfPreviewPages.length === 0 && pdfPreviewLoading ? (
                  <div className="book-pdf-loading">
                    <div className="book-pdf-skeleton" />
                    <div className="book-pdf-skeleton" />
                    <div className="book-pdf-skeleton" />
                  </div>
                ) : pdfPreviewPages.length === 0 ? (
                  <div className="empty-state">No hay páginas disponibles para la selección actual.</div>
                ) : (
                  <div className="book-pdf-preview-list">
                    {pdfPreviewPages.map((page) => (
                      <article key={page.pageNumber} className="book-pdf-page-card">
                        <div className="book-pdf-page-meta">
                          <span>Página {page.pageNumber}</span>
                        </div>
                        <div className="book-pdf-page-frame">
                          <img
                            src={page.src}
                            alt={`Vista previa de la página ${page.pageNumber} del libro de registro`}
                            width={page.width}
                            height={page.height}
                          />
                        </div>
                      </article>
                    ))}
                  </div>
                )}
                </>
              )}
            </div>
          </div>

        </div>
      )}
    </section>
  );
}

function FarmIncidentsSection({ farm, token }) {
  const [incidents, setIncidents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [form, setForm] = useState({
    animalIdentification: '',
    incidentDate: new Date().toISOString().slice(0, 10),
    changeReason: '',
    description: '',
    lastIdentification: '',
    newIdentification: ''
  });

  async function loadIncidents() {
    setLoading(true);
    setError('');

    try {
      const response = await apiRequest(`/api/farms/${farm.id}/incidents`, { token });
      setIncidents(response);
    } catch (requestError) {
      setError(requestError.message);
      setIncidents([]);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadIncidents();
  }, [farm.id, token]);

  async function handleSubmit(event) {
    event.preventDefault();
    setError('');
    setSuccess('');

    if (!form.incidentDate) {
      setError('La fecha de incidencia es obligatoria.');
      return;
    }

    if (
      !form.animalIdentification.trim() &&
      !form.changeReason.trim() &&
      !form.description.trim() &&
      !form.lastIdentification.trim() &&
      !form.newIdentification.trim()
    ) {
      setError('Debes completar al menos un dato descriptivo de la incidencia.');
      return;
    }

    if (form.animalIdentification.trim() && !isValidAnimalIdentification(farm.livestockSpecies, form.animalIdentification)) {
      setError('La identificación del animal relacionada con la incidencia no es válida.');
      return;
    }

    if (form.lastIdentification.trim() && !isValidAnimalIdentification(farm.livestockSpecies, form.lastIdentification)) {
      setError('La identificación anterior no es válida.');
      return;
    }

    if (form.newIdentification.trim() && !isValidAnimalIdentification(farm.livestockSpecies, form.newIdentification)) {
      setError('La nueva identificación no es válida.');
      return;
    }

    setSubmitting(true);
    try {
      await apiRequest(`/api/farms/${farm.id}/incidents`, {
        method: 'POST',
        token,
        body: {
          animalIdentification: form.animalIdentification.trim() ? normalizeAnimalIdentification(form.animalIdentification) : null,
          incidentDate: form.incidentDate,
          changeReason: emptyToNull(form.changeReason),
          description: emptyToNull(form.description),
          lastIdentification: form.lastIdentification.trim() ? normalizeAnimalIdentification(form.lastIdentification) : null,
          newIdentification: form.newIdentification.trim() ? normalizeAnimalIdentification(form.newIdentification) : null
        }
      });
      setSuccess('Incidencia registrada correctamente.');
      setModalOpen(false);
      setForm({
        animalIdentification: '',
        incidentDate: new Date().toISOString().slice(0, 10),
        changeReason: '',
        description: '',
        lastIdentification: '',
        newIdentification: ''
      });
      await loadIncidents();
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) {
    return <div className="panel-card empty-state">Cargando incidencias...</div>;
  }

  return (
    <section className="panel-card stack">
      <div className="section-heading-row">
        <div>
          <h2>Incidencias</h2>
          <p>Anotaciones de identificación y regularizaciones del ganado.</p>
        </div>
        <button className="primary-button" type="button" onClick={() => setModalOpen(true)}>
          <Plus size={16} />
          Registrar incidencia
        </button>
      </div>

      {error && <div className="error-banner">{error}</div>}
      {success && <div className="success-banner">{success}</div>}

      {incidents.length === 0 ? (
        <div className="empty-state">
          <TriangleAlert size={28} />
          <div>No hay incidencias registradas.</div>
        </div>
      ) : (
        <div className="animal-table-card">
          <div className="table-scroll">
            <table className="animal-table">
              <thead>
                <tr>
                  {['Fecha', 'Animal', 'Motivo', 'Descripción', 'Identificación anterior', 'Nueva identificación'].map((header) => (
                    <th key={header}>{header}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {incidents.map((incident) => (
                  <tr key={incident.id}>
                    <td>{formatDate(incident.incidentDate)}</td>
                    <td>{incident.animalIdentification ?? '—'}</td>
                    <td>{incident.changeReason ?? '—'}</td>
                    <td>{incident.description ?? '—'}</td>
                    <td>{incident.lastIdentification ?? '—'}</td>
                    <td>{incident.newIdentification ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="animal-table-footer">{incidents.length} incidencias</div>
        </div>
      )}

      {modalOpen && (
        <ModalDialog cardAs="form" size="wide" onSubmit={handleSubmit}>
          <ModalHeader
            icon={<TriangleAlert size={18} />}
            title="Nueva incidencia"
            subtitle={farm.name}
            onClose={() => setModalOpen(false)}
          />
          <ModalBody className="operation-modal-body">
              <label>
                <span>Animal relacionado</span>
                <input value={form.animalIdentification} onChange={(event) => setForm({ ...form, animalIdentification: event.target.value })} placeholder="Ej: ES0600005831 / GT1800001004" />
              </label>
              <label>
                <span>Fecha de incidencia *</span>
                <input type="date" value={form.incidentDate} onChange={(event) => setForm({ ...form, incidentDate: event.target.value })} />
              </label>
              <label>
                <span>Motivo</span>
                <input value={form.changeReason} onChange={(event) => setForm({ ...form, changeReason: event.target.value })} placeholder="Reposición de crotal, regularización documental..." />
              </label>
              <label>
                <span>Identificación anterior</span>
                <input value={form.lastIdentification} onChange={(event) => setForm({ ...form, lastIdentification: event.target.value })} placeholder="Ej: GT1800001004" />
              </label>
              <label>
                <span>Nueva identificación</span>
                <input value={form.newIdentification} onChange={(event) => setForm({ ...form, newIdentification: event.target.value })} placeholder="Si aplica" />
              </label>
              <label className="operation-form-wide">
                <span>Descripción</span>
                <textarea value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} placeholder="Detalle de la incidencia y actuaciones realizadas." />
              </label>
          </ModalBody>
          <ModalFooter align="end">
              <button className="secondary-button" type="button" onClick={() => setModalOpen(false)}>Cancelar</button>
              <button className="primary-button" type="submit" disabled={submitting}>{submitting ? 'Guardando...' : 'Guardar incidencia'}</button>
          </ModalFooter>
        </ModalDialog>
      )}
    </section>
  );
}

function FarmInspectionsSection({ farm, token }) {
  const [inspections, setInspections] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [form, setForm] = useState({
    inspectionDate: new Date().toISOString().slice(0, 10),
    reason: '',
    observations: '',
    veterinary: '',
    taggedAnimals: ''
  });

  async function loadInspections() {
    setLoading(true);
    setError('');

    try {
      const response = await apiRequest(`/api/farms/${farm.id}/inspections`, { token });
      setInspections(response);
    } catch (requestError) {
      setError(requestError.message);
      setInspections([]);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadInspections();
  }, [farm.id, token]);

  async function handleSubmit(event) {
    event.preventDefault();
    setError('');
    setSuccess('');

    if (!form.inspectionDate) {
      setError('La fecha de inspección es obligatoria.');
      return;
    }

    if (!form.reason.trim() && !form.observations.trim()) {
      setError('Debes indicar al menos el motivo o las observaciones de la inspección.');
      return;
    }

    const taggedAnimals = parseOptionalInteger(form.taggedAnimals);
    if (taggedAnimals != null && (!Number.isInteger(taggedAnimals) || taggedAnimals < 0)) {
      setError('Los animales revisados deben ser un número entero igual o mayor que cero.');
      return;
    }

    setSubmitting(true);
    try {
      await apiRequest(`/api/farms/${farm.id}/inspections`, {
        method: 'POST',
        token,
        body: {
          inspectionDate: form.inspectionDate,
          reason: emptyToNull(form.reason),
          observations: emptyToNull(form.observations),
          veterinary: emptyToNull(form.veterinary),
          taggedAnimals
        }
      });
      setSuccess('Inspección registrada correctamente.');
      setModalOpen(false);
      setForm({
        inspectionDate: new Date().toISOString().slice(0, 10),
        reason: '',
        observations: '',
        veterinary: '',
        taggedAnimals: ''
      });
      await loadInspections();
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) {
    return <div className="panel-card empty-state">Cargando inspecciones...</div>;
  }

  return (
    <section className="panel-card stack">
      <div className="section-heading-row">
        <div>
          <h2>Inspecciones</h2>
          <p>Control veterinario y observaciones oficiales asociadas a la explotación.</p>
        </div>
        <button className="primary-button" type="button" onClick={() => setModalOpen(true)}>
          <Plus size={16} />
          Registrar inspección
        </button>
      </div>

      {error && <div className="error-banner">{error}</div>}
      {success && <div className="success-banner">{success}</div>}

      {inspections.length === 0 ? (
        <div className="empty-state">
          <ClipboardCheck size={28} />
          <div>No hay inspecciones registradas.</div>
        </div>
      ) : (
        <div className="animal-table-card">
          <div className="table-scroll">
            <table className="animal-table">
              <thead>
                <tr>
                  {['Fecha', 'Motivo', 'Observaciones', 'Veterinario', 'Animales revisados'].map((header) => (
                    <th key={header}>{header}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {inspections.map((inspection) => (
                  <tr key={inspection.id}>
                    <td>{formatDate(inspection.inspectionDate)}</td>
                    <td>{inspection.reason ?? '—'}</td>
                    <td>{inspection.observations ?? '—'}</td>
                    <td>{inspection.veterinary ?? '—'}</td>
                    <td>{inspection.taggedAnimals ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="animal-table-footer">{inspections.length} inspecciones</div>
        </div>
      )}

      {modalOpen && (
        <ModalDialog cardAs="form" size="wide" onSubmit={handleSubmit}>
          <ModalHeader
            icon={<ClipboardCheck size={18} />}
            title="Nueva inspección"
            subtitle={farm.name}
            onClose={() => setModalOpen(false)}
          />
          <ModalBody className="operation-modal-body">
              <label>
                <span>Fecha de inspección *</span>
                <input type="date" value={form.inspectionDate} onChange={(event) => setForm({ ...form, inspectionDate: event.target.value })} />
              </label>
              <label>
                <span>Motivo</span>
                <input value={form.reason} onChange={(event) => setForm({ ...form, reason: event.target.value })} placeholder="Inspección programada, revisión documental..." />
              </label>
              <label>
                <span>Veterinario</span>
                <input value={form.veterinary} onChange={(event) => setForm({ ...form, veterinary: event.target.value })} placeholder="Nombre del profesional responsable" />
              </label>
              <label>
                <span>Animales revisados</span>
                <input type="number" min="0" step="1" value={form.taggedAnimals} onChange={(event) => setForm({ ...form, taggedAnimals: event.target.value })} placeholder="Ej: 24" />
              </label>
              <label className="operation-form-wide">
                <span>Observaciones</span>
                <textarea value={form.observations} onChange={(event) => setForm({ ...form, observations: event.target.value })} placeholder="Observaciones veterinarias y seguimiento de la inspección." />
              </label>
          </ModalBody>
          <ModalFooter align="end">
              <button className="secondary-button" type="button" onClick={() => setModalOpen(false)}>Cancelar</button>
              <button className="primary-button" type="submit" disabled={submitting}>{submitting ? 'Guardando...' : 'Guardar inspección'}</button>
          </ModalFooter>
        </ModalDialog>
      )}
    </section>
  );
}

export function FarmDetailPage() {
  const { farmId } = useParams();
  const navigate = useNavigate();
  const { token } = useAuth();
  const [farm, setFarm] = useState(null);
  const [settingsModalOpen, setSettingsModalOpen] = useState(false);
  const [settingsForm, setSettingsForm] = useState(createFarmSettingsForm(null));
  const [settingsErrors, setSettingsErrors] = useState({});
  const [settingsRequestError, setSettingsRequestError] = useState('');
  const [settingsSubmitting, setSettingsSubmitting] = useState(false);
  const [activeTab, setActiveTab] = useState('summary');
  const [movementAnimalFilter, setMovementAnimalFilter] = useState(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  async function loadFarmDetail(targetFarmId) {
    setLoading(true);
    setError('');

    try {
      const response = await apiRequest(`/api/farms/${targetFarmId}`, { token });
      setFarm(response);
    } catch (requestError) {
      setError(requestError.message);
      setFarm(null);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    if (farmId) {
      setActiveTab('summary');
      setMovementAnimalFilter(null);
      setSettingsModalOpen(false);
      loadFarmDetail(farmId);
    } else {
      setLoading(false);
      setError('Explotación no encontrada.');
    }
  }, [farmId, token]);

  function openSettingsModal() {
    if (!farm) {
      return;
    }

    setSettingsForm(createFarmSettingsForm(farm));
    setSettingsErrors({});
    setSettingsRequestError('');
    setSettingsSubmitting(false);
    setSettingsModalOpen(true);
  }

  function closeSettingsModal() {
    setSettingsModalOpen(false);
    setSettingsErrors({});
    setSettingsRequestError('');
    setSettingsSubmitting(false);
  }

  function updateSettingsField(field, value) {
    setSettingsForm((current) => ({ ...current, [field]: value }));
    setSettingsErrors((current) => {
      if (!current[field]) {
        return current;
      }

      const next = { ...current };
      delete next[field];
      return next;
    });
  }

  async function submitSettingsForm() {
    if (!farm) {
      return;
    }

    const validationErrors = validateFarmSettingsForm(settingsForm, farm.livestockSpecies);
    if (Object.keys(validationErrors).length > 0) {
      setSettingsErrors(validationErrors);
      return;
    }

    setSettingsSubmitting(true);
    setSettingsRequestError('');

    try {
      const updatedFarm = await apiRequest(`/api/farms/${farm.id}`, {
        method: 'PUT',
        token,
        body: {
          name: settingsForm.name.trim(),
          regaCode: normalizeRegaCode(settingsForm.regaCode),
          regime: settingsForm.regime,
          town: emptyToNull(settingsForm.town),
          province: emptyToNull(settingsForm.province),
          address: emptyToNull(settingsForm.address),
          zipCode: emptyToNull(settingsForm.zipCode),
          authorisedCapacity: settingsForm.authorisedCapacity === '' ? null : Number(settingsForm.authorisedCapacity),
          porcineRegistryNumber: emptyToNull(settingsForm.porcineRegistryNumber),
          responsible: emptyToNull(settingsForm.responsible),
          zootechnicClassification: emptyToNull(settingsForm.zootechnicClassification),
          xCoordinate: settingsForm.xCoordinate === '' ? null : Number(settingsForm.xCoordinate),
          yCoordinate: settingsForm.yCoordinate === '' ? null : Number(settingsForm.yCoordinate)
        }
      });

      setFarm(updatedFarm);
      closeSettingsModal();
    } catch (requestError) {
      setSettingsRequestError(requestError.message);
    } finally {
      setSettingsSubmitting(false);
    }
  }

  const occupancy = useMemo(() => {
    if (farm?.livestockSpecies !== 'Porcine' || !farm?.authorisedCapacity) {
      return null;
    }

    return Math.round((farm.animalCount / farm.authorisedCapacity) * 100);
  }, [farm]);

  if (loading) {
    return <div className="screen-center">Cargando explotación...</div>;
  }

  if (!farm) {
    return (
      <div className="page-stack">
        <button className="secondary-button farm-detail-back" type="button" onClick={() => navigate('/app/farms')}>
          <ArrowLeft size={16} />
          Volver al listado
        </button>
        <div className="panel-card empty-state">
          <Building2 size={36} />
          <div>{error || 'No se pudo cargar la explotación.'}</div>
        </div>
      </div>
    );
  }

  const speciesTone = speciesToneMap[farm.livestockSpecies] ?? { bg: '#F3F4F6', color: '#6B7280', label: farm.livestockSpecies };
  const statusTone = statusToneMap[farm.status] ?? statusToneMap.Inactive;
  const activeTabConfig = detailTabs.find((tab) => tab.key === activeTab) ?? detailTabs[0];

  return (
    <div className="page-stack">
      <button className="secondary-button farm-detail-back" type="button" onClick={() => navigate('/app/farms')}>
        <ArrowLeft size={16} />
        Volver al listado
      </button>

      {error && <div className="error-banner">{error}</div>}

      <section className="panel-card stack farm-detail-hero">
        <div className="farm-detail-hero-top">
          <div className="farm-detail-hero-main">
            <div className="farm-card-icon farm-detail-hero-icon">
              <Building2 size={22} />
            </div>
            <div className="farm-detail-hero-copy">
              <div className="farm-detail-hero-badges">
                <span className="farm-badge" style={{ background: speciesTone.bg, color: speciesTone.color }}>
                  {speciesTone.label}
                </span>
                <span className="farm-badge" style={{ background: statusTone.bg, color: statusTone.color }}>
                  {statusTone.label}
                </span>
              </div>
              <h1>{farm.name}</h1>
              <p>REGA: {farm.regaCode}</p>
            </div>
          </div>
          <div className="farm-detail-hero-actions">
            <button className="secondary-button" type="button" onClick={openSettingsModal}>
              <Edit3 size={15} />
              Ajustes
            </button>
          </div>
        </div>

        <div className="farm-detail-metrics">
          <SummaryMetric label="Animales registrados" value={farm.animalCount} tone="success" />
          {farm.livestockSpecies === 'Porcine' && farm.porcineRegistryNumber && (
            <SummaryMetric label="Registro porcino" value={farm.porcineRegistryNumber} />
          )}
          {farm.livestockSpecies === 'Porcine' && (
            <SummaryMetric label="Capacidad autorizada" value={farm.authorisedCapacity ?? 'No informada'} />
          )}
          <SummaryMetric label="Titular" value={farm.farmerName} />
          <SummaryMetric label="Régimen" value={formatRegime(farm.regime)} />
        </div>

        <div className="farm-card-meta farm-detail-meta">
          <span>
            <MapPin size={12} />
            {formatText(farm.town, 'Sin localidad')}, {formatText(farm.province, 'Sin provincia')}
          </span>
          {occupancy !== null && <span className="farm-card-highlight">Ocupación: {occupancy}%</span>}
        </div>

        {occupancy !== null && (
          <div className="occupancy-block">
            <div className="occupancy-copy">
              <span>Capacidad ocupada</span>
              <strong>{occupancy}%</strong>
            </div>
            <div className="occupancy-bar">
              <div className="occupancy-bar-fill" style={{ width: `${Math.min(occupancy, 100)}%` }} />
            </div>
          </div>
        )}
      </section>

      <section className="panel-card stack">
        <div className="detail-header">
          <div>
            <h2>Navegación de secciones</h2>
          </div>
          <strong>{activeTabConfig.label}</strong>
        </div>

        <div className="farm-detail-nav-grid" role="tablist" aria-label="Secciones de la explotación">
          {detailTabs.map((tab) => {
            const Icon = tab.icon;

            return (
              <button
                key={tab.key}
                type="button"
                role="tab"
                aria-selected={activeTab === tab.key}
                disabled={!tab.enabled}
                onClick={() => tab.enabled && setActiveTab(tab.key)}
                className={activeTab === tab.key ? 'farm-detail-tab farm-detail-tab-active' : 'farm-detail-tab'}
              >
                <span className="farm-detail-tab-icon">
                  <Icon size={16} />
                </span>
                <span className="farm-detail-tab-copy">
                  <strong>{tab.label}</strong>
                  {!tab.enabled && <small>Próximamente</small>}
                </span>
              </button>
            );
          })}
        </div>
      </section>

      {activeTab === 'summary' && (
        <section className="farm-detail-grid">
          <article className="panel-card stack">
            <div className="detail-header">
              <div>
                <h2>Datos oficiales</h2>
                <p>Información principal de la explotación registrada en Pecualia.</p>
              </div>
            </div>
            <div className="detail-grid">
              <DetailField label="Titular" value={farm.farmerName} />
              <DetailField label="Especie" value={speciesTone.label} />
              <DetailField label="Régimen" value={formatRegime(farm.regime)} />
              <DetailField label="Estado" value={statusTone.label} />
              <DetailField label="Tipo de Ganader@" value={formatText(farm.livestockType)} />
              <DetailField label="Capacidad productiva" value={formatText(farm.productionCapacity)} />
              {farm.livestockSpecies === 'Porcine' && (
                <DetailField label="Registro porcino" value={formatText(farm.porcineRegistryNumber)} />
              )}
              {farm.livestockSpecies === 'Porcine' && (
                <DetailField label="Capacidad autorizada" value={farm.authorisedCapacity ?? 'No informada'} />
              )}
              <DetailField label="Clasificación zootécnica" value={formatText(farm.zootechnicClassification)} />
            </div>
          </article>

          <article className="panel-card stack">
            <div className="detail-header">
              <div>
                <h2>Ubicación y responsable</h2>
                <p>Datos administrativos y coordenadas asociadas a la explotación.</p>
              </div>
            </div>
            <div className="detail-grid">
              <DetailField label="Provincia" value={formatText(farm.province)} />
              <DetailField label="Localidad" value={formatText(farm.town)} />
              <DetailField label="Código postal" value={formatText(farm.zipCode)} />
              <DetailField label="Huso" value={farm.spindle ?? 'No informado'} />
              <DetailField label="Coordenada X" value={formatCoordinate(farm.xCoordinate)} />
              <DetailField label="Coordenada Y" value={formatCoordinate(farm.yCoordinate)} />
              <DetailField label="Responsable" value={formatText(farm.responsible)} />
              <DetailField label="Dirección" value={formatText(farm.address)} fullWidth />
            </div>
          </article>
        </section>
      )}

      {activeTab === 'animals' && (
        <FarmAnimalsSection
          farm={farm}
          token={token}
          movementFilter={movementAnimalFilter}
          onClearMovementFilter={() => setMovementAnimalFilter(null)}
        />
      )}
      {activeTab === 'movements' && (
        <FarmMovementsSection
          farm={farm}
          token={token}
          onViewAnimalsForMovement={(movement) => {
            setMovementAnimalFilter({
              movementId: movement.id,
              codRemo: movement.codRemo
            });
            setActiveTab('animals');
          }}
        />
      )}
      {activeTab === 'births' && <FarmBirthsSection farm={farm} token={token} />}
      {activeTab === 'deaths' && <FarmDeathsSection farm={farm} token={token} />}
      {activeTab === 'vaccinations' && <FarmVaccinationsSection farm={farm} token={token} />}
      {activeTab === 'balances' && <FarmCensusBalancesSection farm={farm} token={token} />}
      {activeTab === 'book' && <FarmBookSection farm={farm} token={token} />}
      {activeTab === 'incidents' && <FarmIncidentsSection farm={farm} token={token} />}
      {activeTab === 'inspections' && <FarmInspectionsSection farm={farm} token={token} />}

      {settingsModalOpen && (
        <FarmSettingsModal
          farm={farm}
          form={settingsForm}
          errors={settingsErrors}
          requestError={settingsRequestError}
          submitting={settingsSubmitting}
          onChange={updateSettingsField}
          onClose={closeSettingsModal}
          onSubmit={submitSettingsForm}
        />
      )}
    </div>
  );
}
