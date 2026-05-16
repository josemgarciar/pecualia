import { useEffect, useMemo, useState } from 'react';
import { ArrowRightLeft, ChevronDown, Plus, Search, Tag, Upload } from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { useAuth } from '../../shared/auth/AuthContext';
import { ModalBody, ModalDialog, ModalFieldLabel, ModalFooter, ModalHeader } from '../../shared/components/modal/Modal';
import {
  buildMerCodeExample,
  buildRandomMerCode,
  getAnimalIdentificationFormatMessage,
  isValidAnimalIdentification,
  isValidMerCode,
  isValidRegaCode,
  normalizeMerCode,
  normalizeAnimalIdentification,
  normalizeRegaCode
} from '../../shared/validation/identifiers';

const speciesToneMap = {
  Ovine: { bg: '#DDEBDF', color: '#2F6B4F', label: 'Ovino' },
  Caprine: { bg: '#DBEAFE', color: '#1D4ED8', label: 'Caprino' },
  Porcine: { bg: '#FCE7F3', color: '#9D174D', label: 'Porcino' }
};

const sexLabelMap = {
  Female: 'Hembra',
  Male: 'Macho'
};

const registrationCauseLabelMap = {
  Entrada: 'Entrada (E)',
  Autorreposicion: 'Autorreposición (A)'
};

const dischargeCauseLabelMap = {
  Salida: 'Salida (S)',
  Muerte: 'Muerte (M)'
};

const initialForm = {
  farmId: '',
  identification: '',
  birthYear: '',
  breed: '',
  sex: '',
  registrationDate: '',
  registrationCause: '',
  originCode: '',
  genotyping: '',
  dominantAllele: '',
  lowAllele: '',
  animalType: '',
  identificationDate: '',
  pigRegistrationNumber: '',
  tag: ''
};

function formatSpecies(value) {
  return speciesToneMap[value]?.label ?? value ?? 'Sin especie';
}

function formatSex(value) {
  return sexLabelMap[value] ?? value ?? 'No informado';
}

function formatCause(value) {
  return registrationCauseLabelMap[value] ?? dischargeCauseLabelMap[value] ?? value ?? 'No informada';
}

function sexSymbol(value) {
  return value === 'Female' ? '♀' : value === 'Male' ? '♂' : '';
}

function formatDate(value) {
  if (!value) {
    return '—';
  }

  return new Intl.DateTimeFormat('es-ES').format(new Date(`${value}T00:00:00`));
}

function emptyToNull(value) {
  return value.trim() ? value.trim() : null;
}

function createDischargeFormState(species) {
  return {
    dischargeDate: new Date().toISOString().slice(0, 10),
    dischargeCause: 'Salida',
    destinationCode: species === 'Porcine' || species === 'Caprine' ? 'MER' : '',
    merCode: ''
  };
}

