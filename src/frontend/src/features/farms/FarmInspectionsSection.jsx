import { useEffect, useState } from 'react';
import { ClipboardCheck, Plus } from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { ModalBody, ModalDialog, ModalFooter, ModalHeader } from '../../shared/components/modal/Modal';
import { emptyToNull, formatDate, parseOptionalInteger } from './FarmDetailShared';

function createInspectionFormState() {
  return {
    inspectionDate: new Date().toISOString().slice(0, 10),
    reason: '',
    observations: '',
    veterinary: '',
    taggedAnimals: ''
  };
}

export function FarmInspectionsSection({ farm, token }) {
  const [inspections, setInspections] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [form, setForm] = useState(createInspectionFormState);

  useEffect(() => {
    loadInspections();
  }, [farm.id, token]);

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
      setForm(createInspectionFormState());
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
        <ModalDialog cardAs="form" size="wide" onSubmit={handleSubmit}>
          <ModalHeader
            icon={<ClipboardCheck size={18} />}
            title="Nueva inspección"
            subtitle={farm.name}
            onClose={() => setModalOpen(false)}
          />
          <ModalBody className="operation-modal-body">
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
          </ModalBody>
          <ModalFooter align="end">
            <button className="secondary-button" type="button" onClick={() => setModalOpen(false)}>Cancelar</button>
            <button className="primary-button" type="submit" disabled={submitting}>{submitting ? 'Guardando...' : 'Guardar inspección'}</button>
          </ModalFooter>
        </ModalDialog>
      )}
    </section>
  );
}
