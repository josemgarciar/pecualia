import { useEffect, useMemo, useState } from 'react';
import {
  AlertTriangle,
  ArrowLeftRight,
  CheckCircle2,
  FileText,
  Search,
  Upload
} from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { ModalBody, ModalDialog, ModalFooter, ModalHeader, ModalStepper } from '../../shared/components/modal/Modal';
import { isValidRegaCode, normalizeRegaCode } from '../../shared/validation/identifiers';

const fullMovementImportSteps = [
  { label: 'Configuración' },
  { label: 'Archivo' },
  { label: 'Validación' },
  { label: 'Confirmación' }
];

const shortMovementImportSteps = [
  { label: 'Configuración' },
  { label: 'Confirmación' }
];

const directionLabelMap = {
  Entry: 'Entrada',
  Exit: 'Salida'
};

const movementStatusToneMap = {
  Confirmed: { bg: '#DDEBDF', color: '#2F6B4F', label: 'Confirmado' },
  Pending: { bg: '#FEF3C7', color: '#D97706', label: 'Pendiente' }
};

const previewStatusToneMap = {
  valid: { bg: '#DDEBDF', color: '#2F6B4F', label: 'Válido' },
  existing: { bg: '#FEE2E2', color: '#DC2626', label: 'Ya existente' },
  not_found: { bg: '#FCE7F3', color: '#9D174D', label: 'Nuevo' },
  duplicate: { bg: '#FEF3C7', color: '#D97706', label: 'Duplicado' },
  invalid_format: { bg: '#F3F4F6', color: '#6B7280', label: 'Inválido' },
  conflict: { bg: '#FEE2E2', color: '#DC2626', label: 'Conflicto' }
};

const porcineAnimalTypeSuggestions = [
  'Verracos',
  'Cerdas vida',
  'Hembras reposición',
  'Machos reposición',
  'Lechones',
  'Recría',
  'Cebo'
];

function getMovementWizardState({ isPorcine, unidentifiedAnimals, step }) {
  const usesShortFlow = isPorcine || unidentifiedAnimals;
  const steps = usesShortFlow ? shortMovementImportSteps : fullMovementImportSteps;

  if (!usesShortFlow) {
    return {
      steps,
      currentStep: Math.min(step, steps.length)
    };
  }

  return {
    steps,
    currentStep: step <= 1 ? 1 : 2
  };
}

function formatDateTime(value) {
  if (!value) {
    return '—';
  }

  return new Intl.DateTimeFormat('es-ES', {
    dateStyle: 'short',
    timeStyle: 'short'
  }).format(new Date(value));
}

function currentDateTimeLocalValue() {
  const now = new Date();
  now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
  return now.toISOString().slice(0, 16);
}

function localDateTimeToIso(value) {
  if (!value) {
    return null;
  }

  return new Date(value).toISOString();
}

