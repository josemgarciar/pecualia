import {
  ArrowLeftRight,
  BarChart3,
  BookOpen,
  Building2,
  ChevronDown,
  ClipboardCheck,
  Edit3,
  Shield,
  Skull,
  Sprout,
  Tag,
  Trash2,
  TriangleAlert
} from 'lucide-react';
import { ModalBody, ModalDialog, ModalFieldLabel, ModalFooter, ModalHeader } from '../../shared/components/modal/Modal';
import {
  getAnimalIdentificationFormatMessage,
  isMerDestinationCode,
  isValidAnimalIdentification,
  isValidRegaCode
} from '../../shared/validation/identifiers';

export const speciesToneMap = {
  Ovine: { bg: '#DDEBDF', color: '#2F6B4F', label: 'Ovino' },
  Caprine: { bg: '#DBEAFE', color: '#2563EB', label: 'Caprino' },
  Porcine: { bg: '#FCE7F3', color: '#9D174D', label: 'Porcino' }
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

export const detailTabs = [
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

export const currentYear = new Date().getFullYear();
export const monthLabels = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];
export const BOOK_PREVIEW_MAX_PAGES = 3;
export const BOOK_PREVIEW_DEBOUNCE_MS = 450;
export const BOOK_PREVIEW_TARGET_WIDTH = 760;
export const FARM_ANIMALS_SEARCH_DEBOUNCE_MS = 300;
export const FARM_ANIMALS_DEFAULT_PAGE_SIZE = 25;
export const FARM_ANIMALS_PAGE_SIZE_OPTIONS = [10, 25, 50];
export const porcineAnimalTypeOptions = [
  'Verracos',
  'Cerdas vida',
  'Hembras reposición',
  'Machos reposición',
  'Lechones',
  'Recría',
  'Cebo'
];
export const regimeOptions = [
  { value: 'Extensive', label: 'Extensivo' },
  { value: 'SemiExtensive', label: 'Semiextensivo' },
  { value: 'Intensive', label: 'Intensivo' }
];
export const provinceOptions = [
  'Álava', 'Albacete', 'Alicante', 'Almería', 'Asturias', 'Ávila', 'Badajoz', 'Barcelona', 'Burgos', 'Cáceres',
  'Cádiz', 'Cantabria', 'Castellón', 'Ciudad Real', 'Córdoba', 'Cuenca', 'Girona', 'Granada', 'Guadalajara',
  'Guipúzcoa', 'Huelva', 'Huesca', 'Islas Baleares', 'Jaén', 'La Coruña', 'La Rioja', 'Las Palmas', 'León',
  'Lleida', 'Lugo', 'Madrid', 'Málaga', 'Murcia', 'Navarra', 'Ourense', 'Palencia', 'Pontevedra', 'Salamanca',
  'Santa Cruz de Tenerife', 'Segovia', 'Sevilla', 'Soria', 'Tarragona', 'Teruel', 'Toledo', 'Valencia',
  'Valladolid', 'Vizcaya', 'Zamora', 'Zaragoza'
];

export function formatText(value, fallback = 'No informado') {
  return value ?? fallback;
}

export function formatLivestockSpecies(value, fallback = 'Sin especie') {
  return speciesToneMap[value]?.label ?? value ?? fallback;
}

export function formatRegime(value) {
  if (!value) {
    return 'No informado';
  }

  return regimeLabelMap[value] ?? value;
}

export function formatCoordinate(value) {
  if (value == null) {
    return 'No informada';
  }

  return new Intl.NumberFormat('es-ES', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  }).format(value);
}

export function formatDate(value) {
  return value ? new Intl.DateTimeFormat('es-ES').format(new Date(`${value}T00:00:00`)) : '—';
}

export function formatAnimalSex(value) {
  return animalSexLabelMap[value] ?? value ?? 'No informado';
}

export function formatAnimalCause(value) {
  return animalRegistrationCauseLabelMap[value] ?? animalDischargeCauseLabelMap[value] ?? value ?? 'No informada';
}

export function isMerOnlyDeathSpecies(species) {
  return species === 'Porcine' || species === 'Caprine';
}

