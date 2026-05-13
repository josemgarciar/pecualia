import { useEffect, useMemo, useState } from 'react';
import { Plus, Search, Tag } from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { normalizeAnimalIdentification, normalizeRegaCode } from '../../shared/validation/identifiers';
import {
  AnimalAutorrepositionModal,
  AnimalDetailModal,
  FARM_ANIMALS_DEFAULT_PAGE_SIZE,
  FARM_ANIMALS_PAGE_SIZE_OPTIONS,
  FARM_ANIMALS_SEARCH_DEBOUNCE_MS,
  createAnimalDetailForm,
  createAutorrepositionForm,
  emptyToNull,
  formatAnimalSex,
  validateAnimalDetailForm,
  validateAutorrepositionForm
} from './FarmDetailShared';

export function FarmAnimalsSection({ farm, token, movementFilter, onClearMovementFilter }) {
  const [animals, setAnimals] = useState([]);
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(FARM_ANIMALS_DEFAULT_PAGE_SIZE);
  const [totalCount, setTotalCount] = useState(0);
  const [activeCount, setActiveCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [reloadKey, setReloadKey] = useState(0);
  const [detailOpen, setDetailOpen] = useState(false);
  const [selectedAnimal, setSelectedAnimal] = useState(null);
  const [animalForm, setAnimalForm] = useState(createAnimalDetailForm(null));
  const [animalFormErrors, setAnimalFormErrors] = useState({});
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailSaving, setDetailSaving] = useState(false);
  const [detailDeleting, setDetailDeleting] = useState(false);
  const [detailError, setDetailError] = useState('');
  const [success, setSuccess] = useState('');
  const [autorrepositionOpen, setAutorrepositionOpen] = useState(false);
  const [autorrepositionSubmitting, setAutorrepositionSubmitting] = useState(false);
  const [autorrepositionError, setAutorrepositionError] = useState('');
  const [autorrepositionFormErrors, setAutorrepositionFormErrors] = useState({});
  const [autorrepositionForm, setAutorrepositionForm] = useState(() => createAutorrepositionForm(farm));
  const [autorrepositionAvailability, setAutorrepositionAvailability] = useState({ availableAnimals: 0, eligibleAnimals: 0 });
  const [loadingAutorrepositionBirths, setLoadingAutorrepositionBirths] = useState(false);
  const [breedOptions, setBreedOptions] = useState([]);
  const [loadingBreedOptions, setLoadingBreedOptions] = useState(false);
  const identificationLabel = farm.livestockSpecies === 'Porcine' ? 'Lote' : 'Crotal';
  const isInitialLoading = loading && animals.length === 0 && !error;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const visiblePageNumbers = useMemo(() => {
    const maxVisiblePages = 5;
    const startPage = Math.max(1, Math.min(page - 2, totalPages - maxVisiblePages + 1));
    const endPage = Math.min(totalPages, startPage + maxVisiblePages - 1);
    return Array.from({ length: endPage - startPage + 1 }, (_, index) => startPage + index);
  }, [page, totalPages]);
  const currentRangeStart = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const currentRangeEnd = totalCount === 0 ? 0 : Math.min(page * pageSize, totalCount);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setPage(1);
      setDebouncedSearch(search.trim());
    }, FARM_ANIMALS_SEARCH_DEBOUNCE_MS);

    return () => {
      window.clearTimeout(timeoutId);
    };
  }, [search]);

  useEffect(() => {
    setPage(1);
  }, [movementFilter?.movementId]);

  useEffect(() => {
    setAutorrepositionForm(createAutorrepositionForm(farm));
  }, [farm.id, farm.livestockSpecies]);

  useEffect(() => {
    if (!autorrepositionOpen) {
      return undefined;
    }

    let cancelled = false;

    async function loadAutorrepositionAvailability() {
      setLoadingAutorrepositionBirths(true);

      try {
        const response = await apiRequest(`/api/farms/${farm.id}/births/autorreposition-availability`, { token });
        if (!cancelled) {
          setAutorrepositionAvailability(response);
        }
      } catch (requestError) {
        if (!cancelled) {
          setAutorrepositionError(requestError.message);
          setAutorrepositionAvailability({ availableAnimals: 0, eligibleAnimals: 0 });
        }
      } finally {
        if (!cancelled) {
          setLoadingAutorrepositionBirths(false);
        }
      }
    }

    async function loadBreedOptions() {
      setLoadingBreedOptions(true);

      try {
        const response = await apiRequest(`/api/movements/breeds/${farm.livestockSpecies}`, { token });
        if (!cancelled) {
          setBreedOptions(response);
        }
      } catch (requestError) {
        if (!cancelled) {
          setAutorrepositionError(requestError.message);
          setBreedOptions([]);
        }
      } finally {
        if (!cancelled) {
          setLoadingBreedOptions(false);
        }
      }
    }

    loadAutorrepositionAvailability();
    loadBreedOptions();

    return () => {
      cancelled = true;
    };
  }, [autorrepositionOpen, farm.id, farm.livestockSpecies, token]);

  useEffect(() => {
    loadAnimals();
  }, [debouncedSearch, farm.id, movementFilter?.movementId, page, pageSize, reloadKey, token]);

  async function loadAnimals() {
    setLoading(true);
    setError('');

    try {
      const params = new URLSearchParams();
      if (debouncedSearch) {
        params.set('search', debouncedSearch);
      }
      if (movementFilter?.movementId) {
        params.set('movementId', String(movementFilter.movementId));
      }
      params.set('page', String(page));
      params.set('pageSize', String(pageSize));

      const response = await apiRequest(`/api/farms/${farm.id}/animals${params.toString() ? `?${params}` : ''}`, { token });
      setAnimals(response.items);
      setTotalCount(response.totalCount);
      setActiveCount(response.activeCount);
      setPage(response.page);
    } catch (requestError) {
      setError(requestError.message);
      setAnimals([]);
      setTotalCount(0);
      setActiveCount(0);
    } finally {
      setLoading(false);
    }
  }

  function updateAnimalField(field, value) {
    setAnimalForm((current) => ({ ...current, [field]: value }));
    setAnimalFormErrors((current) => {
      if (!current[field]) {
        return current;
      }

      const next = { ...current };
      delete next[field];
      return next;
    });
  }

  function openAutorrepositionModal() {
    setAutorrepositionForm(createAutorrepositionForm(farm));
    setAutorrepositionFormErrors({});
    setAutorrepositionError('');
    setAutorrepositionAvailability({ availableAnimals: 0, eligibleAnimals: 0 });
    setAutorrepositionOpen(true);
  }

  function closeAutorrepositionModal() {
    setAutorrepositionOpen(false);
    setAutorrepositionSubmitting(false);
    setAutorrepositionFormErrors({});
    setAutorrepositionError('');
    setAutorrepositionAvailability({ availableAnimals: 0, eligibleAnimals: 0 });
  }

  function updateAutorrepositionField(field, value) {
    setAutorrepositionForm((current) => ({ ...current, [field]: value }));
    setAutorrepositionFormErrors((current) => {
      if (!current[field]) {
        return current;
      }

      const next = { ...current };
      delete next[field];
      return next;
    });
    setAutorrepositionError('');
  }

  async function openAnimalModal(animalId) {
    setDetailOpen(true);
    setDetailLoading(true);
    setDetailSaving(false);
    setDetailDeleting(false);
    setDetailError('');
    setAnimalFormErrors({});

    try {
      const response = await apiRequest(`/api/animals/${animalId}`, { token });
      setSelectedAnimal(response);
      setAnimalForm(createAnimalDetailForm(response));
    } catch (requestError) {
      setDetailError(requestError.message);
      setSelectedAnimal(null);
      setAnimalForm(createAnimalDetailForm(null));
    } finally {
      setDetailLoading(false);
    }
  }

  function closeAnimalModal() {
    setDetailOpen(false);
    setSelectedAnimal(null);
    setAnimalForm(createAnimalDetailForm(null));
    setAnimalFormErrors({});
    setDetailError('');
    setDetailLoading(false);
    setDetailSaving(false);
    setDetailDeleting(false);
  }

  async function saveAnimalChanges() {
    if (!selectedAnimal) {
      return;
    }

    const validationErrors = validateAnimalDetailForm(animalForm, selectedAnimal.livestockSpecies);
    if (Object.keys(validationErrors).length > 0) {
      setAnimalFormErrors(validationErrors);
      return;
    }

    setDetailSaving(true);
    setDetailError('');

    try {
      const updatedAnimal = await apiRequest(`/api/animals/${selectedAnimal.id}`, {
        method: 'PUT',
        token,
        body: {
          identification: normalizeAnimalIdentification(animalForm.identification),
          birthYear: animalForm.birthYear === '' ? null : Number(animalForm.birthYear),
          breed: emptyToNull(animalForm.breed),
          sex: emptyToNull(animalForm.sex),
          registrationDate: animalForm.registrationDate || null,
          registrationCause: animalForm.registrationCause || null,
          originCode: animalForm.originCode.trim() ? normalizeRegaCode(animalForm.originCode) : null,
          ovinoCaprino: selectedAnimal.ovinoCaprino
            ? {
                speciesType: selectedAnimal.ovinoCaprino.speciesType,
                genotyping: emptyToNull(animalForm.genotyping),
                dominantAllele: emptyToNull(animalForm.dominantAllele),
                lowAllele: emptyToNull(animalForm.lowAllele)
              }
            : null,
          porcino: selectedAnimal.porcino
            ? {
                animalType: animalForm.animalType,
                identificationDate: animalForm.identificationDate || null,
                pigRegistrationNumber: emptyToNull(animalForm.pigRegistrationNumber),
                tag: emptyToNull(animalForm.tag)
              }
            : null
        }
      });

      setSelectedAnimal(updatedAnimal);
      setAnimalForm(createAnimalDetailForm(updatedAnimal));
      setReloadKey((current) => current + 1);
    } catch (requestError) {
      setDetailError(requestError.message);
    } finally {
      setDetailSaving(false);
    }
  }

  async function deleteAnimal() {
    if (!selectedAnimal) {
      return;
    }

    const confirmed = window.confirm(`Se eliminará el animal ${selectedAnimal.identification}. Esta acción no se puede deshacer.`);
    if (!confirmed) {
      return;
    }

    setDetailDeleting(true);
    setDetailError('');

    try {
      await apiRequest(`/api/animals/${selectedAnimal.id}`, { method: 'DELETE', token });
      closeAnimalModal();
      setReloadKey((current) => current + 1);
    } catch (requestError) {
      setDetailError(requestError.message);
    } finally {
      setDetailDeleting(false);
    }
  }

  async function submitAutorreposition(event) {
    event.preventDefault();

    const validationErrors = validateAutorrepositionForm(autorrepositionForm, farm.livestockSpecies, autorrepositionAvailability);
    if (Object.keys(validationErrors).length > 0) {
      setAutorrepositionFormErrors(validationErrors);
      return;
    }

    setAutorrepositionSubmitting(true);
    setAutorrepositionError('');
    setSuccess('');

    try {
      const response = await apiRequest(`/api/farms/${farm.id}/animals/autorreposition`, {
        method: 'POST',
        token,
        body: {
          startIdentification: normalizeAnimalIdentification(autorrepositionForm.startIdentification),
          quantity: Number(autorrepositionForm.quantity),
          breed: emptyToNull(autorrepositionForm.breed),
          sex: emptyToNull(autorrepositionForm.sex),
          registrationDate: autorrepositionForm.registrationDate || null,
          ovinoCaprino: farm.livestockSpecies === 'Porcine'
            ? null
            : {
                speciesType: farm.livestockSpecies,
                genotyping: emptyToNull(autorrepositionForm.genotyping),
                dominantAllele: emptyToNull(autorrepositionForm.dominantAllele),
                lowAllele: emptyToNull(autorrepositionForm.lowAllele)
              },
          porcino: farm.livestockSpecies !== 'Porcine'
            ? null
            : {
                animalType: autorrepositionForm.animalType.trim(),
                identificationDate: autorrepositionForm.identificationDate || null,
                pigRegistrationNumber: emptyToNull(autorrepositionForm.pigRegistrationNumber),
                tag: emptyToNull(autorrepositionForm.tag)
              }
        }
      });

      setSuccess(`Se han creado ${response.createdAnimals} animales desde ${response.firstIdentification} hasta ${response.lastIdentification}.`);
      closeAutorrepositionModal();
      setPage(1);
      setReloadKey((current) => current + 1);
    } catch (requestError) {
      setAutorrepositionError(requestError.message);
    } finally {
      setAutorrepositionSubmitting(false);
    }
  }

  return (
    <section className="panel-card stack">
      <div className="farm-animals-header">
        <div>
          <p>{loading && !isInitialLoading ? 'Actualizando animales...' : `${activeCount} activos · ${totalCount} en total`}</p>
        </div>
        <div className="movement-toolbar-actions">
          <button className="primary-button" type="button" onClick={openAutorrepositionModal}>
            <Plus size={16} />
            Autorreposición
          </button>
        </div>
      </div>

      {movementFilter && (
        <div className="filter-summary">
          <div>
            <strong>Filtro activo:</strong> guía {movementFilter.codRemo}
          </div>
          <button className="secondary-button" type="button" onClick={onClearMovementFilter}>
            Eliminar filtro
          </button>
        </div>
      )}

      {error && <div className="error-banner">{error}</div>}
      {success && <div className="success-banner">{success}</div>}

      <div className="animal-filters farm-animals-filters">
        <div className="animal-search">
          <Search size={14} />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={`Buscar ${identificationLabel.toLowerCase()} o raza...`} />
        </div>
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
                  {[identificationLabel, 'Año', 'Raza', 'Sexo', 'Causa alta', 'Procedencia', 'Causa baja', 'Destino', 'Guía entrada/salida'].map((header) => (
                    <th key={header}>{header}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {animals.map((animal) => {
                  const sexLabel = formatAnimalSex(animal.sex);
                  const breedValue = animal.breed ?? '—';
                  const registrationCauseValue = animal.registrationCause ?? '—';
                  const dischargeCauseValue = animal.dischargeCause ?? '—';
                  const guideSeriesValue = animal.entryGuideSerie || animal.exitGuideSerie
                    ? `${animal.entryGuideSerie ?? '—'} / ${animal.exitGuideSerie ?? '—'}`
                    : '—';

                  return (
                    <tr key={animal.id} onClick={() => openAnimalModal(animal.id)}>
                      <td>
                        <div className="animal-identification-cell">
                          <Tag size={13} />
                          <strong>{animal.identification}</strong>
                        </div>
                      </td>
                      <td>{animal.birthYear != null ? String(animal.birthYear) : '—'}</td>
                      <td>{breedValue}</td>
                      <td>{sexLabel}</td>
                      <td>{registrationCauseValue}</td>
                      <td>{animal.originCode ?? '—'}</td>
                      <td>{dischargeCauseValue}</td>
                      <td>{animal.destinationCode ?? '—'}</td>
                      <td>{guideSeriesValue}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <div className="animal-table-footer animal-table-footer-paginated">
            <span>{`Mostrando ${currentRangeStart}-${currentRangeEnd} de ${totalCount} animales`}</span>
            <div className="animal-pagination">
              <label className="animal-pagination-size">
                <span>Filas</span>
                <select value={pageSize} onChange={(event) => {
                  setPage(1);
                  setPageSize(Number(event.target.value));
                }}>
                  {FARM_ANIMALS_PAGE_SIZE_OPTIONS.map((option) => (
                    <option key={option} value={option}>{option}</option>
                  ))}
                </select>
              </label>
              <button type="button" className="animal-pagination-button" onClick={() => setPage((current) => Math.max(1, current - 1))} disabled={page <= 1}>
                Anterior
              </button>
              {visiblePageNumbers.map((pageNumber) => (
                <button
                  key={pageNumber}
                  type="button"
                  className={pageNumber === page ? 'animal-pagination-button animal-pagination-button-active' : 'animal-pagination-button'}
                  onClick={() => setPage(pageNumber)}
                >
                  {pageNumber}
                </button>
              ))}
              <button type="button" className="animal-pagination-button" onClick={() => setPage((current) => Math.min(totalPages, current + 1))} disabled={page >= totalPages}>
                Siguiente
              </button>
            </div>
          </div>
        </div>
      )}

      {detailOpen && (
        <AnimalDetailModal
          animal={selectedAnimal}
          form={animalForm}
          errors={animalFormErrors}
          loading={detailLoading}
          saving={detailSaving}
          deleting={detailDeleting}
          requestError={detailError}
          onChange={updateAnimalField}
          onClose={closeAnimalModal}
          onSave={saveAnimalChanges}
          onDelete={deleteAnimal}
        />
      )}

      {autorrepositionOpen && (
        <AnimalAutorrepositionModal
          farm={farm}
          form={autorrepositionForm}
          errors={autorrepositionFormErrors}
          requestError={autorrepositionError}
          submitting={autorrepositionSubmitting}
          availability={autorrepositionAvailability}
          loadingBirths={loadingAutorrepositionBirths}
          breedOptions={breedOptions}
          loadingBreedOptions={loadingBreedOptions}
          onChange={updateAutorrepositionField}
          onClose={closeAutorrepositionModal}
          onSubmit={submitAutorreposition}
        />
      )}
    </section>
  );
}
