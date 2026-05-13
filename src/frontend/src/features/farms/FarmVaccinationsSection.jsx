import { useEffect, useState } from 'react';
import { Edit3, Plus, Search, Shield, Trash2 } from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { ModalBody, ModalDialog, ModalFooter, ModalHeader } from '../../shared/components/modal/Modal';
import {
  getAnimalIdentificationFormatMessage,
  isValidAnimalIdentification,
  normalizeAnimalIdentification
} from '../../shared/validation/identifiers';
import {
  SummaryMetric,
  createVaccinationFormState,
  emptyToNull,
  formatDate
} from './FarmDetailShared';

export function FarmVaccinationsSection({ farm, token }) {
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

  useEffect(() => {
    loadVaccinations();
  }, [farm.id, token]);

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

    if (!isValidAnimalIdentification(farm.livestockSpecies, form.animalIdentification)) {
      setError(getAnimalIdentificationFormatMessage(farm.livestockSpecies));
      return;
    }

    if (form.nextDose && form.nextDose < form.vaccinationDate) {
      setError('La próxima dosis no puede ser anterior a la fecha de vacunación.');
      return;
    }

    setSubmitting(true);
    try {
      const body = {
        animalIdentification: normalizeAnimalIdentification(form.animalIdentification),
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
        <ModalDialog cardAs="form" size="wide" onSubmit={handleSubmit}>
          <ModalHeader
            icon={<Shield size={18} />}
            title={editingVaccination ? 'Editar vacunación' : 'Nueva vacunación'}
            subtitle={farm.name}
            onClose={closeModal}
          />
          <ModalBody className="operation-modal-body">
            <label>
              <span>{identificationLabel} *</span>
              <input
                value={form.animalIdentification}
                onChange={(event) => setForm({ ...form, animalIdentification: event.target.value })}
                placeholder={farm.livestockSpecies === 'Porcine' ? 'Ej: GT1800001004' : 'Ej: ES0600005831'}
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
          </ModalBody>
          <ModalFooter align="end">
            <button className="secondary-button" type="button" onClick={closeModal}>Cancelar</button>
            <button className="primary-button" type="submit" disabled={submitting}>
              {submitting ? 'Guardando...' : editingVaccination ? 'Guardar cambios' : 'Guardar vacunación'}
            </button>
          </ModalFooter>
        </ModalDialog>
      )}
    </section>
  );
}
