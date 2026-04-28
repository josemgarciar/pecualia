import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  ArrowLeft,
  BarChart3,
  BookOpen,
  Building2,
  ClipboardCheck,
  MapPin,
  Search,
  Skull,
  Sprout,
  Tag,
  TriangleAlert
} from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { useAuth } from '../../shared/auth/AuthContext';

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

const detailTabs = [
  { key: 'summary', label: 'Resumen', icon: Building2, enabled: true },
  { key: 'animals', label: 'Animales', icon: Tag, enabled: true },
  { key: 'movements', label: 'Movimientos', icon: Building2, enabled: false },
  { key: 'births', label: 'Nacimientos', icon: Sprout, enabled: false },
  { key: 'deaths', label: 'Muertes', icon: Skull, enabled: false },
  { key: 'balances', label: 'Censos y balances', icon: BarChart3, enabled: false },
  { key: 'book', label: 'Libro', icon: BookOpen, enabled: false },
  { key: 'incidents', label: 'Incidencias', icon: TriangleAlert, enabled: false },
  { key: 'inspections', label: 'Inspecciones', icon: ClipboardCheck, enabled: false }
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

function FarmAnimalsSection({ farmId, token }) {
  const [animals, setAnimals] = useState([]);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    let cancelled = false;

    async function loadAnimals() {
      setLoading(true);
      setError('');

      try {
        const params = new URLSearchParams();
        if (search) {
          params.set('search', search);
        }
        if (status) {
          params.set('status', status);
        }

        const response = await apiRequest(`/api/farms/${farmId}/animals${params.toString() ? `?${params}` : ''}`, { token });
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
  }, [farmId, search, status, token]);

  if (loading) {
    return <div className="panel-card empty-state">Cargando animales de la explotación...</div>;
  }

  return (
    <section className="panel-card stack">
      <div className="farm-animals-header">
        <p>{animals.filter((animal) => animal.status === 'Active').length} activos · {animals.length} en total</p>
      </div>

      {error && <div className="error-banner">{error}</div>}

      <div className="animal-filters farm-animals-filters">
        <div className="animal-search">
          <Search size={14} />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Buscar crotal o raza..." />
        </div>
        <select value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="">Todos los estados</option>
          <option value="Active">Activo</option>
          <option value="Discharged">Baja</option>
        </select>
      </div>

      {animals.length === 0 ? (
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
                  {['Crotal', 'Especie', 'Raza', 'Sexo', 'Año nac.', 'Fecha alta', 'Causa alta', 'Estado'].map((header) => (
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
                      <td>{animal.registrationCause ?? '—'}</td>
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
        <div className="farm-detail-tabs" role="tablist" aria-label="Secciones de la explotación">
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
                <Icon size={15} />
                <span>{tab.label}</span>
                {!tab.enabled && <small>Próximamente</small>}
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

      {activeTab === 'animals' && <FarmAnimalsSection farmId={farm.id} token={token} />}
    </div>
  );
}
