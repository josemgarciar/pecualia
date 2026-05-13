import { useEffect, useState } from 'react';
import { Plus, Sprout } from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { ModalBody, ModalDialog, ModalFooter, ModalHeader } from '../../shared/components/modal/Modal';
import { BirthDetailModal, formatDate, parsePositiveNumber } from './FarmDetailShared';

function createBirthFormState() {
  return {
    birthDate: new Date().toISOString().slice(0, 10),
    offspringNumber: '1',
    birthWeight: '',
    observations: ''
  };
}

export function FarmBirthsSection({ farm, token }) {
  const [births, setBirths] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [editingBirth, setEditingBirth] = useState(null);
  const [detailBirth, setDetailBirth] = useState(null);
  const [form, setForm] = useState(createBirthFormState);

  useEffect(() => {
    loadBirths();
  }, [farm.id, token]);

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
      await apiRequest(`/api/farms/${farm.id}/births${editingBirth ? `/${editingBirth.id}` : ''}`, {
        method: editingBirth ? 'PUT' : 'POST',
        token,
        body: {
          birthDate: form.birthDate,
          offspringNumber,
          birthWeight,
          observations: form.observations.trim() || null
        }
      });
      setSuccess(editingBirth ? 'Nacimiento actualizado correctamente.' : 'Nacimiento registrado correctamente.');
      setModalOpen(false);
      setEditingBirth(null);
      setForm(createBirthFormState());
      await loadBirths();
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setSubmitting(false);
    }
  }

  function openCreateModal() {
    setEditingBirth(null);
    setDetailBirth(null);
    setForm(createBirthFormState());
    setModalOpen(true);
  }

  function openEditModal(birth) {
    setDetailBirth(null);
    setEditingBirth(birth);
    setForm({
      birthDate: birth.birthDate,
      offspringNumber: String(birth.offspringNumber),
      birthWeight: birth.birthWeight == null ? '' : String(birth.birthWeight),
      observations: birth.observations ?? ''
    });
    setModalOpen(true);
  }

  async function deleteBirth(birth) {
    const confirmed = window.confirm(`Se eliminará el nacimiento del ${formatDate(birth.birthDate)} con ${birth.offspringNumber} crías declaradas.`);
    if (!confirmed) {
      return;
    }

    setError('');
    setSuccess('');

    try {
      await apiRequest(`/api/farms/${farm.id}/births/${birth.id}`, {
        method: 'DELETE',
        token
      });
      setDetailBirth(null);
      setSuccess('Nacimiento eliminado correctamente.');
      await loadBirths();
    } catch (requestError) {
      setError(requestError.message);
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
          <button className="primary-button" type="button" onClick={openCreateModal}>
            <Plus size={16} />
            Registrar nacimiento
          </button>
        </div>

        {error && <div className="error-banner">{error}</div>}
        {success && <div className="success-banner">{success}</div>}

        {births.length === 0 ? (
          <div className="empty-state">
            <Sprout size={28} />
            <div>No hay nacimientos registrados para esta explotación.</div>
          </div>
        ) : (
          <div className="animal-table-card">
            <div className="table-scroll">
              <table className="animal-table">
                <thead>
                  <tr>
                    {['Fecha parto', 'Crías', 'Peso medio', 'Observaciones'].map((header) => (
                      <th key={header}>{header}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {births.map((birth) => (
                    <tr key={birth.id} onClick={() => setDetailBirth(birth)}>
                      <td>
                        <div className="animal-identification-cell">
                          <Sprout size={13} />
                          <strong>{formatDate(birth.birthDate)}</strong>
                        </div>
                      </td>
                      <td>{birth.offspringNumber}</td>
                      <td>{birth.birthWeight == null ? '—' : `${birth.birthWeight} kg`}</td>
                      <td>{birth.observations ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="animal-table-footer">{births.length} nacimientos registrados</div>
          </div>
        )}

        {modalOpen && (
          <ModalDialog cardAs="form" size="wide" onSubmit={handleSubmit}>
            <ModalHeader
              icon={<Sprout size={18} />}
              title={editingBirth ? 'Editar nacimiento' : 'Nuevo nacimiento'}
              subtitle={farm.name}
              onClose={() => {
                setModalOpen(false);
                setEditingBirth(null);
              }}
            />
            <ModalBody className="operation-modal-body">
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
            </ModalBody>
            <ModalFooter align="end">
              <button className="secondary-button" type="button" onClick={() => {
                setModalOpen(false);
                setEditingBirth(null);
              }}>Cancelar</button>
              <button className="primary-button" type="submit" disabled={submitting}>{submitting ? 'Guardando...' : editingBirth ? 'Guardar cambios' : 'Guardar nacimiento'}</button>
            </ModalFooter>
          </ModalDialog>
        )}

        {detailBirth && (
          <BirthDetailModal
            birth={detailBirth}
            farmName={farm.name}
            onClose={() => setDetailBirth(null)}
            onEdit={openEditModal}
            onDelete={deleteBirth}
          />
        )}
      </article>
    </section>
  );
}
