import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Building2, Edit3, MapPin } from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { useAuth } from '../../shared/auth/AuthContext';
import { normalizeRegaCode } from '../../shared/validation/identifiers';
import { FarmMovementsSection } from '../movements/FarmMovementsSection';
import { FarmAnimalsSection } from './FarmAnimalsSection';
import { FarmBirthsSection } from './FarmBirthsSection';
import { FarmBookSection } from './FarmBookSection';
import { FarmCensusBalancesSection } from './FarmCensusBalancesSection';
import {
  createFarmSettingsForm,
  detailTabs,
  emptyToNull,
  FarmSettingsModal,
  formatRegime,
  formatText,
  speciesToneMap,
  SummaryMetric,
  validateFarmSettingsForm
} from './FarmDetailShared';
import { FarmDeathsSection } from './FarmDeathsSection';
import { FarmIncidentsSection } from './FarmIncidentsSection';
import { FarmInspectionsSection } from './FarmInspectionsSection';
import { FarmSummarySection } from './FarmSummarySection';
import { FarmVaccinationsSection } from './FarmVaccinationsSection';

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
          livestockType: emptyToNull(settingsForm.livestockType),
          productionCapacity: settingsForm.productionCapacity === '' ? null : Number(settingsForm.productionCapacity),
          responsible: emptyToNull(settingsForm.responsible),
          zootechnicClassification: emptyToNull(settingsForm.zootechnicClassification),
          spindle: settingsForm.spindle === '' ? null : Number(settingsForm.spindle),
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
  const activeTabConfig = detailTabs.find((tab) => tab.key === activeTab) ?? detailTabs[0];
  const sectionContent = {
    summary: <FarmSummarySection farm={farm} />,
    animals: (
      <FarmAnimalsSection
        farm={farm}
        token={token}
        movementFilter={movementAnimalFilter}
        onClearMovementFilter={() => setMovementAnimalFilter(null)}
      />
    ),
    movements: (
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
    ),
    births: <FarmBirthsSection farm={farm} token={token} />,
    deaths: <FarmDeathsSection farm={farm} token={token} />,
    vaccinations: <FarmVaccinationsSection farm={farm} token={token} />,
    balances: <FarmCensusBalancesSection farm={farm} token={token} />,
    book: <FarmBookSection farm={farm} token={token} />,
    incidents: <FarmIncidentsSection farm={farm} token={token} />,
    inspections: <FarmInspectionsSection farm={farm} token={token} />
  };

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

      {sectionContent[activeTab]}

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