function emptyToNull(value) {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

function buildSharedAnimalDataPayload(form, species) {
  return {
    birthYear: form.birthYear ? Number(form.birthYear) : null,
    breed: emptyToNull(form.breed),
    sex: emptyToNull(form.sex),
    ovinoCaprino: species === 'Porcine'
      ? null
      : {
          speciesType: species,
          genotyping: emptyToNull(form.genotyping),
          dominantAllele: emptyToNull(form.dominantAllele),
          lowAllele: emptyToNull(form.lowAllele)
        },
    porcino: species !== 'Porcine'
      ? null
      : {
          animalType: emptyToNull(form.animalType),
          identificationDate: emptyToNull(form.identificationDate),
          pigRegistrationNumber: emptyToNull(form.pigRegistrationNumber),
          tag: emptyToNull(form.tag)
        }
  };
}

function SharedAnimalDataFields({ species, form, onChange, breedOptions, loadingBreedOptions }) {
  return (
    <div className="movement-shared-data stack">
      <div className="movement-section-copy">
        <h3>Datos comunes de los nuevos animales</h3>
        <p>Solo se aplicarán a las identificaciones que no existan aún en Pecualia.</p>
      </div>

      <div className="grid-form">
        <label className="farm-form-field">
          <span className="farm-field-label">Raza</span>
          <select value={form.breed} onChange={(event) => onChange('breed', event.target.value)} disabled={loadingBreedOptions}>
            <option value="">{loadingBreedOptions ? 'Cargando razas...' : 'Selecciona una raza'}</option>
            {breedOptions.map((option) => (
              <option key={option.name} value={option.name}>
                {option.name} ({option.code})
              </option>
            ))}
          </select>
        </label>
        <label className="farm-form-field">
          <span className="farm-field-label">Sexo</span>
          <select value={form.sex} onChange={(event) => onChange('sex', event.target.value)}>
            <option value="">Selecciona</option>
            <option value="Female">Hembra</option>
            <option value="Male">Macho</option>
          </select>
        </label>
        <label className="farm-form-field">
          <span className="farm-field-label">Año de nacimiento</span>
          <input
            type="number"
            min="2000"
            max="2100"
            value={form.birthYear}
            onChange={(event) => onChange('birthYear', event.target.value)}
          />
        </label>
        {species === 'Porcine' ? (
          <>
            <label className="farm-form-field">
              <span className="farm-field-label">Tipo animal</span>
              <input value={form.animalType} onChange={(event) => onChange('animalType', event.target.value)} />
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
              <span className="farm-field-label">Marca / tag</span>
              <input value={form.tag} onChange={(event) => onChange('tag', event.target.value)} />
            </label>
          </>
        ) : (
          <>
            <label className="farm-form-field">
              <span className="farm-field-label">Genotipado</span>
              <input value={form.genotyping} onChange={(event) => onChange('genotyping', event.target.value)} />
            </label>
            <label className="farm-form-field">
              <span className="farm-field-label">Alelo dominante</span>
              <input value={form.dominantAllele} onChange={(event) => onChange('dominantAllele', event.target.value)} />
            </label>
            <label className="farm-form-field">
              <span className="farm-field-label">Alelo recesivo</span>
              <input value={form.lowAllele} onChange={(event) => onChange('lowAllele', event.target.value)} />
            </label>
          </>
        )}
      </div>
    </div>
  );
}

function MovementDetailModal({ farm, movement, loading, confirming, onClose, onViewAnimals, onConfirm }) {
  const tone = movement ? (movementStatusToneMap[movement.status] ?? movementStatusToneMap.Pending) : movementStatusToneMap.Pending;
  const direction = movement
    ? (movement.originFarmId === farm.id ? 'Salida' : 'Entrada')
    : null;
  const isPorcineAggregateMovement = Boolean(
    movement &&
    movement.livestockSpecies === 'Porcine' &&
    movement.animals.length === 0 &&
    movement.animalType
  );

  return (
    <ModalDialog size="wide" shellClassName="movement-detail-modal-shell">
      <ModalHeader
        icon={<FileText size={18} />}
        title="Detalle de la guía"
        subtitle="Información completa del movimiento y animales asociados."
        onClose={onClose}
      />
      <ModalBody>
          {loading ? (
            <div className="empty-state">Cargando detalle del movimiento...</div>
          ) : !movement ? (
            <div className="empty-state">No se ha podido cargar el detalle del movimiento.</div>
          ) : (
            <div className="stack">
              <div className="animal-detail-hero movement-detail-hero">
                <div className="animal-detail-hero-top">
                  <div>
                    <span>Guía REMO</span>
                    <strong>{movement.codRemo}</strong>
                    <p>{movement.serie ?? 'Sin serie'}</p>
                  </div>
                  <span className="animal-chip" style={{ background: tone.bg, color: tone.color }}>
                    {tone.label}
                  </span>
                </div>
              </div>

              <div className="movement-detail-summary-grid">
                <div className="movement-detail-group">
                  <span>Dirección</span>
                  <strong>{direction ?? 'No disponible'}</strong>
                </div>
                <div className="movement-detail-group">
                  <span>Especie</span>
                  <strong>{movement.livestockSpecies}</strong>
                </div>
                <div className="movement-detail-group">
                  <span>Salida</span>
                  <strong>{formatDateTime(movement.departureDate)}</strong>
                </div>
                <div className="movement-detail-group">
                  <span>Llegada</span>
                  <strong>{formatDateTime(movement.arrivalDate)}</strong>
                </div>
                <div className="movement-detail-group">
                  <span>Solicitud</span>
                  <strong>{formatDateTime(movement.solicitationDate)}</strong>
                </div>
                <div className="movement-detail-group">
                  <span>Animales</span>
                  <strong>{movement.numberOfAnimals}</strong>
                </div>
                {movement.animalType && (
                  <div className="movement-detail-group">
                    <span>Tipo animal</span>
                    <strong>{movement.animalType}</strong>
                  </div>
                )}
              </div>

              <div className="movement-detail-body movement-detail-body-modal">
                <div className="movement-detail-group">
                  <span>Origen</span>
                  <strong>{movement.originName ?? 'Explotación externa'}</strong>
                  <small>{movement.originCode ?? 'Sin código'}</small>
                </div>
                <div className="movement-detail-group">
                  <span>Destino</span>
                  <strong>{movement.destinationName ?? 'Explotación externa'}</strong>
                  <small>{movement.destinationCode ?? 'Sin código'}</small>
                </div>
                <div className="movement-detail-group">
                  <span>Medio de transporte</span>
                  <strong>{movement.meansOfTransport ?? 'No informado'}</strong>
                </div>
                <div className="movement-detail-group">
                  <span>Transportista</span>
                  <strong>{movement.transportName ?? 'No informado'}</strong>
                  <small>{movement.vehicleRegistrationNumber ?? 'Sin matrícula'}</small>
                </div>
              </div>

              <section className="movement-detail-animals-section">
                <div className="movement-section-copy">
                  <h3>{isPorcineAggregateMovement ? 'Movimiento agregado porcino' : 'Animales asociados'}</h3>
                  <p>
                    {isPorcineAggregateMovement
                      ? 'Esta guía porcina se ha registrado por número de cabezas y tipo de animal, sin identificaciones individuales.'
                      : `${movement.animals.length} animales vinculados a esta guía. Para volúmenes altos, consulta el listado filtrado de la explotación.`}
                  </p>
                </div>

                <div className="movement-detail-animals-summary">
                  <strong>{movement.numberOfAnimals} {isPorcineAggregateMovement ? 'animales declarados' : 'animales asociados'}</strong>
                  <span>
                    {isPorcineAggregateMovement
                      ? `Tipo declarado en la guía: ${movement.animalType}.`
                      : 'Abre el tab de animales con el filtro de esta guía para revisarlos de forma óptima.'}
                  </span>
                </div>

                {!isPorcineAggregateMovement && (
                  <div className="movement-detail-animals-actions">
                    <button className="primary-button" type="button" onClick={() => onViewAnimals(movement)}>
                      Ver animales de esta guía
                    </button>
                  </div>
                )}
              </section>
            </div>
          )}
      </ModalBody>

      <ModalFooter align="end">
          {movement?.status === 'Pending' && (
            <button className="secondary-button" type="button" onClick={onConfirm} disabled={loading || confirming}>
              {confirming ? 'Confirmando...' : 'Confirmar guía'}
            </button>
          )}
          <button className="primary-button" type="button" onClick={onClose}>Cerrar</button>
      </ModalFooter>
    </ModalDialog>
  );
}

function MovementImportModal({ farm, token, onClose, onCommitted }) {
  const [step, setStep] = useState(1);
  const [config, setConfig] = useState({
    direction: 'Exit',
    counterpartyExternalCode: '',
    counterpartyExternalName: '',
    codRemo: '',
    serie: '',
    departureDate: currentDateTimeLocalValue(),
    arrivalDate: currentDateTimeLocalValue(),
    solicitationDate: currentDateTimeLocalValue(),
    meansOfTransport: '',
    transportName: '',
    vehicleRegistrationNumber: '',
    numberOfAnimals: '',
    breed: '',
    animalType: ''
  });
  const [sharedAnimalData, setSharedAnimalData] = useState({
    birthYear: '',
    breed: '',
    sex: '',
    genotyping: '',
    dominantAllele: '',
    lowAllele: '',
    animalType: '',
    identificationDate: '',
    pigRegistrationNumber: '',
    tag: ''
  });
  const [fileName, setFileName] = useState('');
  const [rawText, setRawText] = useState('');
  const [preview, setPreview] = useState(null);
  const [filterStatus, setFilterStatus] = useState('');
  const [search, setSearch] = useState('');
  const [requestError, setRequestError] = useState('');
  const [loadingPreview, setLoadingPreview] = useState(false);
  const [loadingBreedOptions, setLoadingBreedOptions] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState(null);
  const [breedOptions, setBreedOptions] = useState([]);
  const [unidentifiedAnimals, setUnidentifiedAnimals] = useState(false);
  const [unidentifiedAnimalCount, setUnidentifiedAnimalCount] = useState('');
  const [unidentifiedCategory, setUnidentifiedCategory] = useState('Under4Months');

  const isOvineOrCaprine = farm.livestockSpecies === 'Ovine' || farm.livestockSpecies === 'Caprine';
  const isPorcine = farm.livestockSpecies === 'Porcine';
  const wizardState = useMemo(
    () => getMovementWizardState({ isPorcine, unidentifiedAnimals, step }),
    [isPorcine, step, unidentifiedAnimals]
  );

  const filteredPreviewRows = useMemo(() => {
    const rows = preview?.rows ?? [];
    return rows.filter((row) => {
      const matchesStatus = !filterStatus || row.status === filterStatus;
      const matchesSearch = !search || row.identification.toLowerCase().includes(search.toLowerCase());
      return matchesStatus && matchesSearch;
    });
  }, [filterStatus, preview, search]);

  const derivedOperation = config.direction === 'Entry' ? 'Alta' : 'Baja';
  const derivedCause = config.direction === 'Entry' ? 'Entrada' : 'Salida';
  const directionLabel = config.direction === 'Entry' ? 'entrada' : 'salida';
  const processableRowsCount = preview
    ? (config.direction === 'Entry' ? preview.summary.notFoundRows : preview.summary.validRows)
    : 0;
  const rejectedRowsCount = preview
    ? preview.summary.totalLines - processableRowsCount
    : 0;
  const requiresSharedAnimalData = Boolean(preview?.requiresSharedAnimalData);
  const isSharedAnimalDataReady = !requiresSharedAnimalData || (
    !loadingBreedOptions &&
    Boolean(sharedAnimalData.breed) &&
    Boolean(sharedAnimalData.sex) &&
    (farm.livestockSpecies !== 'Porcine' || Boolean(sharedAnimalData.animalType))
  );
  const canContinueStep1 = Boolean(
    config.direction &&
    config.departureDate &&
    config.arrivalDate &&
    config.serie.trim() &&
    config.counterpartyExternalCode.trim() &&
    config.counterpartyExternalName.trim() &&
    (!isPorcine || (config.numberOfAnimals && config.breed && config.animalType.trim()))
  );

  function validateStep1() {
    if (!canContinueStep1) {
      const missing = [];
      if (!config.serie.trim()) missing.push('la serie');
      if (!config.counterpartyExternalCode.trim()) missing.push('el código REGA de la contraparte');
      if (!config.counterpartyExternalName.trim()) missing.push('el nombre de la explotación');
      if (isPorcine && !config.numberOfAnimals) missing.push('el número de animales');
      if (isPorcine && !config.breed) missing.push('la raza');
      if (isPorcine && !config.animalType.trim()) missing.push('el tipo de animal');
      return missing.length > 0
        ? `Completa ${missing.join(', ')} antes de continuar.`
        : 'Completa todos los campos obligatorios antes de continuar.';
    }

    if (!isValidRegaCode(config.counterpartyExternalCode)) {
      return 'El código REGA no es válido.';
    }

    if (isPorcine) {
      const count = Number(config.numberOfAnimals);
      if (!count || count < 1 || count > 10000) {
        return 'Indica un número de animales entre 1 y 10.000.';
      }
    }

    return '';
  }

  function updateConfig(field, value) {
    setConfig((current) => ({ ...current, [field]: value }));
    setRequestError('');
  }

  function updateSharedField(field, value) {
    setSharedAnimalData((current) => ({ ...current, [field]: value }));
  }

  function handleContinueFromStep1() {
    const validationMessage = validateStep1();
    if (validationMessage) {
      setRequestError(validationMessage);
      return;
    }

    if (isPorcine) {
      setStep(4);
      return;
    }

    if (unidentifiedAnimals) {
      const count = Number(unidentifiedAnimalCount);
      if (!count || count < 1 || count > 10000) {
        setRequestError('Indica un número de animales entre 1 y 10.000.');
        return;
      }
      if (!unidentifiedCategory) {
        setRequestError('Selecciona la categoría de edad de los animales sin identificar.');
        return;
      }
      setStep(4);
      return;
    }

    setStep(2);
  }

  useEffect(() => {
    let cancelled = false;

    async function loadBreedOptions() {
      setLoadingBreedOptions(true);

      try {
        const response = await apiRequest(`/api/movements/breeds/${farm.livestockSpecies}`, { token });
        if (!cancelled) {
          setBreedOptions(response);
        }
      } catch (error) {
        if (!cancelled) {
          setRequestError(error.message);
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
  }, [farm.livestockSpecies, token]);

  async function handleFileSelected(event) {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    const text = await file.text();
    setFileName(file.name);
    setRawText(text);
    setRequestError('');
  }

  async function handlePreview() {
    const validationMessage = validateStep1();
    if (validationMessage) {
      setRequestError(validationMessage);
      return;
    }

    setLoadingPreview(true);
    setRequestError('');

    try {
      const response = await apiRequest('/api/movements/imports/preview', {
        method: 'POST',
        token,
        body: {
          farmId: farm.id,
          operation: derivedOperation,
          counterpartyExternalCode: emptyToNull(normalizeRegaCode(config.counterpartyExternalCode)),
          counterpartyExternalName: emptyToNull(config.counterpartyExternalName),
          codRemo: emptyToNull(config.codRemo),
          serie: emptyToNull(config.serie),
          departureDate: localDateTimeToIso(config.departureDate),
          arrivalDate: localDateTimeToIso(config.arrivalDate),
          solicitationDate: localDateTimeToIso(config.solicitationDate),
          meansOfTransport: emptyToNull(config.meansOfTransport),
          transportName: emptyToNull(config.transportName),
          vehicleRegistrationNumber: emptyToNull(config.vehicleRegistrationNumber),
          cause: derivedCause,
          numberOfAnimals: isPorcine ? Number(config.numberOfAnimals) : null,
          breed: isPorcine ? emptyToNull(config.breed) : null,
          animalType: isPorcine ? emptyToNull(config.animalType) : null,
          rawText,
          sharedAnimalData: null
        }
      });

      setPreview(response);
      setStep(3);
    } catch (error) {
      setRequestError(error.message);
    } finally {
      setLoadingPreview(false);
    }
  }

  async function handleCommit() {
    const validationMessage = validateStep1();
    if (validationMessage) {
      setRequestError(validationMessage);
      return;
    }

    setSubmitting(true);
    setRequestError('');

    try {
      const body = {
        farmId: farm.id,
        operation: derivedOperation,
        counterpartyExternalCode: emptyToNull(normalizeRegaCode(config.counterpartyExternalCode)),
        counterpartyExternalName: emptyToNull(config.counterpartyExternalName),
        codRemo: emptyToNull(config.codRemo),
        serie: emptyToNull(config.serie),
        departureDate: localDateTimeToIso(config.departureDate),
        arrivalDate: localDateTimeToIso(config.arrivalDate),
        solicitationDate: localDateTimeToIso(config.solicitationDate),
        meansOfTransport: emptyToNull(config.meansOfTransport),
        transportName: emptyToNull(config.transportName),
        vehicleRegistrationNumber: emptyToNull(config.vehicleRegistrationNumber),
        cause: derivedCause,
        numberOfAnimals: isPorcine ? Number(config.numberOfAnimals) : null,
        breed: isPorcine ? emptyToNull(config.breed) : null,
        animalType: isPorcine ? emptyToNull(config.animalType) : null,
        rawText: unidentifiedAnimals ? null : rawText,
        sharedAnimalData: preview?.requiresSharedAnimalData ? buildSharedAnimalDataPayload(sharedAnimalData, farm.livestockSpecies) : null,
        unidentifiedAnimalCount: unidentifiedAnimals ? Number(unidentifiedAnimalCount) : null,
        unidentifiedCategory: unidentifiedAnimals ? unidentifiedCategory : null
      };

      const response = await apiRequest('/api/movements/imports/commit', {
        method: 'POST',
        token,
        body
      });

      onCommitted(response);
      onClose();
    } catch (error) {
      setRequestError(error.message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <ModalDialog size="wide" shellClassName="movement-modal-shell">
      <ModalHeader
        icon={<Upload size={18} />}
        title="Nuevo Movimiento"
        subtitle="Entrada o salida masiva de animales sobre la explotación seleccionada."
        onClose={onClose}
      />
      <ModalStepper steps={wizardState.steps} currentStep={wizardState.currentStep} className="movement-modal-stepper" />
      <ModalBody>
          {requestError && <div className="error-banner">{requestError}</div>}

          {step === 1 && (
            <div className="stack">
              <div className="grid-form">
                <div className="farm-form-field form-full">
                  <span className="farm-field-label">Explotación</span>
                  <div className="movement-selected-farm">
                    <strong>{farm.name}</strong>
                    <span>REGA {farm.regaCode} · {farm.livestockSpecies}</span>
                  </div>
                                {isOvineOrCaprine && (
                <div className="movement-unidentified-section">
                  <label className="movement-unidentified-checkbox">
                    <input
                      type="checkbox"
                      checked={unidentifiedAnimals}
                      onChange={(event) => {
                        setUnidentifiedAnimals(event.target.checked);
                        if (!event.target.checked) {
                          setUnidentifiedCategory('Under4Months');
                          setUnidentifiedAnimalCount('');
                        }
                        setRequestError('');
                      }}
                    />
                    <div>
                      <strong>Animales sin identificar individualmente</strong>
                      <span>Registra la guía solo con el número de cabezas y clasifica si son no reproductores menores de 4 meses o de 4 a 12 meses para actualizar automáticamente el censo.</span>
                    </div>
                  </label>
                  {unidentifiedAnimals && (
                    <>
                      <label className="farm-form-field movement-unidentified-count">
                        <span className="farm-field-label">Categoría de edad <span className="farm-field-label-required">*</span></span>
                        <select
                          value={unidentifiedCategory}
                          onChange={(event) => {
                            setUnidentifiedCategory(event.target.value);
                            setRequestError('');
                          }}
                        >
                          <option value="Under4Months">No reproductores menores de 4 meses</option>
                          <option value="Between4And12Months">No reproductores de 4 a 12 meses</option>
                        </select>
                      </label>
                      <label className="farm-form-field movement-unidentified-count">
                        <span className="farm-field-label">Número de animales <span className="farm-field-label-required">*</span></span>
                        <input
                          type="number"
                          min="1"
                          max="10000"
                          value={unidentifiedAnimalCount}
                          onChange={(event) => {
                            setUnidentifiedAnimalCount(event.target.value);
                            setRequestError('');
                          }}
                          placeholder="Ej. 25"
                        />
                      </label>
                    </>
                  )}
                </div>
              )}
                </div>

                <label className="farm-form-field movement-direction-field">
                  <span className="farm-field-label">Tipo de movimiento</span>
                  <select
                    value={config.direction}
                    onChange={(event) => updateConfig('direction', event.target.value)}
                  >
                    <option value="Entry">Entrada</option>
                    <option value="Exit">Salida</option>
                  </select>
                </label>

                {isPorcine && (
                  <>
                    <label className="farm-form-field">
                      <span className="farm-field-label">Número de animales <span className="farm-field-label-required">*</span></span>
                      <input
                        type="number"
                        min="1"
                        max="10000"
                        value={config.numberOfAnimals}
                        onChange={(event) => updateConfig('numberOfAnimals', event.target.value)}
                        placeholder="Ej. 25"
                      />
                    </label>
                    <label className="farm-form-field">
                      <span className="farm-field-label">Raza <span className="farm-field-label-required">*</span></span>
                      <select value={config.breed} onChange={(event) => updateConfig('breed', event.target.value)} disabled={loadingBreedOptions}>
                        <option value="">{loadingBreedOptions ? 'Cargando razas...' : 'Selecciona una raza'}</option>
                        {breedOptions.map((option) => (
                          <option key={option.name} value={option.name}>
                            {option.name} ({option.code})
                          </option>
                        ))}
                      </select>
                    </label>
                    <label className="farm-form-field">
                      <span className="farm-field-label">Tipo de animal <span className="farm-field-label-required">*</span></span>
                      <select value={config.animalType} onChange={(event) => updateConfig('animalType', event.target.value)}>
                        <option value="">Selecciona un tipo</option>
                        {porcineAnimalTypeSuggestions.map((option) => (
                          <option key={option} value={option}>{option}</option>
                        ))}
                      </select>
                    </label>
                  </>
                )}

                <label className="farm-form-field">
                  <span className="farm-field-label">Fecha de salida</span>
                  <input type="datetime-local" value={config.departureDate} onChange={(event) => updateConfig('departureDate', event.target.value)} />
                </label>
                <label className="farm-form-field">
                  <span className="farm-field-label">Fecha de llegada</span>
                  <input type="datetime-local" value={config.arrivalDate} onChange={(event) => updateConfig('arrivalDate', event.target.value)} />
                </label>
                <label className="farm-form-field">
                  <span className="farm-field-label">Serie <span className="farm-field-label-required">*</span></span>
                  <input value={config.serie} onChange={(event) => updateConfig('serie', event.target.value)} />
                </label>

                <label className="farm-form-field">
                  <span className="farm-field-label">Código REMO</span>
                  <input value={config.codRemo} onChange={(event) => updateConfig('codRemo', event.target.value)} />
                </label>

                <label className="farm-form-field">
                  <span className="farm-field-label">
                    {config.direction === 'Entry' ? 'Nombre de explotación origen' : 'Nombre de explotación destino'}
                    <span className="farm-field-label-required"> *</span>
                  </span>
                  <input value={config.counterpartyExternalName} onChange={(event) => updateConfig('counterpartyExternalName', event.target.value)} />
                </label>
                <label className="farm-form-field">
                  <span className="farm-field-label">
                    {config.direction === 'Entry' ? 'Código REGA de origen' : 'Código REGA de destino'}
                    <span className="farm-field-label-required"> *</span>
                  </span>
                  <input
                    value={config.counterpartyExternalCode}
                    onChange={(event) => updateConfig('counterpartyExternalCode', event.target.value)}
                  />
                </label>

                <label className="farm-form-field">
                  <span className="farm-field-label">Medio de transporte</span>
                  <input value={config.meansOfTransport} onChange={(event) => updateConfig('meansOfTransport', event.target.value)} />
                </label>
                <label className="farm-form-field">
                  <span className="farm-field-label">Transportista</span>
                  <input value={config.transportName} onChange={(event) => updateConfig('transportName', event.target.value)} />
                </label>
                <label className="farm-form-field">
                  <span className="farm-field-label">Matrícula</span>
                  <input value={config.vehicleRegistrationNumber} onChange={(event) => updateConfig('vehicleRegistrationNumber', event.target.value)} />
                </label>
              </div>
            </div>
          )}

          {step === 2 && (
            <div className="stack">
              <label className="movement-upload-box">
                <Upload size={20} />
                <strong>{fileName || 'Selecciona un archivo TXT'}</strong>
                <span>También puedes pegar el contenido manualmente en el cuadro inferior.</span>
                <input type="file" accept=".txt,text/plain" onChange={handleFileSelected} hidden />
              </label>

              <label className="farm-form-field">
                <span className="farm-field-label">Contenido TXT</span>
                <textarea
                  rows={10}
                  value={rawText}
                  onChange={(event) => setRawText(event.target.value)}
                  placeholder={farm.livestockSpecies === 'Porcine' ? 'GT12345678' : 'ES100008594650'}
                />
              </label>
            </div>
          )}

          {step === 3 && preview && (
            <div className="stack">
              <div className="movement-preview-summary-grid">
                <article className="movement-preview-summary-card">
                  <span>Total líneas</span>
                  <strong>{preview.summary.totalLines}</strong>
                </article>
                <article className="movement-preview-summary-card">
                  <span>Procesables</span>
                  <strong>{processableRowsCount}</strong>
                </article>
                <article className="movement-preview-summary-card">
                  <span>Duplicadas</span>
                  <strong>{preview.summary.duplicateRows}</strong>
                </article>
                <article className="movement-preview-summary-card">
                  <span>Inválidas / conflicto</span>
                  <strong>{preview.summary.invalidFormatRows + preview.summary.conflictRows}</strong>
                </article>
              </div>

              <div className="animal-filters farm-animals-filters">
                <div className="animal-search">
                  <Search size={14} />
                  <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Buscar identificación..." />
                </div>
                <select value={filterStatus} onChange={(event) => setFilterStatus(event.target.value)}>
                  <option value="">Todos los estados</option>
                  {Object.entries(previewStatusToneMap).map(([key, value]) => (
                    <option key={key} value={key}>{value.label}</option>
                  ))}
                </select>
              </div>

              <div className="animal-table-card">
                <div className="table-scroll">
                  <table className="animal-table">
                    <thead>
                      <tr>
                        {['Línea', 'Identificación', 'Estado', 'Acción', 'Detalle'].map((header) => <th key={header}>{header}</th>)}
                      </tr>
                    </thead>
                    <tbody>
                      {filteredPreviewRows.map((row) => {
                        const tone = previewStatusToneMap[row.status] ?? previewStatusToneMap.conflict;
                        return (
                          <tr key={`${row.lineNumber}-${row.identification}`}>
                            <td>{row.lineNumber}</td>
                            <td><strong>{row.identification}</strong></td>
                            <td><span className="animal-chip" style={{ background: tone.bg, color: tone.color }}>{tone.label}</span></td>
                            <td>{row.action}</td>
                            <td>{row.message}</td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
                <div className="animal-table-footer">{filteredPreviewRows.length} filas visibles</div>
              </div>
            </div>
          )}

          {step === 4 && (
            <div className="stack">
              <section className="movement-confirmation-card">
                <div className="movement-section-copy">
                  <h3>Resumen de importación</h3>
                  {isPorcine ? (
                    <p>
                      Se registrará un movimiento porcino de <strong>{config.numberOfAnimals}</strong> animales como {directionLabel},
                      clasificados como <strong>{config.animalType}</strong> y de raza <strong>{config.breed}</strong>.
                      El sistema actualizará automáticamente el balance y el censo
                    </p>
                  ) : unidentifiedAnimals ? (
                    <p>
                      Se registrará un movimiento de <strong>{unidentifiedAnimalCount}</strong> animales sin identificar como {directionLabel} en la categoría <strong>{unidentifiedCategory === 'Under4Months' ? 'no reproductores menores de 4 meses' : 'no reproductores de 4 a 12 meses'}</strong>.
                    </p>
                  ) : (
                    <p>
                      Se procesarán {processableRowsCount} identificaciones como {directionLabel} masiva.
                      {rejectedRowsCount > 0 && (
                        <> {rejectedRowsCount} quedarán excluidas.</>
                      )}
                    </p>
                  )}
                </div>
              </section>

              {!unidentifiedAnimals && requiresSharedAnimalData && (
                <>
                  <section className="movement-confirmation-card">
                    <div className="movement-section-copy">
                      <h3>Datos derivados de la guía</h3>
                      <p>Estos datos se aplicarán automáticamente a los animales nuevos a partir de la configuración de la guía.</p>
                    </div>
                    <div className="movement-detail-summary-grid">
                      <div className="movement-detail-group">
                        <span>{config.direction === 'Entry' ? 'Código de origen' : 'Código de destino'}</span>
                        <strong>{config.counterpartyExternalCode || 'No informado'}</strong>
                      </div>
                      <div className="movement-detail-group">
                        <span>Serie guía</span>
                        <strong>{config.serie || 'No informada'}</strong>
                      </div>
                    </div>
                  </section>
                  <SharedAnimalDataFields
                    species={farm.livestockSpecies}
                    form={sharedAnimalData}
                    onChange={updateSharedField}
                    breedOptions={breedOptions}
                    loadingBreedOptions={loadingBreedOptions}
                  />
                </>
              )}
            </div>
          )}

          {step === 5 && result && (
            <div className="farm-success-card">
              <div className="farm-success-icon">
                <CheckCircle2 size={32} />
              </div>
              <div className="farm-success-copy">
                <h2>Importación completada</h2>
                <p>Guía {result.codRemo} registrada correctamente.</p>
                <span>{result.processedRows} identificaciones procesadas · {result.rejectedRows} rechazadas</span>
              </div>
            </div>
          )}
      </ModalBody>

        {step < 5 && (
          <ModalFooter>
            <button className="secondary-button" type="button" onClick={() => {
              if (step === 1) {
                onClose();
              } else if (step === 4 && (unidentifiedAnimals || isPorcine)) {
                setStep(1);
              } else {
                setStep((current) => current - 1);
              }
            }}>
              {step === 1 ? 'Cancelar' : 'Volver'}
            </button>
            <div className="movement-footer-actions">
              {step === 1 && <button className="primary-button" type="button" onClick={handleContinueFromStep1}>Continuar</button>}
              {step === 2 && <button className="primary-button" type="button" onClick={handlePreview} disabled={loadingPreview}>{loadingPreview ? 'Validando...' : 'Validar archivo'}</button>}
              {step === 3 && <button className="primary-button" type="button" onClick={() => setStep(4)}>Continuar</button>}
              {step === 4 && (
                <button
                  className="primary-button"
                  type="button"
                  onClick={handleCommit}
                  disabled={submitting || (!isPorcine && !unidentifiedAnimals && (processableRowsCount === 0 || !isSharedAnimalDataReady))}
                >
                  {submitting ? 'Registrando...' : 'Confirmar importación'}
                </button>
              )}
            </div>
          </ModalFooter>
        )}
        {step === 5 && result && (
          <ModalFooter align="end">
            <button className="primary-button farm-success-button" type="button" onClick={onClose}>Cerrar</button>
          </ModalFooter>
        )}
    </ModalDialog>
  );
}

export function FarmMovementsSection({ farm, token, onViewAnimalsForMovement }) {
  const [movements, setMovements] = useState([]);
  const [selectedMovementId, setSelectedMovementId] = useState(null);
  const [selectedMovement, setSelectedMovement] = useState(null);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [confirmingMovement, setConfirmingMovement] = useState(false);
  const [error, setError] = useState('');
  const [importOpen, setImportOpen] = useState(false);

  function closeDetailModal() {
    setSelectedMovementId(null);
    setSelectedMovement(null);
  }

  async function loadMovements(keepSelection = true) {
    setLoading(true);
    setError('');

    try {
      const movementResponse = await apiRequest(`/api/farms/${farm.id}/movements`, { token });
      setMovements(movementResponse);

      if (keepSelection && selectedMovementId && movementResponse.some((entry) => entry.id === selectedMovementId)) {
        setSelectedMovementId(selectedMovementId);
      } else {
        setSelectedMovementId(null);
        setSelectedMovement(null);
      }
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setLoading(false);
    }
  }

  async function confirmSelectedMovement() {
    if (!selectedMovementId) {
      return;
    }

    setConfirmingMovement(true);
    setError('');
    try {
      await apiRequest(`/api/movements/${selectedMovementId}/confirm`, {
        method: 'POST',
        token
      });

      await loadMovements(true);
      const response = await apiRequest(`/api/movements/${selectedMovementId}`, { token });
      setSelectedMovement(response);
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setConfirmingMovement(false);
    }
  }

  useEffect(() => {
    loadMovements(false);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [farm.id, token]);

  useEffect(() => {
    let cancelled = false;

    async function loadMovementDetail() {
      if (!selectedMovementId) {
        setSelectedMovement(null);
        return;
      }

      setDetailLoading(true);
      setSelectedMovement(null);
      try {
        const response = await apiRequest(`/api/movements/${selectedMovementId}`, { token });
        if (!cancelled) {
          setSelectedMovement(response);
        }
      } catch (requestError) {
        if (!cancelled) {
          setError(requestError.message);
          setSelectedMovement(null);
        }
      } finally {
        if (!cancelled) {
          setDetailLoading(false);
        }
      }
    }

    loadMovementDetail();
    return () => {
      cancelled = true;
    };
  }, [selectedMovementId, token]);

  const filteredMovements = useMemo(() => (
    movements.filter((movement) => {
      const normalizedSearch = search.trim().toLowerCase();
      if (!normalizedSearch) {
        return true;
      }

      return movement.codRemo.toLowerCase().includes(normalizedSearch) ||
        movement.counterpartyName.toLowerCase().includes(normalizedSearch) ||
        movement.counterpartyCode.toLowerCase().includes(normalizedSearch);
    })
  ), [movements, search]);

  if (loading) {
    return <div className="panel-card empty-state">Cargando movimientos de la explotación...</div>;
  }

  return (
    <>
      <section className="panel-card stack">
        <div className="farm-animals-header movement-toolbar">
          <div>
            <p>{movements.length} guías de movimiento registradas</p>
          </div>
          <div className="movement-toolbar-actions">
            <button className="primary-button" type="button" onClick={() => setImportOpen(true)}>
              <Upload size={16} />
              Nuevo Movimiento
            </button>
          </div>
        </div>

        {error && <div className="error-banner">{error}</div>}

        <div className="animal-filters farm-animals-filters">
          <div className="animal-search">
            <Search size={14} />
            <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Buscar por guía, nombre o código..." />
          </div>
        </div>

        {filteredMovements.length === 0 ? (
          <div className="empty-state">
            <FileText size={28} />
            <div>No hay movimientos que coincidan con los filtros.</div>
          </div>
        ) : (
          <div className="animal-table-card">
            <div className="table-scroll">
              <table className="animal-table">
                <thead>
                  <tr>
                    {['Guía', 'Dirección', 'Explotación', 'Animales', 'Salida', 'Llegada', 'Estado'].map((header) => (
                      <th key={header}>{header}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {filteredMovements.map((movement) => {
                    const tone = movementStatusToneMap[movement.status] ?? movementStatusToneMap.Pending;
                    return (
                      <tr
                        key={movement.id}
                        onClick={() => setSelectedMovementId(movement.id)}
                        className={selectedMovementId === movement.id ? 'animal-row-selected' : undefined}
                      >
                        <td><strong>{movement.serie}</strong></td>
                        <td>{directionLabelMap[movement.direction] ?? movement.direction}</td>
                        <td>{movement.counterpartyName}</td>
                        <td>
                          <div className="animal-identification-cell">
                            <ArrowLeftRight size={13} />
                            <strong>{movement.numberOfAnimals}</strong>
                          </div>
                        </td>
                        <td>{formatDateTime(movement.departureDate)}</td>
                        <td>{formatDateTime(movement.arrivalDate)}</td>
                        <td><span className="animal-chip" style={{ background: tone.bg, color: tone.color }}>{tone.label}</span></td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
            <div className="animal-table-footer">{filteredMovements.length} movimientos</div>
          </div>
        )}
      </section>

      {importOpen && (
        <MovementImportModal
          farm={farm}
          token={token}
          onClose={() => setImportOpen(false)}
          onCommitted={() => {
            loadMovements(false);
          }}
        />
      )}

      {selectedMovementId && (
        <MovementDetailModal
          farm={farm}
          movement={selectedMovement}
          loading={detailLoading}
          confirming={confirmingMovement}
          onViewAnimals={(movement) => {
            closeDetailModal();
            onViewAnimalsForMovement(movement);
          }}
          onConfirm={confirmSelectedMovement}
          onClose={closeDetailModal}
        />
      )}
    </>
  );
}
