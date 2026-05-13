import { useEffect, useState } from 'react';
import { Plus, TriangleAlert } from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { ModalBody, ModalDialog, ModalFooter, ModalHeader } from '../../shared/components/modal/Modal';
import {
  isValidAnimalIdentification,
  normalizeAnimalIdentification
} from '../../shared/validation/identifiers';
import { emptyToNull, formatDate } from './FarmDetailShared';

function createIncidentFormState() {
  return {
    animalIdentification: '',
    incidentDate: new Date().toISOString().slice(0, 10),
    changeReason: '',
    description: '',
    lastIdentification: '',
    newIdentification: ''
  };
}

export function FarmIncidentsSection({ farm, token }) {
  const [incidents, setIncidents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [form, setForm] = useState(createIncidentFormState);

  useEffect(() => {
    loadIncidents();
  }, [farm.id, token]);

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

    if (form.animalIdentification.trim() && !isValidAnimalIdentification(farm.livestockSpecies, form.animalIdentification)) {
      setError('La identificación del animal relacionada con la incidencia no es válida.');
      return;
    }

    if (form.lastIdentification.trim() && !isValidAnimalIdentification(farm.livestockSpecies, form.lastIdentification)) {
      setError('La identificación anterior no es válida.');
      return;
    }

    if (form.newIdentification.trim() && !isValidAnimalIdentification(farm.livestockSpecies, form.newIdentification)) {
      setError('La nueva identificación no es válida.');
      return;
    }

    setSubmitting(true);
    try {
      await apiRequest(`/api/farms/${farm.id}/incidents`, {
        method: 'POST',
        token,
        body: {
          animalIdentification: form.animalIdentification.trim() ? normalizeAnimalIdentification(form.animalIdentification) : null,
          incidentDate: form.incidentDate,
          changeReason: emptyToNull(form.changeReason),
          description: emptyToNull(form.description),
          lastIdentification: form.lastIdentification.trim() ? normalizeAnimalIdentification(form.lastIdentification) : null,
          newIdentification: form.newIdentification.trim() ? normalizeAnimalIdentification(form.newIdentification) : null
        }
      });
      setSuccess('Incidencia registrada correctamente.');
      setModalOpen(false);
      setForm(createIncidentFormState());
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
        <ModalDialog cardAs="form" size="wide" onSubmit={handleSubmit}>
          <ModalHeader
            icon={<TriangleAlert size={18} />}
            title="Nueva incidencia"
            subtitle={farm.name}
            onClose={() => setModalOpen(false)}
          />
          <ModalBody className="operation-modal-body">
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
          </ModalBody>
          <ModalFooter align="end">
            <button className="secondary-button" type="button" onClick={() => setModalOpen(false)}>Cancelar</button>
            <button className="primary-button" type="submit" disabled={submitting}>{submitting ? 'Guardando...' : 'Guardar incidencia'}</button>
          </ModalFooter>
        </ModalDialog>
      )}
    </section>
  );
}
