import { useEffect, useMemo, useState } from 'react';
import { AlertCircle, ChevronDown, Plus, Search, Tag, Upload } from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { useAuth } from '../../shared/auth/AuthContext';
import { ModalBody, ModalDialog, ModalFooter, ModalHeader } from '../../shared/components/modal/Modal';
import {
  getAnimalIdentificationFormatMessage,
  isValidAnimalIdentification,
  isValidRegaCode,
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
  healthDocumentNumber: '',
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
      healthDocumentNumber: emptyToNull(form.healthDocumentNumber),
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
            <div className="error-banner">
              <AlertCircle size={14} />
              {formError || error}
            </div>
          )}

          <div className="grid-form">
            <label>
              Explotación
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
            <label>
              Identificación / crotal
              <input
                value={form.identification}
                onChange={(event) => updateField('identification', event.target.value)}
                placeholder={selectedFarm?.livestockSpecies === 'Porcine' ? 'GT1800001004' : 'ES0600005831'}
                required
              />
            </label>
            <label>
              Raza
              <input value={form.breed} onChange={(event) => updateField('breed', event.target.value)} placeholder="Merina" />
            </label>
            <label>
              Sexo
              <div className="select-wrapper">
                <select value={form.sex} onChange={(event) => updateField('sex', event.target.value)}>
                  <option value="">No informado</option>
                  <option value="Female">Hembra</option>
                  <option value="Male">Macho</option>
                </select>
                <ChevronDown size={16} />
              </div>
            </label>
            <label>
              Año nacimiento
              <input type="number" min="1900" max="2100" value={form.birthYear} onChange={(event) => updateField('birthYear', event.target.value)} placeholder="2024" />
            </label>
            <label>
              Fecha alta
              <input type="date" value={form.registrationDate} onChange={(event) => updateField('registrationDate', event.target.value)} />
            </label>
            <label>
              Causa alta
              <div className="select-wrapper">
                <select value={form.registrationCause} onChange={(event) => updateField('registrationCause', event.target.value)}>
                  <option value="">No informada</option>
                  <option value="Entrada">Entrada (E)</option>
                  <option value="Autorreposicion">Autorreposición (A)</option>
                </select>
                <ChevronDown size={16} />
              </div>
            </label>
            <label>
              Código origen
              <input value={form.originCode} onChange={(event) => updateField('originCode', event.target.value)} placeholder="ES06000058" />
            </label>
            <label className="form-full">
              Documento sanitario
              <input value={form.healthDocumentNumber} onChange={(event) => updateField('healthDocumentNumber', event.target.value)} placeholder="Número de documento sanitario" />
            </label>
          </div>

          {isOvineCaprine && (
            <div className="animal-specific-block">
              <h3>Datos ovino/caprino</h3>
              <div className="grid-form">
                <label>
                  Genotipado
                  <input value={form.genotyping} onChange={(event) => updateField('genotyping', event.target.value)} placeholder="ARQ/ARR" />
                </label>
                <label>
                  Alelo dominante
                  <input value={form.dominantAllele} onChange={(event) => updateField('dominantAllele', event.target.value)} placeholder="ARR" />
                </label>
                <label>
                  Alelo bajo
                  <input value={form.lowAllele} onChange={(event) => updateField('lowAllele', event.target.value)} placeholder="ARQ" />
                </label>
              </div>
            </div>
          )}

          {isPorcine && (
            <div className="animal-specific-block">
              <h3>Datos porcino</h3>
              <div className="grid-form">
                <label>
                  Tipo de animal
                  <input value={form.animalType} onChange={(event) => updateField('animalType', event.target.value)} placeholder="Cebo" required />
                </label>
                <label>
                  Fecha identificación
                  <input type="date" value={form.identificationDate} onChange={(event) => updateField('identificationDate', event.target.value)} />
                </label>
                <label>
                  Nº registro porcino
                  <input value={form.pigRegistrationNumber} onChange={(event) => updateField('pigRegistrationNumber', event.target.value)} placeholder="RPO-2026-0001" />
                </label>
                <label>
                  Marca/crotal
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

  if (!animal) {
    return null;
  }

  const speciesTone = speciesToneMap[animal.livestockSpecies] ?? speciesToneMap.Ovine;

  async function handleDischarge() {
    const cause = window.prompt('Causa de baja: Salida o Muerte', 'Salida');
    if (!cause) {
      return;
    }

    const normalizedCause = cause.trim();
    if (!dischargeCauseLabelMap[normalizedCause]) {
      setDischargeError('La causa de baja debe ser Salida (S) o Muerte (M).');
      return;
    }

    let destinationCode = null;
    if (normalizedCause === 'Muerte') {
      if (animal.livestockSpecies === 'Porcine') {
        destinationCode = 'MER';
      } else {
        const destination = window.prompt('Destino de la baja por muerte: SANDACH o MER', 'SANDACH');
        if (!destination) {
          return;
        }

        const normalizedDestination = destination.trim().toUpperCase();
        if (!['SANDACH', 'MER'].includes(normalizedDestination)) {
          setDischargeError('El destino de una baja por muerte debe ser SANDACH o MER.');
          return;
        }

        destinationCode = normalizedDestination;
      }
    }

    setDischarging(true);
    setDischargeError('');
    try {
      await onDischarged(animal.id, {
        dischargeDate: new Date().toISOString().slice(0, 10),
        dischargeCause: normalizedCause,
        destinationCode
      });
    } catch (requestError) {
      setDischargeError(requestError.message);
    } finally {
      setDischarging(false);
    }
  }

  return (
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
        {dischargeError && <div className="error-banner">{dischargeError}</div>}

        {tab === 'data' && !loading && (
          <>
            <AnimalDetailField label="Sexo" value={`${sexSymbol(animal.sex)} ${formatSex(animal.sex)}`.trim()} />
            <AnimalDetailField label="Año de identificación" value={animal.birthYear ?? 'No informado'} />
            <AnimalDetailField label="Explotación" value={animal.farmName} />
            <AnimalDetailField label="Fecha de alta" value={formatDate(animal.registrationDate)} />
            <AnimalDetailField label="Causa de alta" value={formatCause(animal.registrationCause)} />
            <AnimalDetailField label="Documento sanitario" value={animal.healthDocumentNumber ?? 'No informado'} />

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
              <button className="danger-button" type="button" onClick={handleDischarge} disabled={animal.status === 'Discharged' || discharging}>
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
          <button className="primary-button" type="button" onClick={() => setModalOpen(true)}>
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
        <AnimalFormModal farms={farms} loading={submitting} error={formError} onClose={() => setModalOpen(false)} onSubmit={createAnimal} />
      )}
    </div>
  );
}
