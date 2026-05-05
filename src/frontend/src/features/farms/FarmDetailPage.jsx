import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  ArrowLeft,
  BarChart3,
  BookOpen,
  Building2,
  ArrowLeftRight,
  Check,
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
  TriangleAlert,
  X
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

const registrationCauseLabelMap = {
  Entrada: 'Entrada (E)',
  Autorreposicion: 'Autorreposición (A)'
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

function formatText(value, fallback = 'No informado') {
  return value ?? fallback;
}

function formatRegime(value) {
  if (!value) {
    return 'No informado';
  }

  return regimeLabelMap[value] ?? value;
}

function formatRegistrationCause(value) {
  return registrationCauseLabelMap[value] ?? value ?? '—';
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

function FarmAnimalsSection({ farm, token }) {
  const [animals, setAnimals] = useState([]);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const identificationLabel = farm.livestockSpecies === 'Porcine' ? 'Lote' : 'Crotal';
  const activeAnimals = animals.filter((animal) => animal.status === 'Active').length;
  const isInitialLoading = loading && animals.length === 0 && !error;

  useEffect(() => {
    let cancelled = false;

    async function loadAnimals() {
      setLoading(true);
      setError('');

      try {
        const params = new URLSearchParams();
        if (search.trim()) {
          params.set('search', search.trim());
        }
        if (status) {
          params.set('status', status);
        }

        const response = await apiRequest(`/api/farms/${farm.id}/animals${params.toString() ? `?${params}` : ''}`, { token });
        if (!cancelled) {
          setAnimals(response);
        }
      } catch (requestError) {
        if (!cancelled) {
          setError(requestError.message);
          setAnimals([]);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    loadAnimals();

    return () => {
      cancelled = true;
    };
  }, [farm.id, search, status, token]);

  return (
    <section className="panel-card stack">
      <div className="farm-animals-header">
        <p>{loading && !isInitialLoading ? 'Actualizando animales...' : `${activeAnimals} activos · ${animals.length} en total`}</p>
      </div>

      {error && <div className="error-banner">{error}</div>}

      <div className="animal-filters farm-animals-filters">
        <div className="animal-search">
          <Search size={14} />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={`Buscar ${identificationLabel.toLowerCase()} o raza...`} />
        </div>
        <select value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="">Todos los estados</option>
          <option value="Active">Activo</option>
          <option value="Discharged">Baja</option>
        </select>
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
                  {[identificationLabel, 'Especie', 'Raza', 'Sexo', 'Año', 'Fecha identificación', 'Causa alta', 'Estado'].map((header) => (
                    <th key={header}>{header}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {animals.map((animal) => {
                  const speciesTone = speciesToneMap[animal.livestockSpecies] ?? { bg: '#F3F4F6', color: '#6B7280', label: animal.livestockSpecies };
                  const statusTone = animal.status === 'Discharged'
                    ? { bg: '#FEE2E2', color: '#DC2626', label: 'Baja' }
                    : { bg: '#DDEBDF', color: '#2F6B4F', label: 'Activo' };
                  const sexLabel = animal.sex === 'Female' ? 'Hembra' : animal.sex === 'Male' ? 'Macho' : 'No informado';

                  return (
                    <tr key={animal.id}>
                      <td>
                        <div className="animal-identification-cell">
                          <Tag size={13} />
                          <strong>{animal.identification}</strong>
                        </div>
                      </td>
                      <td><span className="animal-chip" style={{ background: speciesTone.bg, color: speciesTone.color }}>{speciesTone.label}</span></td>
                      <td>{animal.breed ?? '—'}</td>
                      <td>{animal.sex === 'Female' ? '♀' : animal.sex === 'Male' ? '♂' : ''} {sexLabel}</td>
                      <td>{animal.birthYear ?? '—'}</td>
                      <td>{animal.registrationDate ? new Intl.DateTimeFormat('es-ES').format(new Date(`${animal.registrationDate}T00:00:00`)) : '—'}</td>
                      <td>{formatRegistrationCause(animal.registrationCause)}</td>
                      <td><span className="animal-chip" style={{ background: statusTone.bg, color: statusTone.color }}>{statusTone.label}</span></td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <div className="animal-table-footer">{animals.length} animales</div>
        </div>
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

        <div className="info-callout">
          <Sprout size={18} />
          <p>El nacimiento registra trazabilidad agregada para censo y balance. No crea animales ni solicita crotales.</p>
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
          <div className="modal-backdrop" role="dialog" aria-modal="true">
            <form className="modal-card modal-wide farm-modal-shell" onSubmit={handleSubmit}>
              <div className="farm-modal-header">
                <div className="farm-modal-title">
                  <div className="modal-panel-icon"><Sprout size={18} /></div>
                  <div>
                    <h2>Nuevo nacimiento</h2>
                    <p>{farm.name}</p>
                  </div>
                </div>
                <button className="farm-modal-close" type="button" onClick={() => setModalOpen(false)} aria-label="Cerrar modal">
                  <X size={18} />
                </button>
              </div>
              <div className="farm-modal-body operation-modal-body">
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
              </div>
              <div className="farm-modal-footer">
                <button className="secondary-button" type="button" onClick={() => setModalOpen(false)}>Cancelar</button>
                <button className="primary-button" type="submit" disabled={submitting}>{submitting ? 'Guardando...' : 'Guardar nacimiento'}</button>
              </div>
            </form>
          </div>
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

    setSubmitting(true);
    try {
      await apiRequest(`/api/farms/${farm.id}/deaths`, {
        method: 'POST',
        token,
        body: {
          identification: form.identification.trim(),
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
      <div className="info-callout info-callout-danger">
        <Skull size={18} />
        <p>Esta sección solo gestiona bajas cuya causa oficial es Muerte. Las bajas por Salida se gestionan desde movimientos/importación.</p>
      </div>

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
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <form className="modal-card modal-wide farm-modal-shell" onSubmit={handleSubmit}>
            <div className="farm-modal-header">
              <div className="farm-modal-title">
                <div className="modal-panel-icon"><Skull size={18} /></div>
                <div>
                  <h2>Registrar baja por muerte</h2>
                  <p>{farm.name}</p>
                </div>
              </div>
              <button className="farm-modal-close" type="button" onClick={() => setModalOpen(false)} aria-label="Cerrar modal">
                <X size={18} />
              </button>
            </div>
            <div className="farm-modal-body operation-modal-body">
              <label>
                <span>Crotal / identificación *</span>
                <input value={form.identification} onChange={(event) => setForm({ ...form, identification: event.target.value })} placeholder="Ej: ES0600005831" />
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
            </div>
            <div className="farm-modal-footer">
              <button className="secondary-button" type="button" onClick={() => setModalOpen(false)}>Cancelar</button>
              <button className="primary-button" type="submit" disabled={submitting}>{submitting ? 'Guardando...' : 'Guardar baja'}</button>
            </div>
          </form>
        </div>
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

    if (form.nextDose && form.nextDose < form.vaccinationDate) {
      setError('La próxima dosis no puede ser anterior a la fecha de vacunación.');
      return;
    }

    setSubmitting(true);
    try {
      const body = {
        animalIdentification: form.animalIdentification.trim(),
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
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <form className="modal-card modal-wide farm-modal-shell" onSubmit={handleSubmit}>
            <div className="farm-modal-header">
              <div className="farm-modal-title">
                <div className="modal-panel-icon"><Shield size={18} /></div>
                <div>
                  <h2>{editingVaccination ? 'Editar vacunación' : 'Nueva vacunación'}</h2>
                  <p>{farm.name}</p>
                </div>
              </div>
              <button className="farm-modal-close" type="button" onClick={closeModal} aria-label="Cerrar modal">
                <X size={18} />
              </button>
            </div>
            <div className="farm-modal-body operation-modal-body">
              <label>
                <span>{identificationLabel} *</span>
                <input
                  value={form.animalIdentification}
                  onChange={(event) => setForm({ ...form, animalIdentification: event.target.value })}
                  placeholder={farm.livestockSpecies === 'Porcine' ? 'Ej: LOTE-2026-01' : 'Ej: ES0600005831'}
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
            </div>
            <div className="farm-modal-footer">
              <button className="secondary-button" type="button" onClick={closeModal}>Cancelar</button>
              <button className="primary-button" type="submit" disabled={submitting}>
                {submitting ? 'Guardando...' : editingVaccination ? 'Guardar cambios' : 'Guardar vacunación'}
              </button>
            </div>
          </form>
        </div>
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

    setSubmitting(true);
    try {
      await apiRequest(`/api/farms/${farm.id}/incidents`, {
        method: 'POST',
        token,
        body: {
          animalIdentification: emptyToNull(form.animalIdentification),
          incidentDate: form.incidentDate,
          changeReason: emptyToNull(form.changeReason),
          description: emptyToNull(form.description),
          lastIdentification: emptyToNull(form.lastIdentification),
          newIdentification: emptyToNull(form.newIdentification)
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
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <form className="modal-card modal-wide farm-modal-shell" onSubmit={handleSubmit}>
            <div className="farm-modal-header">
              <div className="farm-modal-title">
                <div className="modal-panel-icon"><TriangleAlert size={18} /></div>
                <div>
                  <h2>Nueva incidencia</h2>
                  <p>{farm.name}</p>
                </div>
              </div>
              <button className="farm-modal-close" type="button" onClick={() => setModalOpen(false)} aria-label="Cerrar modal">
                <X size={18} />
              </button>
            </div>
            <div className="farm-modal-body operation-modal-body">
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
            </div>
            <div className="farm-modal-footer">
              <button className="secondary-button" type="button" onClick={() => setModalOpen(false)}>Cancelar</button>
              <button className="primary-button" type="submit" disabled={submitting}>{submitting ? 'Guardando...' : 'Guardar incidencia'}</button>
            </div>
          </form>
        </div>
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
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <form className="modal-card modal-wide farm-modal-shell" onSubmit={handleSubmit}>
            <div className="farm-modal-header">
              <div className="farm-modal-title">
                <div className="modal-panel-icon"><ClipboardCheck size={18} /></div>
                <div>
                  <h2>Nueva inspección</h2>
                  <p>{farm.name}</p>
                </div>
              </div>
              <button className="farm-modal-close" type="button" onClick={() => setModalOpen(false)} aria-label="Cerrar modal">
                <X size={18} />
              </button>
            </div>
            <div className="farm-modal-body operation-modal-body">
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
            </div>
            <div className="farm-modal-footer">
              <button className="secondary-button" type="button" onClick={() => setModalOpen(false)}>Cancelar</button>
              <button className="primary-button" type="submit" disabled={submitting}>{submitting ? 'Guardando...' : 'Guardar inspección'}</button>
            </div>
          </form>
        </div>
      )}
    </section>
  );
}

export function FarmDetailPage() {
  const { farmId } = useParams();
  const navigate = useNavigate();
  const { token } = useAuth();
  const [farm, setFarm] = useState(null);
  const [activeTab, setActiveTab] = useState('summary');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function loadFarmDetail() {
      setLoading(true);
      setError('');

      try {
        const response = await apiRequest(`/api/farms/${farmId}`, { token });
        if (!cancelled) {
          setFarm(response);
        }
      } catch (requestError) {
        if (!cancelled) {
          setError(requestError.message);
          setFarm(null);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    if (farmId) {
      loadFarmDetail();
    } else {
      setLoading(false);
      setError('Explotación no encontrada.');
    }

    return () => {
      cancelled = true;
    };
  }, [farmId, token]);

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
            <p>Accede a cada bloque del detalle sin depender de un scroll horizontal.</p>
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
              <DetailField label="Tipo ganadero" value={formatText(farm.livestockType)} />
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

      {activeTab === 'animals' && <FarmAnimalsSection farm={farm} token={token} />}
      {activeTab === 'movements' && <FarmMovementsSection farm={farm} token={token} />}
      {activeTab === 'births' && <FarmBirthsSection farm={farm} token={token} />}
      {activeTab === 'deaths' && <FarmDeathsSection farm={farm} token={token} />}
      {activeTab === 'vaccinations' && <FarmVaccinationsSection farm={farm} token={token} />}
      {activeTab === 'balances' && <FarmCensusBalancesSection farm={farm} token={token} />}
      {activeTab === 'book' && <FarmBookSection farm={farm} token={token} />}
      {activeTab === 'incidents' && <FarmIncidentsSection farm={farm} token={token} />}
      {activeTab === 'inspections' && <FarmInspectionsSection farm={farm} token={token} />}
    </div>
  );
}
