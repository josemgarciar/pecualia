import { useEffect, useState } from 'react';
import { Plus, Search, Skull, Tag } from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { ModalBody, ModalDialog, ModalFooter, ModalHeader } from '../../shared/components/modal/Modal';
import {
  buildMerCodeExample,
  getAnimalIdentificationFormatMessage,
  isValidAnimalIdentification,
  isValidMerCode,
  normalizeAnimalIdentification,
  normalizeMerCode
} from '../../shared/validation/identifiers';
import {
  SummaryMetric,
  createDeathFormState,
  emptyToNull,
  formatDate,
  formatDeathDestination,
  getDeathDestinationOptions,
  getDeathDestinationType,
  isMerOnlyDeathSpecies,
  porcineAnimalTypeOptions
} from './FarmDetailShared';

export function FarmDeathsSection({ farm, token }) {
  const isMerOnlyFarm = isMerOnlyDeathSpecies(farm.livestockSpecies);
  const [deaths, setDeaths] = useState([]);
  const [search, setSearch] = useState('');
  const [destination, setDestination] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [formError, setFormError] = useState('');
  const [success, setSuccess] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [form, setForm] = useState(() => createDeathFormState(farm.livestockSpecies));
  const merCodeExample = buildMerCodeExample();

  useEffect(() => {
    loadDeaths();
  }, [farm.id, token]);

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

  async function handleSubmit(event) {
    event.preventDefault();
    setError('');
    setFormError('');
    setSuccess('');
    const quantity = Number(form.quantity);

    if (!form.dischargeDate || !form.destinationCode) {
      setFormError('Fecha y destino son obligatorios.');
      return;
    }

    if (farm.livestockSpecies === 'Porcine') {
      if (!form.animalType.trim()) {
        setFormError('El tipo de animal es obligatorio en porcino.');
        return;
      }

      if (!Number.isInteger(quantity) || quantity <= 0) {
        setFormError('El número de animales debe ser un entero mayor que cero.');
        return;
      }

      if (form.identification.trim() && quantity !== 1) {
        setFormError('Si indicas un crotal individual, el número de animales debe ser 1.');
        return;
      }

      if (form.identification.trim() && !isValidAnimalIdentification(farm.livestockSpecies, form.identification)) {
        setFormError(getAnimalIdentificationFormatMessage(farm.livestockSpecies));
        return;
      }
    } else {
      if (!form.identification.trim()) {
        setFormError('Crotal, fecha y destino son obligatorios.');
        return;
      }

      if (!isValidAnimalIdentification(farm.livestockSpecies, form.identification)) {
        setFormError(getAnimalIdentificationFormatMessage(farm.livestockSpecies));
        return;
      }
    }

    if (form.destinationCode === 'MER' && !form.merCode.trim()) {
      setFormError('Debes indicar el número de MER.');
      return;
    }

    if (form.destinationCode === 'MER' && !isValidMerCode(form.merCode)) {
      setFormError(`El número MER debe tener formato ${merCodeExample}.`);
      return;
    }

    setSubmitting(true);
    try {
      await apiRequest(`/api/farms/${farm.id}/deaths`, {
        method: 'POST',
        token,
        body: {
          identification: form.identification.trim()
            ? normalizeAnimalIdentification(form.identification)
            : null,
          animalType: farm.livestockSpecies === 'Porcine'
            ? emptyToNull(form.animalType)
            : null,
          quantity: farm.livestockSpecies === 'Porcine' ? quantity : 1,
          dischargeDate: form.dischargeDate,
          destinationCode: form.destinationCode === 'MER' ? normalizeMerCode(form.merCode) : form.destinationCode
        }
      });
      setSuccess('Baja por muerte registrada correctamente.');
      setModalOpen(false);
      setForm(createDeathFormState(farm.livestockSpecies));
      setFormError('');
      await loadDeaths();
    } catch (requestError) {
      setFormError(requestError.message);
    } finally {
      setSubmitting(false);
    }
  }

  const filteredDeaths = deaths.filter((death) => {
    const matchesSearch = !search.trim() || `${death.identification ?? ''} ${death.animalType ?? ''} ${death.breed ?? ''}`.toLowerCase().includes(search.trim().toLowerCase());
    const matchesDestination = !destination || getDeathDestinationType(death.destinationCode) === destination;
    return matchesSearch && matchesDestination;
  });
  const destinationOptions = getDeathDestinationOptions(farm.livestockSpecies);
  const totalAnimals = deaths.reduce((sum, death) => sum + (death.numberOfAnimals ?? 1), 0);
  const sandachCount = deaths
    .filter((death) => death.destinationCode === 'SANDACH')
    .reduce((sum, death) => sum + (death.numberOfAnimals ?? 1), 0);
  const merCount = deaths
    .filter((death) => getDeathDestinationType(death.destinationCode) === 'MER')
    .reduce((sum, death) => sum + (death.numberOfAnimals ?? 1), 0);

  if (loading) {
    return <div className="panel-card empty-state">Cargando bajas por muerte...</div>;
  }

  return (
    <section className="panel-card stack">
      <div className="farm-detail-metrics">
        <SummaryMetric label="Total animales de baja" value={totalAnimals} />
        {!isMerOnlyFarm && <SummaryMetric label="SANDACH" value={sandachCount} />}
        {!isMerOnlyFarm && <SummaryMetric label="MER" value={merCount} />}
        <SummaryMetric label="Explotación" value={farm.name} />
      </div>

      {error && <div className="error-banner">{error}</div>}
      {success && <div className="success-banner">{success}</div>}

      <div className="section-heading-row">
          <div className="animal-filters farm-animals-filters">
          <div className="animal-search">
            <Search size={14} />
            <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Buscar por crotal, tipo o raza..." />
          </div>
          <select value={destination} onChange={(event) => setDestination(event.target.value)}>
            <option value="">Todos los destinos</option>
            {destinationOptions.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
        </div>
        <button className="primary-button" type="button" onClick={() => {
          setError('');
          setFormError('');
          setSuccess('');
          setForm(createDeathFormState(farm.livestockSpecies));
          setModalOpen(true);
        }}>
          <Plus size={16} />
          Registrar baja
        </button>
      </div>

      <div className="animal-table-card">
        <div className="table-scroll">
          <table className="animal-table">
            <thead>
              <tr>
                {['Crotal / tipo', 'Nº animales', 'Raza / sexo / año', 'Fecha baja', 'Causa baja', 'Destino'].map((header) => <th key={header}>{header}</th>)}
              </tr>
            </thead>
            <tbody>
              {filteredDeaths.map((death) => (
                <tr key={death.id}>
                  <td>
                    <div className="animal-identification-cell">
                      <Tag size={13} />
                      <strong>{death.identification ?? 'Sin crotal'}</strong>
                    </div>
                    {!death.identification && death.animalType && <small>{death.animalType}</small>}
                  </td>
                  <td>{death.numberOfAnimals ?? 1}</td>
                  <td>
                    {death.breed ?? death.animalType ?? '—'} · {death.sex ?? 'Sexo no informado'} · {death.birthYear ?? 'Año no informado'}
                  </td>
                  <td>{formatDate(death.dischargeDate)}</td>
                  <td><span className="animal-chip death-chip">Muerte</span></td>
                  <td>{formatDeathDestination(death.destinationCode)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="animal-table-footer">{filteredDeaths.length} de {deaths.length} registros de baja por muerte</div>
      </div>

      {modalOpen && (
        <ModalDialog cardAs="form" size="wide" onSubmit={handleSubmit}>
          <ModalHeader
            icon={<Skull size={18} />}
            title="Registrar baja por muerte"
            subtitle={farm.name}
            onClose={() => {
              setModalOpen(false);
              setFormError('');
            }}
          />
          <ModalBody className="operation-modal-body">
            {formError && <div className="error-banner">{formError}</div>}
            <label>
              <span>{farm.livestockSpecies === 'Porcine' ? 'Crotal / identificación' : 'Crotal / identificación *'}</span>
              <input value={form.identification} onChange={(event) => setForm({ ...form, identification: event.target.value })} placeholder={farm.livestockSpecies === 'Porcine' ? 'Opcional. Ej: GT1800001004' : 'Ej: ES0600005831'} />
            </label>
            {farm.livestockSpecies === 'Porcine' && (
              <label>
                <span>Tipo de animal *</span>
                <select value={form.animalType} onChange={(event) => setForm({ ...form, animalType: event.target.value })}>
                  <option value="">Seleccionar...</option>
                  {porcineAnimalTypeOptions.map((option) => (
                    <option key={option} value={option}>{option}</option>
                  ))}
                </select>
              </label>
            )}
            {farm.livestockSpecies === 'Porcine' && (
              <label>
                <span>Nº animales *</span>
                <input
                  type="number"
                  min="1"
                  step="1"
                  value={form.quantity}
                  onChange={(event) => setForm({ ...form, quantity: event.target.value })}
                />
              </label>
            )}
            <label>
              <span>Fecha de baja *</span>
              <input type="date" value={form.dischargeDate} onChange={(event) => setForm({ ...form, dischargeDate: event.target.value })} />
            </label>
            <label>
              <span>Destino *</span>
              <select
                value={form.destinationCode}
                disabled={isMerOnlyFarm}
                onChange={(event) => setForm({ ...form, destinationCode: event.target.value, merCode: event.target.value === 'MER' ? form.merCode : '' })}>
                {!isMerOnlyFarm && <option value="">Seleccionar...</option>}
                {destinationOptions.map((option) => (
                  <option key={option.value} value={option.value}>{option.label}</option>
                ))}
              </select>
            </label>
            {form.destinationCode === 'MER' && (
              <label>
                <span>Nº MER *</span>
                <div className="inline-form-actions">
                  <input
                    value={form.merCode}
                    onChange={(event) => setForm({ ...form, merCode: event.target.value })}
                    placeholder={merCodeExample}
                  />
                </div>
                <small>Formato obligatorio: {merCodeExample}</small>
              </label>
            )}
          </ModalBody>
          <ModalFooter align="end">
            <button className="secondary-button" type="button" onClick={() => {
              setModalOpen(false);
              setFormError('');
            }}>Cancelar</button>
            <button className="primary-button" type="submit" disabled={submitting}>{submitting ? 'Guardando...' : 'Guardar baja'}</button>
          </ModalFooter>
        </ModalDialog>
      )}
    </section>
  );
}