function AnimalFormModal({ farms, loading, error, onClose, onSubmit }) {
  const [form, setForm] = useState(initialForm);
  const [formError, setFormError] = useState('');
  const selectedFarm = farms.find((farm) => String(farm.id) === form.farmId);
  const isPorcine = selectedFarm?.livestockSpecies === 'Porcine';
  const isOvineCaprine = selectedFarm?.livestockSpecies === 'Ovine' || selectedFarm?.livestockSpecies === 'Caprine';

  function updateField(field, value) {
    setForm((current) => ({ ...current, [field]: value }));
    setFormError('');
  }

  async function handleSubmit(event) {
    event.preventDefault();

    if (!form.farmId || !form.identification.trim()) {
      setFormError('Selecciona una explotación e indica la identificación.');
      return;
    }

    if (!selectedFarm || !isValidAnimalIdentification(selectedFarm.livestockSpecies, form.identification)) {
      setFormError(selectedFarm ? getAnimalIdentificationFormatMessage(selectedFarm.livestockSpecies) : 'Selecciona una explotación válida.');
      return;
    }

    if (form.originCode.trim() && !isValidRegaCode(form.originCode)) {
      setFormError('El código REGA de origen no es válido.');
      return;
    }

    if (isPorcine && !form.animalType.trim()) {
      setFormError('El tipo de animal porcino es obligatorio.');
      return;
    }

    const payload = {
      farmId: Number(form.farmId),
      identification: normalizeAnimalIdentification(form.identification),
      birthYear: form.birthYear ? Number(form.birthYear) : null,
      breed: emptyToNull(form.breed),
      sex: emptyToNull(form.sex),
      registrationDate: form.registrationDate || null,
      registrationCause: form.registrationCause || null,
      originCode: form.originCode.trim() ? normalizeRegaCode(form.originCode) : null,
      ovinoCaprino: isOvineCaprine
        ? {
            speciesType: selectedFarm.livestockSpecies,
            genotyping: emptyToNull(form.genotyping),
            dominantAllele: emptyToNull(form.dominantAllele),
            lowAllele: emptyToNull(form.lowAllele)
          }
        : null,
      porcino: isPorcine
        ? {
            animalType: form.animalType,
            identificationDate: form.identificationDate || null,
            pigRegistrationNumber: emptyToNull(form.pigRegistrationNumber),
            tag: emptyToNull(form.tag)
          }
        : null
    };

    await onSubmit(payload);
  }

  return (
    <ModalDialog cardAs="form" size="wide" shellClassName="animal-modal" onSubmit={handleSubmit}>
      <ModalHeader
        icon={<Tag size={18} />}
        title="Registrar animal"
        subtitle="Alta individual dentro de una explotación activa"
        onClose={onClose}
      />
      <ModalBody>
          {(formError || error) && (
            <div className="error-banner">{formError || error}</div>
          )}

          <div className="grid-form">
            <label className="farm-form-field">
              <ModalFieldLabel required>Explotación</ModalFieldLabel>
              <div className="select-wrapper">
                <select value={form.farmId} onChange={(event) => updateField('farmId', event.target.value)} required>
                  <option value="">Selecciona explotación</option>
                  {farms.map((farm) => (
                    <option key={farm.id} value={farm.id}>
                      {farm.name} · {formatSpecies(farm.livestockSpecies)}
                    </option>
                  ))}
                </select>
                <ChevronDown size={16} />
              </div>
            </label>
            <label className="farm-form-field">
              <ModalFieldLabel required>Identificación / crotal</ModalFieldLabel>
              <input
                value={form.identification}
                onChange={(event) => updateField('identification', event.target.value)}
                placeholder={selectedFarm?.livestockSpecies === 'Porcine' ? 'GT1800001004' : 'ES060000583112'}
                required
              />
            </label>
            <label className="farm-form-field">
              <ModalFieldLabel>Raza</ModalFieldLabel>
              <input value={form.breed} onChange={(event) => updateField('breed', event.target.value)} placeholder="Merina" />
            </label>
            <label className="farm-form-field">
              <ModalFieldLabel>Sexo</ModalFieldLabel>
              <div className="select-wrapper">
                <select value={form.sex} onChange={(event) => updateField('sex', event.target.value)}>
                  <option value="">No informado</option>
                  <option value="Female">Hembra</option>
                  <option value="Male">Macho</option>
                </select>
                <ChevronDown size={16} />
              </div>
            </label>
            <label className="farm-form-field">
              <ModalFieldLabel>Año nacimiento</ModalFieldLabel>
              <input type="number" min="1900" max="2100" value={form.birthYear} onChange={(event) => updateField('birthYear', event.target.value)} placeholder="2024" />
            </label>
            <label className="farm-form-field">
              <ModalFieldLabel>Fecha alta</ModalFieldLabel>
              <input type="date" value={form.registrationDate} onChange={(event) => updateField('registrationDate', event.target.value)} />
            </label>
            <label className="farm-form-field">
              <ModalFieldLabel>Causa alta</ModalFieldLabel>
              <div className="select-wrapper">
                <select value={form.registrationCause} onChange={(event) => updateField('registrationCause', event.target.value)}>
                  <option value="">No informada</option>
                  <option value="Entrada">Entrada (E)</option>
                  <option value="Autorreposicion">Autorreposición (A)</option>
                </select>
                <ChevronDown size={16} />
              </div>
            </label>
            <label className="farm-form-field">
              <ModalFieldLabel>Código origen</ModalFieldLabel>
              <input value={form.originCode} onChange={(event) => updateField('originCode', event.target.value)} placeholder="ES060000581234" />
            </label>
          </div>

          {isOvineCaprine && (
            <div className="animal-specific-block">
              <h3>Datos ovino/caprino</h3>
              <div className="grid-form">
                <label className="farm-form-field">
                  <ModalFieldLabel>Genotipado</ModalFieldLabel>
                  <input value={form.genotyping} onChange={(event) => updateField('genotyping', event.target.value)} placeholder="ARQ/ARR" />
                </label>
                <label className="farm-form-field">
                  <ModalFieldLabel>Alelo dominante</ModalFieldLabel>
                  <input value={form.dominantAllele} onChange={(event) => updateField('dominantAllele', event.target.value)} placeholder="ARR" />
                </label>
                <label className="farm-form-field">
                  <ModalFieldLabel>Alelo bajo</ModalFieldLabel>
                  <input value={form.lowAllele} onChange={(event) => updateField('lowAllele', event.target.value)} placeholder="ARQ" />
                </label>
              </div>
            </div>
          )}

          {isPorcine && (
            <div className="animal-specific-block">
              <h3>Datos porcino</h3>
              <div className="grid-form">
                <label className="farm-form-field">
                  <ModalFieldLabel required>Tipo de animal</ModalFieldLabel>
                  <input value={form.animalType} onChange={(event) => updateField('animalType', event.target.value)} placeholder="Cebo" required />
                </label>
                <label className="farm-form-field">
                  <ModalFieldLabel>Fecha identificación</ModalFieldLabel>
                  <input type="date" value={form.identificationDate} onChange={(event) => updateField('identificationDate', event.target.value)} />
                </label>
                <label className="farm-form-field">
                  <ModalFieldLabel>Nº registro porcino</ModalFieldLabel>
                  <input value={form.pigRegistrationNumber} onChange={(event) => updateField('pigRegistrationNumber', event.target.value)} placeholder="RPO-2026-0001" />
                </label>
                <label className="farm-form-field">
                  <ModalFieldLabel>Marca / crotal</ModalFieldLabel>
                  <input value={form.tag} onChange={(event) => updateField('tag', event.target.value)} placeholder="GT215284" />
                </label>
              </div>
            </div>
          )}
      </ModalBody>

      <ModalFooter align="end">
          <button className="secondary-button" type="button" onClick={onClose}>Cancelar</button>
          <button className="primary-button" type="submit" disabled={loading}>
            {loading ? 'Guardando...' : 'Guardar animal'}
          </button>
      </ModalFooter>
    </ModalDialog>
  );
}