export function getDeathDestinationOptions(species) {
  return isMerOnlyDeathSpecies(species)
    ? [{ value: 'MER', label: 'MER' }]
    : [
        { value: 'SANDACH', label: 'SANDACH' },
        { value: 'MER', label: 'MER' }
      ];
}

export function getDeathDestinationType(value) {
  if (isMerDestinationCode(value)) {
    return 'MER';
  }

  return value ?? '';
}

export function formatDeathDestination(value) {
  if (!value) {
    return '—';
  }

  return isMerDestinationCode(value) && value !== 'MER'
    ? `MER · ${value}`
    : value;
}

export function parsePositiveNumber(value) {
  return value === '' ? null : Number(value);
}

export function parseOptionalInteger(value) {
  return value === '' ? null : Number(value);
}

export function emptyToNull(value) {
  const normalized = value?.trim();
  return normalized ? normalized : null;
}

export function createDeathFormState(species) {
  return {
    identification: '',
    animalType: '',
    quantity: '1',
    dischargeDate: new Date().toISOString().slice(0, 10),
    destinationCode: isMerOnlyDeathSpecies(species) ? 'MER' : '',
    merCode: ''
  };
}

export function createAutorrepositionForm(farm) {
  const today = new Date().toISOString().slice(0, 10);

  return {
    startIdentification: '',
    quantity: '1',
    breed: '',
    sex: '',
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

export function buildConsecutiveIdentificationPreview(startIdentification, numberOfAnimals) {
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

export function validateAutorrepositionForm(form, species, availability) {
  const errors = {};
  const count = Number(form.quantity);
  const totalAvailable = availability?.availableAnimals ?? 0;
  const totalEligible = availability?.eligibleAnimals ?? 0;

  if (!form.startIdentification.trim()) {
    errors.startIdentification = 'Campo obligatorio';
  } else if (!isValidAnimalIdentification(species, form.startIdentification)) {
    errors.startIdentification = getAnimalIdentificationFormatMessage(species);
  }

  if (!Number.isInteger(count) || count <= 0) {
    errors.quantity = 'Debe ser un número entero mayor que cero';
  }

  if (count > totalAvailable) {
    errors.quantity = 'No puedes autoreponer más animales que los no identificados disponibles en el censo';
  }

  if (count > totalEligible) {
    errors.quantity = 'Solo puedes autoreponer animales con más de 4 meses cumplidos';
  }

  if (!form.breed.trim()) {
    errors.breed = 'Campo obligatorio';
  }

  if (!form.sex.trim()) {
    errors.sex = 'Campo obligatorio';
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
    buildConsecutiveIdentificationPreview(form.startIdentification, form.quantity);
  } catch (error) {
    errors.startIdentification = error.message;
  }

  return errors;
}

export function createAnimalDetailForm(animal) {
  return {
    identification: animal?.identification ?? '',
    birthYear: animal?.birthYear != null ? String(animal.birthYear) : '',
    breed: animal?.breed ?? '',
    sex: animal?.sex ?? '',
    registrationDate: animal?.registrationDate ?? '',
    registrationCause: animal?.registrationCauseValue ?? '',
    originCode: animal?.originCode ?? '',
    genotyping: animal?.ovinoCaprino?.genotyping ?? '',
    dominantAllele: animal?.ovinoCaprino?.dominantAllele ?? '',
    lowAllele: animal?.ovinoCaprino?.lowAllele ?? '',
    animalType: animal?.porcino?.animalType ?? '',
    identificationDate: animal?.porcino?.identificationDate ?? '',
    pigRegistrationNumber: animal?.porcino?.pigRegistrationNumber ?? '',
    tag: animal?.porcino?.tag ?? ''
  };
}

export function createManualPorcineAnimalForm() {
  const today = new Date().toISOString().slice(0, 10);

  return {
    identification: '',
    birthYear: '',
    breed: '',
    sex: '',
    registrationDate: today,
    registrationCause: 'Entrada',
    originCode: '',
    animalType: '',
    identificationDate: today,
    pigRegistrationNumber: '',
    tag: ''
  };
}

export function validateManualPorcineAnimalForm(form) {
  const errors = {};

  if (!form.identification.trim()) {
    errors.identification = 'Campo obligatorio';
  } else if (!isValidAnimalIdentification('Porcine', form.identification)) {
    errors.identification = getAnimalIdentificationFormatMessage('Porcine');
  }

  if (!form.animalType.trim()) {
    errors.animalType = 'Campo obligatorio para porcino';
  }

  if (form.originCode.trim() && !isValidRegaCode(form.originCode)) {
    errors.originCode = 'Código REGA inválido';
  }

  if (form.birthYear !== '') {
    const birthYear = Number(form.birthYear);
    if (!Number.isInteger(birthYear) || birthYear < 1900 || birthYear > 2100) {
      errors.birthYear = 'Debe ser un año válido';
    }
  }

  return errors;
}

export function formatAnimalGuideSeries(entryGuideSerie, exitGuideSerie) {
  if (!entryGuideSerie && !exitGuideSerie) {
    return 'No informadas';
  }

  return `${entryGuideSerie ?? '—'} / ${exitGuideSerie ?? '—'}`;
}

export function validateAnimalDetailForm(form, species) {
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

export function createFarmSettingsForm(farm) {
  return {
    name: farm?.name ?? '',
    regaCode: farm?.regaCode ?? '',
    regime: farm?.regime ?? '',
    town: farm?.town ?? '',
    province: farm?.province ?? '',
    address: farm?.address ?? '',
    zipCode: farm?.zipCode ?? '',
    porcineRegistryNumber: farm?.porcineRegistryNumber ?? '',
    livestockType: farm?.livestockType ?? '',
    porcineMothersCapacity: farm?.porcineMothersCapacity != null ? String(farm.porcineMothersCapacity) : '',
    porcineFatteningCapacity: farm?.porcineFatteningCapacity != null ? String(farm.porcineFatteningCapacity) : '',
    responsible: farm?.responsible ?? '',
    zootechnicClassification: farm?.zootechnicClassification ?? '',
    spindle: farm?.spindle != null ? String(farm.spindle) : '',
    xCoordinate: farm?.xCoordinate != null ? String(farm.xCoordinate) : '',
    yCoordinate: farm?.yCoordinate != null ? String(farm.yCoordinate) : ''
  };
}

export function validateFarmSettingsForm(form, species) {
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
  if (species === 'Porcine' && form.porcineMothersCapacity !== '') {
    const porcineMothersCapacity = Number(form.porcineMothersCapacity);
    if (!Number.isInteger(porcineMothersCapacity) || porcineMothersCapacity < 0) {
      errors.porcineMothersCapacity = 'Debe ser un número entero válido';
    }
  }
  if (species === 'Porcine' && form.porcineFatteningCapacity !== '') {
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

  return errors;
}

export function buildBookPdfPath(farmId, sectionIds) {
  const params = new URLSearchParams();
  sectionIds.forEach((sectionId) => params.append('sectionIds', sectionId));
  const query = params.toString();
  return `/api/farms/${farmId}/book/pdf${query ? `?${query}` : ''}`;
}

export function createVaccinationFormState() {
  return {
    animalIdentification: '',
    vaccinationDate: new Date().toISOString().slice(0, 10),
    nextDose: '',
    vaccinationType: '',
    observations: ''
  };
}

export function DetailField({ label, value, fullWidth = false }) {
  return (
    <div className={fullWidth ? 'detail-full' : undefined}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

export function SummaryMetric({ label, value, tone = 'default' }) {
  return (
    <article className={`farm-detail-metric-card${tone === 'success' ? ' farm-detail-metric-card-success' : ''}`}>
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  );
}

export function BirthDetailModal({ birth, farmName, onClose, onEdit, onDelete }) {
  if (!birth) {
    return null;
  }

  return (
    <ModalDialog size="wide">
      <ModalHeader
        icon={<Sprout size={18} />}
        title="Detalle del nacimiento"
        subtitle={farmName}
        onClose={onClose}
      />
      <ModalBody>
        <div className="profile-grid">
          <DetailField label="Fecha de parto" value={formatDate(birth.birthDate)} />
          <DetailField label="Crías declaradas" value={String(birth.offspringNumber)} />
          <DetailField label="Peso medio" value={birth.birthWeight == null ? 'No informado' : `${birth.birthWeight} kg`} />
          <DetailField label="Observaciones" value={birth.observations ?? 'No informadas'} fullWidth />
        </div>
      </ModalBody>
      <ModalFooter>
        <button className="danger-button" type="button" onClick={() => onDelete(birth)}>
          <Trash2 size={15} />
          Eliminar
        </button>
        <div className="animal-modal-actions">
          <button className="secondary-button" type="button" onClick={onClose}>Cerrar</button>
          <button className="primary-button" type="button" onClick={() => onEdit(birth)}>
            <Edit3 size={15} />
            Editar
          </button>
        </div>
      </ModalFooter>
    </ModalDialog>
  );
}

export function FarmSettingsModal({ farm, form, errors, requestError, submitting, onChange, onClose, onSubmit }) {
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
                <span className="farm-field-label">CAPACIDAD MÁXIMA MADRES</span>
                <input type="number" min="0" className={errors.porcineMothersCapacity ? 'farm-input farm-input-error' : 'farm-input'} value={form.porcineMothersCapacity} onChange={(event) => onChange('porcineMothersCapacity', event.target.value)} />
                {errors.porcineMothersCapacity && <p className="farm-field-error">{errors.porcineMothersCapacity}</p>}
              </div>

              <div className="farm-form-field">
                <span className="farm-field-label">CAPACIDAD MÁXIMA CEBO</span>
                <input type="number" min="0" className={errors.porcineFatteningCapacity ? 'farm-input farm-input-error' : 'farm-input'} value={form.porcineFatteningCapacity} onChange={(event) => onChange('porcineFatteningCapacity', event.target.value)} />
                {errors.porcineFatteningCapacity && <p className="farm-field-error">{errors.porcineFatteningCapacity}</p>}
              </div>
            </>
          )}

          <div className="farm-form-field">
            <span className="farm-field-label">RESPONSABLE</span>
            <input className="farm-input" value={form.responsible} onChange={(event) => onChange('responsible', event.target.value)} />
          </div>

          <div className="farm-form-field">
            <span className="farm-field-label">TIPO DE EXPLOTACIÓN</span>
            <input className="farm-input" value={form.livestockType} onChange={(event) => onChange('livestockType', event.target.value)} />
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
            <span className="farm-field-label">HUSO</span>
            <input type="number" min="1" className={errors.spindle ? 'farm-input farm-input-error' : 'farm-input'} value={form.spindle} onChange={(event) => onChange('spindle', event.target.value)} />
            {errors.spindle && <p className="farm-field-error">{errors.spindle}</p>}
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

export function AnimalDetailModal({
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
              <label className="farm-form-field">
                <ModalFieldLabel>Identificación / crotal</ModalFieldLabel>
                <input value={form.identification} onChange={(event) => onChange('identification', event.target.value)} />
                {errors.identification && <span className="farm-inline-error">{errors.identification}</span>}
              </label>
              <label className="farm-form-field">
                <ModalFieldLabel>Raza</ModalFieldLabel>
                <input value={form.breed} onChange={(event) => onChange('breed', event.target.value)} />
              </label>
              <label className="farm-form-field">
                <ModalFieldLabel>Sexo</ModalFieldLabel>
                <div className="select-wrapper">
                  <select value={form.sex} onChange={(event) => onChange('sex', event.target.value)}>
                    <option value="">No informado</option>
                    <option value="Female">Hembra</option>
                    <option value="Male">Macho</option>
                  </select>
                  <ChevronDown size={16} />
                </div>
              </label>
              <label className="farm-form-field">
                <ModalFieldLabel>Año nacimiento</ModalFieldLabel>
                <input type="number" min="1900" max="2100" value={form.birthYear} onChange={(event) => onChange('birthYear', event.target.value)} />
                {errors.birthYear && <span className="farm-inline-error">{errors.birthYear}</span>}
              </label>
              <label className="farm-form-field">
                <ModalFieldLabel>Fecha alta</ModalFieldLabel>
                <input type="date" value={form.registrationDate} onChange={(event) => onChange('registrationDate', event.target.value)} />
              </label>
              <label className="farm-form-field">
                <ModalFieldLabel>Causa alta</ModalFieldLabel>
                <div className="select-wrapper">
                  <select value={form.registrationCause} onChange={(event) => onChange('registrationCause', event.target.value)}>
                    <option value="">No informada</option>
                    <option value="Entrada">Entrada (E)</option>
                    <option value="Autorreposicion">Autorreposición (A)</option>
                  </select>
                  <ChevronDown size={16} />
                </div>
              </label>
              <label className="farm-form-field">
                <ModalFieldLabel>Procedencia</ModalFieldLabel>
                <input value={form.originCode} onChange={(event) => onChange('originCode', event.target.value)} />
                {errors.originCode && <span className="farm-inline-error">{errors.originCode}</span>}
              </label>
              <label className="farm-form-field form-full">
                <ModalFieldLabel>Serie guía entrada / salida</ModalFieldLabel>
                <input
                  value={formatAnimalGuideSeries(animal.entryGuideSerie, animal.exitGuideSerie)}
                  disabled
                />
              </label>
            </div>

            {animal.ovinoCaprino && (
              <div className="animal-specific-block">
                <h3>Datos ovino/caprino</h3>
                <div className="grid-form">
                  <label className="farm-form-field">
                    <ModalFieldLabel>Genotipado</ModalFieldLabel>
                    <input value={form.genotyping} onChange={(event) => onChange('genotyping', event.target.value)} />
                  </label>
                  <label className="farm-form-field">
                    <ModalFieldLabel>Alelo dominante</ModalFieldLabel>
                    <input value={form.dominantAllele} onChange={(event) => onChange('dominantAllele', event.target.value)} />
                  </label>
                  <label className="farm-form-field">
                    <ModalFieldLabel>Alelo bajo</ModalFieldLabel>
                    <input value={form.lowAllele} onChange={(event) => onChange('lowAllele', event.target.value)} />
                  </label>
                </div>
              </div>
            )}

            {animal.porcino && (
              <div className="animal-specific-block">
                <h3>Datos porcino</h3>
                <div className="grid-form">
                  <label className="farm-form-field">
                    <ModalFieldLabel>Tipo de animal</ModalFieldLabel>
                    <input value={form.animalType} onChange={(event) => onChange('animalType', event.target.value)} />
                    {errors.animalType && <span className="farm-inline-error">{errors.animalType}</span>}
                  </label>
                  <label className="farm-form-field">
                    <ModalFieldLabel>Fecha identificación</ModalFieldLabel>
                    <input type="date" value={form.identificationDate} onChange={(event) => onChange('identificationDate', event.target.value)} />
                  </label>
                  <label className="farm-form-field">
                    <ModalFieldLabel>Nº registro porcino</ModalFieldLabel>
                    <input value={form.pigRegistrationNumber} onChange={(event) => onChange('pigRegistrationNumber', event.target.value)} />
                  </label>
                  <label className="farm-form-field">
                    <ModalFieldLabel>Marca / crotal</ModalFieldLabel>
                    <input value={form.tag} onChange={(event) => onChange('tag', event.target.value)} />
                  </label>
                </div>
              </div>
            )}

              <div className="animal-specific-block">
                <h3>Histórico de baja</h3>
                <div className="grid-form">
                  <label className="farm-form-field">
                    <ModalFieldLabel>Causa de baja</ModalFieldLabel>
                    <input value={formatAnimalCause(animal.dischargeCause)} disabled />
                  </label>
                  <label className="farm-form-field">
                    <ModalFieldLabel>Fecha de baja</ModalFieldLabel>
                    <input value={formatDate(animal.dischargeDate)} disabled />
                  </label>
                  <label className="farm-form-field">
                    <ModalFieldLabel>Destino</ModalFieldLabel>
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

export function AnimalAutorrepositionModal({
  farm,
  form,
  errors,
  requestError,
  submitting,
  availability,
  loadingBirths,
  breedOptions,
  loadingBreedOptions,
  onChange,
  onClose,
  onSubmit
}) {
  const totalAvailable = availability?.availableAnimals ?? 0;
  const totalEligible = availability?.eligibleAnimals ?? 0;
  let rangePreview = null;
  let rangePreviewError = '';

  try {
    rangePreview = buildConsecutiveIdentificationPreview(form.startIdentification, form.quantity);
  } catch (error) {
    rangePreviewError = error.message;
  }

  return (
    <ModalDialog cardAs="form" size="wide" onSubmit={onSubmit}>
      <ModalHeader
        icon={<Tag size={18} />}
        title="Autorreposición"
        subtitle={`Convierte animales no reproductores sin identificar en reproductores identificados dentro de ${farm.name}.`}
        onClose={onClose}
      />
      <ModalBody className="operation-modal-body">
        {requestError && <div className="error-banner">{requestError}</div>}
        <div className="grid-form">
          <label className="farm-form-field">
            <span className="farm-field-label">Identificación inicial <span className="farm-field-label-required">*</span></span>
            <input value={form.startIdentification} onChange={(event) => onChange('startIdentification', event.target.value)} placeholder="ES100003542349" required />
            {errors.startIdentification && <span className="farm-inline-error">{errors.startIdentification}</span>}
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Número de animales <span className="farm-field-label-required">*</span></span>
            <input type="number" min="1" step="1" value={form.quantity} onChange={(event) => onChange('quantity', event.target.value)} required />
            {errors.quantity && <span className="farm-inline-error">{errors.quantity}</span>}
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
            <span className="farm-field-label">Fecha alta <span className="farm-field-label-required">*</span></span>
            <input type="date" value={form.registrationDate} onChange={(event) => onChange('registrationDate', event.target.value)} required />
            {errors.registrationDate && <span className="farm-inline-error">{errors.registrationDate}</span>}
          </label>
        </div>

        <div className={totalAvailable > 0 ? 'farm-settings-note' : 'info-callout info-callout-danger'}>
          {loadingBirths ? (
            <p>Cargando disponibilidad para autoreposición...</p>
          ) : totalAvailable > 0 ? (
            <>
              <strong>Disponibilidad agregada del censo</strong>
              <p>
                {totalAvailable} animales no identificados disponibles · {totalEligible} con más de 4 meses y aptos para autoreposición
              </p>
            </>
          ) : (
            <p>No hay animales no identificados disponibles para autoreposición.</p>
          )}
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
              <label className="farm-form-field">
                <ModalFieldLabel>Tipo de animal</ModalFieldLabel>
                <input value={form.animalType} onChange={(event) => onChange('animalType', event.target.value)} />
                {errors.animalType && <span className="farm-inline-error">{errors.animalType}</span>}
              </label>
              <label className="farm-form-field">
                <ModalFieldLabel>Fecha identificación</ModalFieldLabel>
                <input type="date" value={form.identificationDate} onChange={(event) => onChange('identificationDate', event.target.value)} />
              </label>
              <label className="farm-form-field">
                <ModalFieldLabel>Nº registro porcino</ModalFieldLabel>
                <input value={form.pigRegistrationNumber} onChange={(event) => onChange('pigRegistrationNumber', event.target.value)} />
              </label>
              <label className="farm-form-field">
                <ModalFieldLabel>Marca / tag</ModalFieldLabel>
                <input value={form.tag} onChange={(event) => onChange('tag', event.target.value)} />
              </label>
            </div>
          </div>
        ) : (
          <div className="animal-specific-block">
            <h3>Datos ovino/caprino</h3>
            <div className="grid-form">
              <label className="farm-form-field">
                <ModalFieldLabel>Genotipado</ModalFieldLabel>
                <input value={form.genotyping} onChange={(event) => onChange('genotyping', event.target.value)} />
              </label>
              <label className="farm-form-field">
                <ModalFieldLabel>Alelo dominante</ModalFieldLabel>
                <input value={form.dominantAllele} onChange={(event) => onChange('dominantAllele', event.target.value)} />
              </label>
              <label className="farm-form-field">
                <ModalFieldLabel>Alelo bajo</ModalFieldLabel>
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

export function ManualPorcineAnimalModal({
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
  return (
    <ModalDialog cardAs="form" size="wide" onSubmit={onSubmit}>
      <ModalHeader
        icon={<Tag size={18} />}
        title="Registrar porcino individual"
        subtitle={`Alta manual excepcional de un animal porcino identificado dentro de ${farm.name}.`}
        onClose={onClose}
      />
      <ModalBody className="operation-modal-body">
        {requestError && <div className="error-banner">{requestError}</div>}

        <div className="grid-form">
          <label className="farm-form-field">
            <span className="farm-field-label">Identificación individual <span className="farm-field-label-required">*</span></span>
            <input value={form.identification} onChange={(event) => onChange('identification', event.target.value)} placeholder="GT1800001004" required />
            {errors.identification && <span className="farm-inline-error">{errors.identification}</span>}
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Tipo de animal <span className="farm-field-label-required">*</span></span>
            <div className="select-wrapper">
              <select value={form.animalType} onChange={(event) => onChange('animalType', event.target.value)} required>
                <option value="">Selecciona un tipo</option>
                {porcineAnimalTypeOptions.map((option) => (
                  <option key={option} value={option}>{option}</option>
                ))}
              </select>
              <ChevronDown size={16} />
            </div>
            {errors.animalType && <span className="farm-inline-error">{errors.animalType}</span>}
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Raza</span>
            <div className="select-wrapper">
              <select value={form.breed} onChange={(event) => onChange('breed', event.target.value)} disabled={loadingBreedOptions}>
                <option value="">{loadingBreedOptions ? 'Cargando razas...' : 'Selecciona una raza'}</option>
                {breedOptions.map((option) => (
                  <option key={option.name} value={option.name}>
                    {option.name} ({option.code})
                  </option>
                ))}
              </select>
              <ChevronDown size={16} />
            </div>
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Sexo</span>
            <div className="select-wrapper">
              <select value={form.sex} onChange={(event) => onChange('sex', event.target.value)}>
                <option value="">No informado</option>
                <option value="Female">Hembra</option>
                <option value="Male">Macho</option>
              </select>
              <ChevronDown size={16} />
            </div>
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Año de nacimiento</span>
            <input type="number" min="1900" max="2100" value={form.birthYear} onChange={(event) => onChange('birthYear', event.target.value)} placeholder="2026" />
            {errors.birthYear && <span className="farm-inline-error">{errors.birthYear}</span>}
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Fecha de alta</span>
            <input type="date" value={form.registrationDate} onChange={(event) => onChange('registrationDate', event.target.value)} />
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Causa de alta</span>
            <div className="select-wrapper">
              <select value={form.registrationCause} onChange={(event) => onChange('registrationCause', event.target.value)}>
                <option value="Entrada">Entrada (E)</option>
                <option value="Autorreposicion">Autorreposición (A)</option>
              </select>
              <ChevronDown size={16} />
            </div>
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Código REGA de origen</span>
            <input value={form.originCode} onChange={(event) => onChange('originCode', event.target.value)} placeholder="ES061230000145" />
            {errors.originCode && <span className="farm-inline-error">{errors.originCode}</span>}
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Fecha de identificación</span>
            <input type="date" value={form.identificationDate} onChange={(event) => onChange('identificationDate', event.target.value)} />
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Nº registro porcino</span>
            <input value={form.pigRegistrationNumber} onChange={(event) => onChange('pigRegistrationNumber', event.target.value)} />
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Marca / crotal</span>
            <input value={form.tag} onChange={(event) => onChange('tag', event.target.value)} />
          </label>
        </div>
      </ModalBody>

      <ModalFooter align="end">
        <button className="secondary-button" type="button" onClick={onClose}>Cancelar</button>
        <button className="primary-button" type="submit" disabled={submitting}>
          {submitting ? 'Guardando...' : 'Registrar animal'}
        </button>
      </ModalFooter>
    </ModalDialog>
  );
}
