import { useEffect, useMemo, useState } from 'react';
import {
  AlertTriangle,
  ArrowLeftRight,
  CheckCircle2,
  FileText,
  Plus,
  Search,
  Upload,
  X
} from 'lucide-react';
import { apiRequest } from '../../shared/api/client';

const directionLabelMap = {
  Entry: 'Entrada',
  Exit: 'Salida'
};

const counterpartyTypeLabelMap = {
  Internal: 'Interna',
  External: 'Externa'
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

const registrationCauseOptions = [
  { value: 'Entrada', label: 'Entrada (E)' },
  { value: 'Autorreposicion', label: 'Autorreposición (A)' }
];

const dischargeCauseOptions = [
  { value: 'Salida', label: 'Salida (S)' },
  { value: 'Muerte', label: 'Muerte (M)' }
];

function formatDate(value) {
  if (!value) {
    return '—';
  }

  return new Intl.DateTimeFormat('es-ES').format(new Date(`${value}T00:00:00`));
}

function todayInputValue() {
  return new Date().toISOString().slice(0, 10);
}

function emptyToNull(value) {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

function toTextareaLines(value) {
  return value
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);
}

function buildSharedAnimalDataPayload(form, species) {
  return {
    birthYear: form.birthYear ? Number(form.birthYear) : null,
    breed: emptyToNull(form.breed),
    sex: emptyToNull(form.sex),
    registrationCause: form.registrationCause || null,
    originCode: emptyToNull(form.originCode),
    healthDocumentNumber: emptyToNull(form.healthDocumentNumber),
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

function SharedAnimalDataFields({ species, form, onChange }) {
  return (
    <div className="movement-shared-data stack">
      <div className="movement-section-copy">
        <h3>Datos comunes de los nuevos animales</h3>
        <p>Solo se aplicarán a las identificaciones que no existan aún en Pecualia.</p>
      </div>

      <div className="grid-form">
        <label className="farm-form-field">
          <span className="farm-field-label">Raza</span>
          <input value={form.breed} onChange={(event) => onChange('breed', event.target.value)} />
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
        <label className="farm-form-field">
          <span className="farm-field-label">Causa de alta</span>
          <select value={form.registrationCause} onChange={(event) => onChange('registrationCause', event.target.value)}>
            <option value="">Selecciona</option>
            {registrationCauseOptions.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
        </label>
        <label className="farm-form-field">
          <span className="farm-field-label">Código de origen</span>
          <input value={form.originCode} onChange={(event) => onChange('originCode', event.target.value)} />
        </label>
        <label className="farm-form-field">
          <span className="farm-field-label">Documento sanitario</span>
          <input value={form.healthDocumentNumber} onChange={(event) => onChange('healthDocumentNumber', event.target.value)} />
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

function MovementManualModal({ farm, token, farms, onClose, onCreated }) {
  const [form, setForm] = useState({
    direction: 'Exit',
    counterpartyType: 'External',
    counterpartyFarmId: '',
    counterpartyExternalCode: '',
    counterpartyExternalName: '',
    codRemo: '',
    serie: '',
    departureDate: todayInputValue(),
    arrivalDate: todayInputValue(),
    solicitationDate: todayInputValue(),
    meansOfTransport: '',
    transportName: '',
    vehicleRegistrationNumber: '',
    healthDocumentNumber: '',
    cause: '',
    identifications: ''
  });
  const [sharedAnimalData, setSharedAnimalData] = useState({
    birthYear: '',
    breed: '',
    sex: '',
    registrationCause: '',
    originCode: '',
    healthDocumentNumber: '',
    genotyping: '',
    dominantAllele: '',
    lowAllele: '',
    animalType: '',
    identificationDate: '',
    pigRegistrationNumber: '',
    tag: ''
  });
  const [availableAnimals, setAvailableAnimals] = useState([]);
  const [selectedAnimalIds, setSelectedAnimalIds] = useState([]);
  const [loadingAnimals, setLoadingAnimals] = useState(false);
  const [requestError, setRequestError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const filteredFarms = useMemo(
    () => farms.filter((entry) => entry.id !== farm.id && entry.livestockSpecies === farm.livestockSpecies),
    [farm.id, farm.livestockSpecies, farms]
  );

  const usesIdentificationTextarea = form.direction === 'Entry' && form.counterpartyType === 'External';
  const animalSourceFarmId = form.direction === 'Entry' && form.counterpartyType === 'Internal'
    ? Number(form.counterpartyFarmId || 0)
    : farm.id;

  useEffect(() => {
    let cancelled = false;

    async function loadAnimals() {
      if (usesIdentificationTextarea || !animalSourceFarmId) {
        setAvailableAnimals([]);
        setSelectedAnimalIds([]);
        return;
      }

      setLoadingAnimals(true);
      setRequestError('');

      try {
        const response = await apiRequest(`/api/farms/${animalSourceFarmId}/animals?status=Active`, { token });
        if (!cancelled) {
          setAvailableAnimals(response);
          setSelectedAnimalIds([]);
        }
      } catch (error) {
        if (!cancelled) {
          setRequestError(error.message);
          setAvailableAnimals([]);
          setSelectedAnimalIds([]);
        }
      } finally {
        if (!cancelled) {
          setLoadingAnimals(false);
        }
      }
    }

    loadAnimals();
    return () => {
      cancelled = true;
    };
  }, [animalSourceFarmId, token, usesIdentificationTextarea]);

  function updateField(field, value) {
    setForm((current) => ({ ...current, [field]: value }));
  }

  function updateSharedField(field, value) {
    setSharedAnimalData((current) => ({ ...current, [field]: value }));
  }

  function toggleAnimal(animalId) {
    setSelectedAnimalIds((current) => (
      current.includes(animalId)
        ? current.filter((entry) => entry !== animalId)
        : [...current, animalId]
    ));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setSubmitting(true);
    setRequestError('');

    try {
      const payload = {
        farmId: farm.id,
        direction: form.direction,
        counterpartyType: form.counterpartyType,
        counterpartyFarmId: form.counterpartyType === 'Internal' && form.counterpartyFarmId ? Number(form.counterpartyFarmId) : null,
        counterpartyExternalCode: form.counterpartyType === 'External' ? emptyToNull(form.counterpartyExternalCode) : null,
        counterpartyExternalName: form.counterpartyType === 'External' ? emptyToNull(form.counterpartyExternalName) : null,
        codRemo: form.codRemo.trim(),
        serie: emptyToNull(form.serie),
        departureDate: form.departureDate,
        arrivalDate: emptyToNull(form.arrivalDate),
        solicitationDate: emptyToNull(form.solicitationDate),
        meansOfTransport: emptyToNull(form.meansOfTransport),
        transportName: emptyToNull(form.transportName),
        vehicleRegistrationNumber: emptyToNull(form.vehicleRegistrationNumber),
        healthDocumentNumber: emptyToNull(form.healthDocumentNumber),
        cause: form.cause.trim(),
        animalIds: usesIdentificationTextarea ? null : selectedAnimalIds,
        identifications: usesIdentificationTextarea ? toTextareaLines(form.identifications) : null,
        sharedAnimalData: usesIdentificationTextarea ? buildSharedAnimalDataPayload(sharedAnimalData, farm.livestockSpecies) : null
      };

      const response = await apiRequest('/api/movements/manual', {
        method: 'POST',
        body: payload,
        token
      });

      onCreated(response);
    } catch (error) {
      setRequestError(error.message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <form className="modal-card modal-wide farm-modal-shell movement-modal-shell" onSubmit={handleSubmit}>
        <div className="farm-modal-header">
          <div className="farm-modal-title">
            <div className="modal-panel-icon">
              <ArrowLeftRight size={18} />
            </div>
            <div>
              <h2>Nuevo movimiento</h2>
              <p>La guía actualizará animales, balance y censo de forma automática.</p>
            </div>
          </div>
          <button className="farm-modal-close" type="button" onClick={onClose} aria-label="Cerrar modal">
            <X size={18} />
          </button>
        </div>

        <div className="farm-modal-body">
          {requestError && <div className="error-banner">{requestError}</div>}

          <div className="grid-form">
            <label className="farm-form-field">
                  <span className="farm-field-label">Dirección</span>
                  <select value={form.direction} onChange={(event) => setForm((current) => ({ ...current, direction: event.target.value, cause: '' }))}>
                    <option value="Exit">Salida</option>
                    <option value="Entry">Entrada</option>
                  </select>
            </label>

            <label className="farm-form-field">
              <span className="farm-field-label">Contraparte</span>
              <select value={form.counterpartyType} onChange={(event) => updateField('counterpartyType', event.target.value)}>
                <option value="External">Externa</option>
                <option value="Internal">Interna</option>
              </select>
            </label>

            {form.counterpartyType === 'Internal' ? (
              <label className="farm-form-field form-full">
                <span className="farm-field-label">Explotación contraparte</span>
                <select value={form.counterpartyFarmId} onChange={(event) => updateField('counterpartyFarmId', event.target.value)}>
                  <option value="">Selecciona una explotación</option>
                  {filteredFarms.map((entry) => (
                    <option key={entry.id} value={entry.id}>{entry.name} · {entry.regaCode}</option>
                  ))}
                </select>
              </label>
            ) : (
              <>
                <label className="farm-form-field">
                  <span className="farm-field-label">Nombre contraparte</span>
                  <input value={form.counterpartyExternalName} onChange={(event) => updateField('counterpartyExternalName', event.target.value)} />
                </label>
                <label className="farm-form-field">
                  <span className="farm-field-label">Código contraparte</span>
                  <input value={form.counterpartyExternalCode} onChange={(event) => updateField('counterpartyExternalCode', event.target.value)} />
                </label>
              </>
            )}

            <label className="farm-form-field">
              <span className="farm-field-label">Código REMO</span>
              <input value={form.codRemo} onChange={(event) => updateField('codRemo', event.target.value)} />
            </label>
            <label className="farm-form-field">
              <span className="farm-field-label">Serie</span>
              <input value={form.serie} onChange={(event) => updateField('serie', event.target.value)} />
            </label>
            <label className="farm-form-field">
              <span className="farm-field-label">Fecha salida</span>
              <input type="date" value={form.departureDate} onChange={(event) => updateField('departureDate', event.target.value)} />
            </label>
            <label className="farm-form-field">
              <span className="farm-field-label">Fecha llegada</span>
              <input type="date" value={form.arrivalDate} onChange={(event) => updateField('arrivalDate', event.target.value)} />
            </label>
            <label className="farm-form-field">
              <span className="farm-field-label">Fecha solicitud</span>
              <input type="date" value={form.solicitationDate} onChange={(event) => updateField('solicitationDate', event.target.value)} />
            </label>
            <label className="farm-form-field">
              <span className="farm-field-label">Causa</span>
              <select value={form.cause} onChange={(event) => updateField('cause', event.target.value)}>
                <option value="">Selecciona</option>
                {(form.direction === 'Entry' ? registrationCauseOptions : dischargeCauseOptions).map((option) => (
                  <option key={option.value} value={option.value}>{option.label}</option>
                ))}
              </select>
            </label>
            <label className="farm-form-field">
              <span className="farm-field-label">Medio de transporte</span>
              <input value={form.meansOfTransport} onChange={(event) => updateField('meansOfTransport', event.target.value)} />
            </label>
            <label className="farm-form-field">
              <span className="farm-field-label">Transportista</span>
              <input value={form.transportName} onChange={(event) => updateField('transportName', event.target.value)} />
            </label>
            <label className="farm-form-field">
              <span className="farm-field-label">Matrícula</span>
              <input value={form.vehicleRegistrationNumber} onChange={(event) => updateField('vehicleRegistrationNumber', event.target.value)} />
            </label>
            <label className="farm-form-field">
              <span className="farm-field-label">Documento sanitario</span>
              <input value={form.healthDocumentNumber} onChange={(event) => updateField('healthDocumentNumber', event.target.value)} />
            </label>
          </div>

          {usesIdentificationTextarea ? (
            <>
              <label className="farm-form-field">
                <span className="farm-field-label">Identificaciones</span>
                <textarea
                  rows={8}
                  value={form.identifications}
                  onChange={(event) => updateField('identifications', event.target.value)}
                  placeholder={farm.livestockSpecies === 'Porcine' ? 'GT12345678' : 'ES100008594650'}
                />
              </label>
              <SharedAnimalDataFields species={farm.livestockSpecies} form={sharedAnimalData} onChange={updateSharedField} />
            </>
          ) : (
            <section className="movement-animal-picker stack">
              <div className="movement-section-copy">
                <h3>Animales incluidos en la guía</h3>
                <p>
                  {form.direction === 'Entry' && form.counterpartyType === 'Internal'
                    ? 'Selecciona animales activos en la explotación origen.'
                    : 'Selecciona animales activos en la explotación actual.'}
                </p>
              </div>

              {loadingAnimals ? (
                <div className="empty-state">Cargando animales disponibles...</div>
              ) : availableAnimals.length === 0 ? (
                <div className="empty-state">No hay animales disponibles para esta operación.</div>
              ) : (
                <div className="movement-selection-grid">
                  {availableAnimals.map((animal) => (
                    <button
                      key={animal.id}
                      type="button"
                      onClick={() => toggleAnimal(animal.id)}
                      className={selectedAnimalIds.includes(animal.id) ? 'movement-selection-card movement-selection-card-active' : 'movement-selection-card'}
                    >
                      <strong>{animal.identification}</strong>
                      <span>{animal.breed ?? 'Sin raza'} · {animal.sex === 'Female' ? 'Hembra' : animal.sex === 'Male' ? 'Macho' : 'Sin sexo'}</span>
                    </button>
                  ))}
                </div>
              )}
            </section>
          )}
        </div>

        <div className="farm-modal-footer">
          <button className="secondary-button" type="button" onClick={onClose}>Cancelar</button>
          <button className="primary-button" type="submit" disabled={submitting}>
            {submitting ? 'Registrando...' : 'Registrar movimiento'}
          </button>
        </div>
      </form>
    </div>
  );
}

function MovementImportModal({ farm, token, farms, onClose, onCommitted }) {
  const [step, setStep] = useState(1);
  const [config, setConfig] = useState({
    operation: 'Baja',
    sinGuia: false,
    counterpartyExternalCode: '',
    counterpartyExternalName: '',
    codRemo: '',
    serie: '',
    departureDate: todayInputValue(),
    arrivalDate: todayInputValue(),
    solicitationDate: todayInputValue(),
    meansOfTransport: '',
    transportName: '',
    vehicleRegistrationNumber: '',
    healthDocumentNumber: '',
    cause: ''
  });
  const [sharedAnimalData, setSharedAnimalData] = useState({
    birthYear: '',
    breed: '',
    sex: '',
    registrationCause: '',
    originCode: '',
    healthDocumentNumber: '',
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
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState(null);

  const filteredPreviewRows = useMemo(() => {
    const rows = preview?.rows ?? [];
    return rows.filter((row) => {
      const matchesStatus = !filterStatus || row.status === filterStatus;
      const matchesSearch = !search || row.identification.toLowerCase().includes(search.toLowerCase());
      return matchesStatus && matchesSearch;
    });
  }, [filterStatus, preview, search]);

  const processableRowsCount = preview
    ? (config.operation === 'Alta' ? preview.summary.notFoundRows : preview.summary.validRows)
    : 0;
  const rejectedRowsCount = preview
    ? preview.summary.totalLines - processableRowsCount
    : 0;
  const canContinueStep1 = Boolean(
    config.operation &&
    config.cause.trim() &&
    config.departureDate &&
    (config.sinGuia || config.codRemo.trim())
  );

  function updateConfig(field, value) {
    setConfig((current) => ({ ...current, [field]: value }));
  }

  function updateSharedField(field, value) {
    setSharedAnimalData((current) => ({ ...current, [field]: value }));
  }

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
    setLoadingPreview(true);
    setRequestError('');

    try {
      const response = await apiRequest('/api/movements/imports/preview', {
        method: 'POST',
        token,
        body: {
          farmId: farm.id,
          operation: config.operation,
          counterpartyExternalCode: emptyToNull(config.counterpartyExternalCode),
          counterpartyExternalName: emptyToNull(config.counterpartyExternalName),
          codRemo: config.sinGuia ? null : emptyToNull(config.codRemo),
          serie: emptyToNull(config.serie),
          departureDate: config.departureDate,
          arrivalDate: emptyToNull(config.arrivalDate),
          solicitationDate: emptyToNull(config.solicitationDate),
          meansOfTransport: emptyToNull(config.meansOfTransport),
          transportName: emptyToNull(config.transportName),
          vehicleRegistrationNumber: emptyToNull(config.vehicleRegistrationNumber),
          healthDocumentNumber: emptyToNull(config.healthDocumentNumber),
          cause: config.cause.trim(),
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
    setSubmitting(true);
    setRequestError('');

    try {
      const response = await apiRequest('/api/movements/imports/commit', {
        method: 'POST',
        token,
        body: {
          farmId: farm.id,
          operation: config.operation,
          counterpartyExternalCode: emptyToNull(config.counterpartyExternalCode),
          counterpartyExternalName: emptyToNull(config.counterpartyExternalName),
          codRemo: config.sinGuia ? null : emptyToNull(config.codRemo),
          serie: emptyToNull(config.serie),
          departureDate: config.departureDate,
          arrivalDate: emptyToNull(config.arrivalDate),
          solicitationDate: emptyToNull(config.solicitationDate),
          meansOfTransport: emptyToNull(config.meansOfTransport),
          transportName: emptyToNull(config.transportName),
          vehicleRegistrationNumber: emptyToNull(config.vehicleRegistrationNumber),
          healthDocumentNumber: emptyToNull(config.healthDocumentNumber),
          cause: config.cause.trim(),
          rawText,
          sharedAnimalData: preview?.requiresSharedAnimalData ? buildSharedAnimalDataPayload(sharedAnimalData, farm.livestockSpecies) : null
        }
      });

      setResult(response);
      setStep(5);
      onCommitted(response);
    } catch (error) {
      setRequestError(error.message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <div className="modal-card modal-wide farm-modal-shell movement-modal-shell">
        <div className="farm-modal-header">
          <div className="farm-modal-title">
            <div className="modal-panel-icon">
              <Upload size={18} />
            </div>
            <div>
              <h2>Importar identificadores</h2>
              <p>Alta o baja masiva de animales sobre la explotación seleccionada.</p>
            </div>
          </div>
          <button className="farm-modal-close" type="button" onClick={onClose} aria-label="Cerrar modal">
            <X size={18} />
          </button>
        </div>

        <div className="movement-stepper">
          {['Configuración', 'Archivo', 'Validación', 'Confirmación'].map((label, index) => (
            <div key={label} className="movement-stepper-item">
              <div className={step >= index + 1 ? 'movement-stepper-badge movement-stepper-badge-active' : 'movement-stepper-badge'}>
                {step > index + 1 ? <CheckCircle2 size={15} /> : index + 1}
              </div>
              <span className={step === index + 1 ? 'movement-stepper-label movement-stepper-label-active' : 'movement-stepper-label'}>
                {label}
              </span>
            </div>
          ))}
        </div>

        <div className="farm-modal-body">
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
                </div>

                <label className="farm-form-field">
                  <span className="farm-field-label">Tipo de operación</span>
                  <select
                    value={config.operation}
                    onChange={(event) => setConfig((current) => ({ ...current, operation: event.target.value, cause: '' }))}
                  >
                    <option value="Alta">Alta</option>
                    <option value="Baja">Baja</option>
                  </select>
                </label>
                <label className="farm-form-field">
                  <span className="farm-field-label">{config.operation === 'Alta' ? 'Causa de alta' : 'Causa de baja'}</span>
                  <select value={config.cause} onChange={(event) => updateConfig('cause', event.target.value)}>
                    <option value="">Selecciona</option>
                    {(config.operation === 'Alta' ? registrationCauseOptions : dischargeCauseOptions).map((option) => (
                      <option key={option.value} value={option.value}>{option.label}</option>
                    ))}
                  </select>
                </label>

                <label className="farm-form-field">
                  <span className="farm-field-label">Fecha de operación</span>
                  <input type="date" value={config.departureDate} onChange={(event) => updateConfig('departureDate', event.target.value)} />
                </label>
                <label className="farm-form-field">
                  <span className="farm-field-label">Serie</span>
                  <input value={config.serie} onChange={(event) => updateConfig('serie', event.target.value)} disabled={config.sinGuia} />
                </label>

                <label className="farm-form-field">
                  <span className="farm-field-label">Código REMO</span>
                  <input value={config.codRemo} onChange={(event) => updateConfig('codRemo', event.target.value)} disabled={config.sinGuia} />
                </label>
                <label className="farm-form-field">
                  <span className="farm-field-label">Sin guía asociada</span>
                  <select
                    value={config.sinGuia ? 'yes' : 'no'}
                    onChange={(event) => updateConfig('sinGuia', event.target.value === 'yes')}
                  >
                    <option value="no">Vincular a guía</option>
                    <option value="yes">Sin guía asociada</option>
                  </select>
                </label>

                <label className="farm-form-field">
                  <span className="farm-field-label">{config.operation === 'Alta' ? 'Origen externo' : 'Destino externo'}</span>
                  <input value={config.counterpartyExternalName} onChange={(event) => updateConfig('counterpartyExternalName', event.target.value)} placeholder="Nombre informativo" />
                </label>
                <label className="farm-form-field">
                  <span className="farm-field-label">Código REGA / destino</span>
                  <input value={config.counterpartyExternalCode} onChange={(event) => updateConfig('counterpartyExternalCode', event.target.value)} placeholder="Solo metadata, no se vincula" />
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
                <label className="farm-form-field">
                  <span className="farm-field-label">Documento sanitario</span>
                  <input value={config.healthDocumentNumber} onChange={(event) => updateConfig('healthDocumentNumber', event.target.value)} />
                </label>
              </div>
              <div className="movement-impact-note">
                <AlertTriangle size={16} />
                <p>La aplicación no busca ni vincula la explotación contraparte. La importación solo modifica animales, censo y balance de {farm.name}.</p>
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

          {step === 4 && preview && (
            <div className="stack">
              <section className="movement-confirmation-card">
                <div className="movement-section-copy">
                  <h3>Resumen de importación</h3>
                  <p>
                    Se procesarán {processableRowsCount} identificaciones como {config.operation === 'Alta' ? 'alta masiva' : 'baja masiva'}.
                    {rejectedRowsCount > 0 && (
                      <> {rejectedRowsCount} quedarán excluidas.</>
                    )}
                  </p>
                </div>
              </section>

              {preview.requiresSharedAnimalData && (
                <SharedAnimalDataFields species={farm.livestockSpecies} form={sharedAnimalData} onChange={updateSharedField} />
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
                <p>{result.codRemo ? `Guía ${result.codRemo}` : 'Importación sin guía'} registrada correctamente.</p>
                <span>{result.processedRows} identificaciones procesadas · {result.rejectedRows} rechazadas</span>
              </div>
              <button className="primary-button farm-success-button" type="button" onClick={onClose}>Cerrar</button>
            </div>
          )}
        </div>

        {step < 5 && (
          <div className="farm-modal-footer">
            <button className="secondary-button" type="button" onClick={() => (step === 1 ? onClose() : setStep((current) => current - 1))}>
              {step === 1 ? 'Cancelar' : 'Volver'}
            </button>
            <div className="movement-footer-actions">
              {step === 1 && <button className="primary-button" type="button" onClick={() => setStep(2)} disabled={!canContinueStep1}>Continuar</button>}
              {step === 2 && <button className="primary-button" type="button" onClick={handlePreview} disabled={loadingPreview}>{loadingPreview ? 'Validando...' : 'Validar archivo'}</button>}
              {step === 3 && <button className="primary-button" type="button" onClick={() => setStep(4)}>Continuar</button>}
              {step === 4 && <button className="primary-button" type="button" onClick={handleCommit} disabled={submitting || processableRowsCount === 0}>{submitting ? 'Registrando...' : 'Confirmar importación'}</button>}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

export function FarmMovementsSection({ farm, token }) {
  const [movements, setMovements] = useState([]);
  const [farms, setFarms] = useState([]);
  const [selectedMovementId, setSelectedMovementId] = useState(null);
  const [selectedMovement, setSelectedMovement] = useState(null);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [error, setError] = useState('');
  const [manualOpen, setManualOpen] = useState(false);
  const [importOpen, setImportOpen] = useState(false);

  async function loadMovements(keepSelection = true) {
    setLoading(true);
    setError('');

    try {
      const [movementResponse, farmResponse] = await Promise.all([
        apiRequest(`/api/farms/${farm.id}/movements`, { token }),
        apiRequest('/api/farms', { token })
      ]);
      setMovements(movementResponse);
      setFarms(farmResponse);

      if (movementResponse.length > 0) {
        const nextSelectedId = keepSelection && movementResponse.some((entry) => entry.id === selectedMovementId)
          ? selectedMovementId
          : movementResponse[0].id;
        setSelectedMovementId(nextSelectedId);
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
            <button className="secondary-button movement-import-button" type="button" onClick={() => setImportOpen(true)}>
              <Upload size={16} />
              Importar identificadores
            </button>
            <button className="primary-button" type="button" onClick={() => setManualOpen(true)}>
              <Plus size={16} />
              Nuevo movimiento
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
          <div className="movements-layout">
            <div className="animal-table-card">
              <div className="table-scroll">
                <table className="animal-table">
                  <thead>
                    <tr>
                      {['Guía', 'Dirección', 'Contraparte', 'Animales', 'Salida', 'Llegada', 'Estado'].map((header) => (
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
                          <td><strong>{movement.codRemo}</strong></td>
                          <td>{directionLabelMap[movement.direction] ?? movement.direction}</td>
                          <td>{movement.counterpartyName}</td>
                          <td>
                            <div className="animal-identification-cell">
                              <ArrowLeftRight size={13} />
                              <strong>{movement.numberOfAnimals}</strong>
                            </div>
                          </td>
                          <td>{formatDate(movement.departureDate)}</td>
                          <td>{formatDate(movement.arrivalDate)}</td>
                          <td><span className="animal-chip" style={{ background: tone.bg, color: tone.color }}>{tone.label}</span></td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
              <div className="animal-table-footer">{filteredMovements.length} movimientos</div>
            </div>

            <aside className="animal-detail-panel movement-detail-panel">
              {detailLoading ? (
                <div className="empty-state">Cargando detalle del movimiento...</div>
              ) : !selectedMovement ? (
                <div className="empty-state">Selecciona un movimiento para ver su detalle.</div>
              ) : (
                <>
                  <div className="animal-detail-hero movement-detail-hero">
                    <div className="animal-detail-hero-top">
                      <div>
                        <span>Guía REMO</span>
                        <strong>{selectedMovement.codRemo}</strong>
                        <p>{selectedMovement.serie ?? 'Sin serie'}</p>
                      </div>
                      <span className="animal-chip" style={{
                        background: (movementStatusToneMap[selectedMovement.status] ?? movementStatusToneMap.Pending).bg,
                        color: (movementStatusToneMap[selectedMovement.status] ?? movementStatusToneMap.Pending).color
                      }}>
                        {(movementStatusToneMap[selectedMovement.status] ?? movementStatusToneMap.Pending).label}
                      </span>
                    </div>
                  </div>

                  <div className="movement-detail-body">
                    <div className="movement-detail-group">
                      <span>Origen</span>
                      <strong>{selectedMovement.originName ?? 'Explotación externa'}</strong>
                      <small>{selectedMovement.originCode ?? 'Sin código'}</small>
                    </div>
                    <div className="movement-detail-group">
                      <span>Destino</span>
                      <strong>{selectedMovement.destinationName ?? 'Explotación externa'}</strong>
                      <small>{selectedMovement.destinationCode ?? 'Sin código'}</small>
                    </div>
                    <div className="movement-detail-group">
                      <span>Fechas</span>
                      <strong>Salida {formatDate(selectedMovement.departureDate)}</strong>
                      <small>Llegada {formatDate(selectedMovement.arrivalDate)}</small>
                    </div>
                    <div className="movement-detail-group">
                      <span>Transporte</span>
                      <strong>{selectedMovement.transportName ?? 'No informado'}</strong>
                      <small>{selectedMovement.vehicleRegistrationNumber ?? 'Sin matrícula'}</small>
                    </div>
                    <div className="movement-detail-group">
                      <span>Animales</span>
                      <strong>{selectedMovement.numberOfAnimals}</strong>
                    </div>

                    <div className="movement-detail-list">
                      {selectedMovement.animals.map((animal) => (
                        <div key={animal.animalId} className="movement-detail-animal">
                          <strong>{animal.identification}</strong>
                          <span>{animal.breed ?? 'Sin raza'} · {animal.sex === 'Female' ? 'Hembra' : animal.sex === 'Male' ? 'Macho' : 'Sin sexo'}</span>
                        </div>
                      ))}
                    </div>
                  </div>
                </>
              )}
            </aside>
          </div>
        )}

        <div className="movement-impact-note">
          <AlertTriangle size={16} />
          <p>Cada guía actualiza automáticamente el estado de los animales afectados y registra entradas operativas en balance y censo.</p>
        </div>
      </section>

      {manualOpen && (
        <MovementManualModal
          farm={farm}
          token={token}
          farms={farms}
          onClose={() => setManualOpen(false)}
          onCreated={(movement) => {
            setManualOpen(false);
            setSelectedMovement(movement);
            setSelectedMovementId(movement.id);
            loadMovements(true);
          }}
        />
      )}

      {importOpen && (
        <MovementImportModal
          farm={farm}
          token={token}
          farms={farms}
          onClose={() => setImportOpen(false)}
          onCommitted={() => {
            loadMovements(false);
          }}
        />
      )}
    </>
  );
}