function AnimalDetailPanel({ animal, loading, onClose, onDischarged }) {
  const [tab, setTab] = useState('data');
  const [dischargeError, setDischargeError] = useState('');
  const [discharging, setDischarging] = useState(false);
  const [dischargeModalOpen, setDischargeModalOpen] = useState(false);
  const [dischargeForm, setDischargeForm] = useState(() => createDischargeFormState(animal?.livestockSpecies));

  if (!animal) {
    return null;
  }

  const speciesTone = speciesToneMap[animal.livestockSpecies] ?? speciesToneMap.Ovine;
  const merCodeExample = buildMerCodeExample();
  const isMerOnlySpecies = animal.livestockSpecies === 'Porcine' || animal.livestockSpecies === 'Caprine';

  function openDischargeModal() {
    setDischargeError('');
    setDischargeForm(createDischargeFormState(animal.livestockSpecies));
    setDischargeModalOpen(true);
  }

  async function handleDischargeSubmit(event) {
    event.preventDefault();

    const normalizedCause = dischargeForm.dischargeCause.trim();
    if (!dischargeCauseLabelMap[normalizedCause]) {
      setDischargeError('La causa de baja debe ser Salida (S) o Muerte (M).');
      return;
    }

    let destinationCode = null;
    if (normalizedCause === 'Muerte') {
      if (isMerOnlySpecies) {
        if (!dischargeForm.merCode.trim()) {
          setDischargeError('Debes indicar el número de MER.');
          return;
        }

        if (!isValidMerCode(dischargeForm.merCode)) {
          setDischargeError(`El número MER debe tener formato ${merCodeExample}.`);
          return;
        }

        destinationCode = normalizeMerCode(dischargeForm.merCode);
      } else {
        if (!dischargeForm.destinationCode) {
          setDischargeError('El destino de una baja por muerte debe ser SANDACH o MER.');
          return;
        }

        const normalizedDestination = dischargeForm.destinationCode.trim().toUpperCase();
        if (!['SANDACH', 'MER'].includes(normalizedDestination)) {
          setDischargeError('El destino de una baja por muerte debe ser SANDACH o MER.');
          return;
        }

        if (normalizedDestination === 'MER') {
          if (!dischargeForm.merCode.trim()) {
            setDischargeError('Debes indicar el número de MER.');
            return;
          }

          if (!isValidMerCode(dischargeForm.merCode)) {
            setDischargeError(`El número MER debe tener formato ${merCodeExample}.`);
            return;
          }

          destinationCode = normalizeMerCode(dischargeForm.merCode);
        } else {
          destinationCode = normalizedDestination;
        }
      }
    }

    setDischarging(true);
    setDischargeError('');
    try {
      await onDischarged(animal.id, {
        dischargeDate: dischargeForm.dischargeDate,
        dischargeCause: normalizedCause,
        destinationCode
      });
      setDischargeModalOpen(false);
    } catch (requestError) {
      setDischargeError(requestError.message);
    } finally {
      setDischarging(false);
    }
  }

  return (
    <>
      <aside className="animal-detail-panel">
        <div className="animal-detail-hero">
          <div className="animal-detail-hero-top">
            <div>
              <span>IDENTIFICACIÓN</span>
              <strong>{animal.identification}</strong>
            </div>
            <button type="button" onClick={onClose} aria-label="Cerrar detalle">×</button>
          </div>
          <div className="animal-detail-badges">
            <span>{formatSpecies(animal.livestockSpecies)}</span>
            {animal.breed && <span>{animal.breed}</span>}
          </div>
        </div>

        <div className="animal-detail-tabs">
          <button type="button" className={tab === 'data' ? 'animal-detail-tab-active' : ''} onClick={() => setTab('data')}>Datos</button>
          <button type="button" className={tab === 'history' ? 'animal-detail-tab-active' : ''} onClick={() => setTab('history')}>Historial</button>
        </div>

        <div className="animal-detail-body">
          {loading && <div className="muted-text">Cargando detalle...</div>}
          {tab === 'data' && !loading && (
            <>
              <AnimalDetailField label="Sexo" value={`${sexSymbol(animal.sex)} ${formatSex(animal.sex)}`.trim()} />
              <AnimalDetailField label="Año de identificación" value={animal.birthYear ?? 'No informado'} />
              <AnimalDetailField label="Explotación" value={animal.farmName} />
              <AnimalDetailField label="Fecha de alta" value={formatDate(animal.registrationDate)} />
              <AnimalDetailField label="Causa de alta" value={formatCause(animal.registrationCause)} />
              <AnimalDetailField label="Serie guía entrada" value={animal.entryGuideSerie ?? 'No informada'} />
              <AnimalDetailField label="Serie guía salida" value={animal.exitGuideSerie ?? 'No informada'} />

              {animal.ovinoCaprino && (
                <div className="animal-specific-detail">
                  <h3>Datos ovino/caprino</h3>
                  <AnimalDetailField label="Genotipado" value={animal.ovinoCaprino.genotyping ?? 'No informado'} />
                  <AnimalDetailField label="Alelo dominante" value={animal.ovinoCaprino.dominantAllele ?? 'No informado'} />
                  <AnimalDetailField label="Alelo bajo" value={animal.ovinoCaprino.lowAllele ?? 'No informado'} />
                </div>
              )}

              {animal.porcino && (
                <div className="animal-specific-detail">
                  <h3>Datos porcino</h3>
                  <AnimalDetailField label="Tipo de animal" value={animal.porcino.animalType} />
                  <AnimalDetailField label="Nº registro porcino" value={animal.porcino.pigRegistrationNumber ?? 'No informado'} />
                  <AnimalDetailField label="Marca/crotal" value={animal.porcino.tag ?? 'No informado'} />
                </div>
              )}

              <div className="animal-detail-actions">
                <button className="primary-button" type="button">Ver movimientos</button>
                <button className="danger-button" type="button" onClick={openDischargeModal} disabled={animal.status === 'Discharged' || discharging}>
                  {discharging ? 'Registrando...' : 'Registrar baja'}
                </button>
              </div>
            </>
          )}

          {tab === 'history' && (
            <div className="empty-state">El historial detallado se completará al conectar movimientos e incidencias por animal.</div>
          )}
        </div>
      </aside>

      {dischargeModalOpen && (
        <ModalDialog cardAs="form" size="wide" onSubmit={handleDischargeSubmit}>
          <ModalHeader
            icon={<ArrowRightLeft size={18} />}
            title="Registrar baja"
            subtitle={animal.identification}
            onClose={() => {
              setDischargeModalOpen(false);
              setDischargeError('');
            }}
          />
          <ModalBody className="operation-modal-body">
            {dischargeError && <div className="error-banner">{dischargeError}</div>}
            <label>
              <ModalFieldLabel required>Fecha de baja</ModalFieldLabel>
              <input
                type="date"
                value={dischargeForm.dischargeDate}
                onChange={(event) => setDischargeForm({ ...dischargeForm, dischargeDate: event.target.value })}
              />
            </label>
            <label>
              <ModalFieldLabel required>Causa</ModalFieldLabel>
              <select
                value={dischargeForm.dischargeCause}
                onChange={(event) => setDischargeForm({
                  ...dischargeForm,
                  dischargeCause: event.target.value,
                  destinationCode: event.target.value === 'Muerte'
                    ? isMerOnlySpecies ? 'MER' : dischargeForm.destinationCode
                    : '',
                  merCode: event.target.value === 'Muerte' ? dischargeForm.merCode : ''
                })}>
                <option value="Salida">Salida</option>
                <option value="Muerte">Muerte</option>
              </select>
            </label>
            {dischargeForm.dischargeCause === 'Muerte' && (
              <label>
                <ModalFieldLabel required>Destino</ModalFieldLabel>
                <select
                  value={dischargeForm.destinationCode}
                  disabled={isMerOnlySpecies}
                  onChange={(event) => setDischargeForm({
                    ...dischargeForm,
                    destinationCode: event.target.value,
                    merCode: event.target.value === 'MER' ? dischargeForm.merCode : ''
                  })}>
                  {!isMerOnlySpecies && <option value="">Seleccionar...</option>}
                  <option value="SANDACH">SANDACH</option>
                  <option value="MER">MER</option>
                </select>
              </label>
            )}
            {dischargeForm.dischargeCause === 'Muerte' && (isMerOnlySpecies || dischargeForm.destinationCode === 'MER') && (
              <label>
                <ModalFieldLabel required>Nº MER</ModalFieldLabel>
                <div className="animal-discharge-mer-row">
                  <input
                    value={dischargeForm.merCode}
                    onChange={(event) => setDischargeForm({ ...dischargeForm, merCode: event.target.value })}
                    placeholder={merCodeExample}
                  />
                  <button
                    className="secondary-button"
                    type="button"
                    onClick={() => setDischargeForm({ ...dischargeForm, merCode: buildRandomMerCode() })}>
                    Generar
                  </button>
                </div>
                <small>Formato obligatorio: {merCodeExample}</small>
              </label>
            )}
          </ModalBody>
          <ModalFooter align="end">
            <button className="secondary-button" type="button" onClick={() => {
              setDischargeModalOpen(false);
              setDischargeError('');
            }}>Cancelar</button>
            <button className="primary-button" type="submit" disabled={discharging}>
              {discharging ? 'Registrando...' : 'Guardar baja'}
            </button>
          </ModalFooter>
        </ModalDialog>
      )}
    </>
  );
}

