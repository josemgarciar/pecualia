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
  formatDate,
  formatDeathDestination,
  getDeathDestinationOptions,
  getDeathDestinationType
} from './FarmDetailShared';

export function FarmDeathsSection({ farm, token }) {
  const isPorcineFarm = farm.livestockSpecies === 'Porcine';
  const [deaths, setDeaths] = useState([]);
  const [search, setSearch] = useState('');
  const [destination, setDestination] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
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
    setSuccess('');

    if (!form.identification.trim() || !form.dischargeDate || !form.destinationCode) {
      setError('Crotal, fecha y destino son obligatorios.');
      return;
    }

    if (form.destinationCode === 'MER' && !form.merCode.trim()) {
      setError('Debes indicar el número de MER.');
      return;
    }

    if (!isValidAnimalIdentification(farm.livestockSpecies, form.identification)) {
      setError(getAnimalIdentificationFormatMessage(farm.livestockSpecies));
      return;
    }

    if (form.destinationCode === 'MER' && !isValidMerCode(form.merCode)) {
      setError(`El número MER debe tener formato ${merCodeExample}.`);
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
          destinationCode: form.destinationCode === 'MER' ? normalizeMerCode(form.merCode) : form.destinationCode
        }
      });
      setSuccess('Baja por muerte registrada correctamente.');
      setModalOpen(false);
      setForm(createDeathFormState(farm.livestockSpecies));
      await loadDeaths();
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setSubmitting(false);
    }
  }

  const filteredDeaths = deaths.filter((death) => {
    const matchesSearch = !search.trim() || `${death.identification} ${death.breed ?? ''}`.toLowerCase().includes(search.trim().toLowerCase());
    const matchesDestination = !destination || getDeathDestinationType(death.destinationCode) === destination;
    return matchesSearch && matchesDestination;
  });
  const destinationOptions = getDeathDestinationOptions(farm.livestockSpecies);
  const sandachCount = deaths.filter((death) => death.destinationCode === 'SANDACH').length;
  const merCount = deaths.filter((death) => getDeathDestinationType(death.destinationCode) === 'MER').length;

  if (loading) {
    return <div className="panel-card empty-state">Cargando bajas por muerte...</div>;
  }

  return (
    <section className="panel-card stack">
      <div className="farm-detail-metrics">
        <SummaryMetric label="Total bajas por muerte" value={deaths.length} />
        {!isPorcineFarm && <SummaryMetric label="SANDACH" value={sandachCount} />}
        {!isPorcineFarm && <SummaryMetric label="MER" value={merCount} />}
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
        <button className="primary-button" type="button" onClick={() => {
          setError('');
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
                  <td>{formatDeathDestination(death.destinationCode)}</td>
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
              <select value={form.destinationCode} onChange={(event) => setForm({ ...form, destinationCode: event.target.value, merCode: event.target.value === 'MER' ? form.merCode : '' })}>
                <option value="">Seleccionar...</option>
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
            <div className="info-callout">
              <Skull size={18} />
              <p>
                {farm.livestockSpecies === 'Porcine'
                  ? `En porcino, la baja por muerte solo puede registrarse con un número MER válido (${merCodeExample}).`
                  : `La causa oficial guardada será Baja - Causa Muerte. Si el destino es MER, debes indicar un número válido con formato ${merCodeExample}.`}
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