function AnimalDetailField({ label, value }) {
  return (
    <div className="animal-detail-field">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function AnimalsTable({ animals, selectedAnimalId, onSelect }) {
  if (animals.length === 0) {
    return (
      <div className="empty-state">
        <Tag size={28} />
        <div>No hay animales que coincidan con los filtros.</div>
      </div>
    );
  }

  return (
    <div className="animal-table-card">
      <div className="table-scroll">
        <table className="animal-table">
          <thead>
            <tr>
              {['Identificación', 'Especie', 'Raza', 'Sexo', 'Año Nac.', 'Explotación', 'Fecha alta', 'Causa'].map((header) => (
                <th key={header}>{header}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {animals.map((animal) => {
              const speciesTone = speciesToneMap[animal.livestockSpecies] ?? speciesToneMap.Ovine;

              return (
                <tr key={animal.id} className={selectedAnimalId === animal.id ? 'animal-row-selected' : ''} onClick={() => onSelect(animal)}>
                  <td>
                    <div className="animal-identification-cell">
                      <Tag size={14} />
                      <strong>{animal.identification}</strong>
                    </div>
                  </td>
                  <td><span className="animal-chip" style={{ background: speciesTone.bg, color: speciesTone.color }}>{speciesTone.label}</span></td>
                  <td>{animal.breed ?? '—'}</td>
                  <td className={animal.sex === 'Female' ? 'animal-sex-female' : animal.sex === 'Male' ? 'animal-sex-male' : undefined}>
                    {sexSymbol(animal.sex)} {formatSex(animal.sex)}
                  </td>
                  <td>{animal.birthYear ?? '—'}</td>
                  <td>{animal.farmName}</td>
                  <td>{formatDate(animal.registrationDate)}</td>
                  <td>{formatCause(animal.registrationCause)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      <div className="animal-table-footer">{animals.length} animales</div>
    </div>
  );
}

export function AnimalsPage() {
  const { token } = useAuth();
  const [animals, setAnimals] = useState([]);
  const [farms, setFarms] = useState([]);
  const [selectedAnimal, setSelectedAnimal] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [loading, setLoading] = useState(true);
  const [modalOpen, setModalOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [formError, setFormError] = useState('');
  const [filters, setFilters] = useState({ search: '', species: '', sex: '' });

  async function loadAnimals() {
    setLoading(true);
    setError('');
    try {
      const params = new URLSearchParams();
      Object.entries(filters).forEach(([key, value]) => {
        if (value) {
          params.set(key, value);
        }
      });
      const response = await apiRequest(`/api/animals${params.toString() ? `?${params}` : ''}`, { token });
      setAnimals(response);
      if (selectedAnimal && !response.some((animal) => animal.id === selectedAnimal.id)) {
        setSelectedAnimal(null);
      }
    } catch (requestError) {
      setError(requestError.message);
      setAnimals([]);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    let cancelled = false;

    async function loadFarms() {
      try {
        const response = await apiRequest('/api/farms/', { token });
        if (!cancelled) {
          setFarms(response);
        }
      } catch {
        if (!cancelled) {
          setFarms([]);
        }
      }
    }

    loadFarms();
    return () => {
      cancelled = true;
    };
  }, [token]);

  useEffect(() => {
    loadAnimals();
  }, [filters.search, filters.species, filters.sex, token]);

  const counters = useMemo(() => ({
    active: animals.filter((animal) => animal.status === 'Active').length,
    total: animals.length
  }), [animals]);

  async function selectAnimal(animal) {
    setSelectedAnimal(animal);
    setDetailLoading(true);
    try {
      const response = await apiRequest(`/api/animals/${animal.id}`, { token });
      setSelectedAnimal(response);
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setDetailLoading(false);
    }
  }

  async function createAnimal(payload) {
    setSubmitting(true);
    setFormError('');
    try {
      const created = await apiRequest('/api/animals/', { method: 'POST', body: payload, token });
      setModalOpen(false);
      setSelectedAnimal(created);
      await loadAnimals();
    } catch (requestError) {
      setFormError(requestError.message);
    } finally {
      setSubmitting(false);
    }
  }

  async function dischargeAnimal(animalId, payload) {
    const updated = await apiRequest(`/api/animals/${animalId}/discharge`, { method: 'POST', body: payload, token });
    setSelectedAnimal(updated);
    await loadAnimals();
  }

  function updateFilter(field, value) {
    setFilters((current) => ({ ...current, [field]: value }));
  }

  return (
    <div className="page-stack">
      <div className="animal-header">
        <div>
          <h1>Animales</h1>
          <p>{counters.active} activos · {counters.total} registros totales</p>
        </div>
        <div className="animal-header-actions">
          <button className="secondary-button animal-import-button" type="button" title="Próximamente">
            <Upload size={16} />
            Importar identificadores
          </button>
          <button className="primary-button" type="button" onClick={() => {
            setFormError('');
            setModalOpen(true);
          }}>
            <Plus size={16} />
            Registrar animal
          </button>
        </div>
      </div>

      {error && <div className="error-banner">{error}</div>}

      <div className="animal-layout">
        <section className="animal-main">
          <div className="animal-filters">
            <div className="animal-search">
              <Search size={16} />
              <input value={filters.search} onChange={(event) => updateFilter('search', event.target.value)} placeholder="Buscar por crotal o raza..." />
            </div>
            <select value={filters.species} onChange={(event) => updateFilter('species', event.target.value)}>
              <option value="">Especie</option>
              <option value="Ovine">Ovino</option>
              <option value="Caprine">Caprino</option>
              <option value="Porcine">Porcino</option>
            </select>
            <select value={filters.sex} onChange={(event) => updateFilter('sex', event.target.value)}>
              <option value="">Sexo</option>
              <option value="Female">Hembra</option>
              <option value="Male">Macho</option>
            </select>
          </div>

          {loading ? (
            <div className="panel-card empty-state">Cargando animales...</div>
          ) : (
            <AnimalsTable animals={animals} selectedAnimalId={selectedAnimal?.id} onSelect={selectAnimal} />
          )}
        </section>

        <AnimalDetailPanel animal={selectedAnimal} loading={detailLoading} onClose={() => setSelectedAnimal(null)} onDischarged={dischargeAnimal} />
      </div>

      {modalOpen && (
        <AnimalFormModal
          farms={farms}
          loading={submitting}
          error={formError}
          onClose={() => {
            setModalOpen(false);
            setFormError('');
          }}
          onSubmit={createAnimal}
        />
      )}
    </div>
  );
}
