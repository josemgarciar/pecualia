import { speciesToneMap } from './farmOptions.js';
import { formatDate, formatAnimalCause, formatAnimalGuideSeries } from './farmFormatting.js';
import { ChevronDown, Tag, Trash2 } from 'lucide-react';
import { ModalBody, ModalDialog, ModalFieldLabel, ModalFooter, ModalHeader } from '../../shared/components/modal/Modal';

export function AnimalDetailModal({
  animal,
  form,
  errors,
  loading,
  saving,
  deleting,
  requestError,
  onChange,
  onClose,
  onSave,
  onDelete
}) {
  return (
    <ModalDialog size="wide" shellClassName="animal-modal">
      <ModalHeader
        icon={<Tag size={18} />}
        title="Detalle del animal"
        subtitle={animal ? `${animal.identification} · ${animal.farmName}` : 'Cargando datos del animal...'}
        onClose={onClose}
      />
      <ModalBody>
        {requestError && <div className="error-banner">{requestError}</div>}
        {loading || !animal ? (
          <div className="empty-state">Cargando detalle del animal...</div>
        ) : (
          <>
            <div className="profile-grid">
              <div>
                <span>Explotación</span>
                <strong>{animal.farmName}</strong>
              </div>
              <div>
                <span>Especie</span>
                <strong>{speciesToneMap[animal.livestockSpecies]?.label ?? animal.livestockSpecies}</strong>
              </div>
            </div>

            <div className="grid-form">
              <label className="farm-form-field">
                <ModalFieldLabel>Identificación / crotal</ModalFieldLabel>
                <input className={errors.identification ? 'farm-input farm-input-error' : 'farm-input'} value={form.identification} onChange={(event) => onChange('identification', event.target.value)} />
                {errors.identification && <p className="farm-field-error">{errors.identification}</p>}
              </label>
              <label className="farm-form-field">
                <ModalFieldLabel>Raza</ModalFieldLabel>
                <input value={form.breed} onChange={(event) => onChange('breed', event.target.value)} />
              </label>
              <label className="farm-form-field">
                <ModalFieldLabel>Sexo</ModalFieldLabel>
                <div className="select-wrapper">
                  <select value={form.sex} onChange={(event) => onChange('sex', event.target.value)}>
                    <option value="">No informado</option>
                    <option value="Female">Hembra</option>
                    <option value="Male">Macho</option>
                  </select>
                  <ChevronDown size={16} />
                </div>
              </label>
              {animal.livestockSpecies === 'Porcine' ? (
                <label className="farm-form-field">
                  <ModalFieldLabel>Año nacimiento</ModalFieldLabel>
                  <input className={errors.birthYear ? 'farm-input farm-input-error' : 'farm-input'} type="number" min="1900" max="2100" value={form.birthYear} onChange={(event) => onChange('birthYear', event.target.value)} />
                  {errors.birthYear && <p className="farm-field-error">{errors.birthYear}</p>}
                </label>
              ) : (
                <label className="farm-form-field">
                  <ModalFieldLabel>Fecha de nacimiento</ModalFieldLabel>
                  <input type="date" value={form.birthDate} onChange={(event) => onChange('birthDate', event.target.value)} />
                </label>
              )}
              <label className="farm-form-field">
                <ModalFieldLabel>Fecha alta</ModalFieldLabel>
                <input type="date" value={form.registrationDate} onChange={(event) => onChange('registrationDate', event.target.value)} />
              </label>
              <label className="farm-form-field">
                <ModalFieldLabel>Causa alta</ModalFieldLabel>
                <div className="select-wrapper">
                  <select value={form.registrationCause} onChange={(event) => onChange('registrationCause', event.target.value)}>
                    <option value="">No informada</option>
                    <option value="Entrada">Entrada (E)</option>
                    <option value="Autorreposicion">Autorreposición (A)</option>
                  </select>
                  <ChevronDown size={16} />
                </div>
              </label>
              <label className="farm-form-field">
                <ModalFieldLabel>Procedencia</ModalFieldLabel>
                <input className={errors.originCode ? 'farm-input farm-input-error' : 'farm-input'} value={form.originCode} onChange={(event) => onChange('originCode', event.target.value)} />
                {errors.originCode && <p className="farm-field-error">{errors.originCode}</p>}
              </label>
              <label className="farm-form-field form-full">
                <ModalFieldLabel>Serie guía entrada / salida</ModalFieldLabel>
                <input
                  value={formatAnimalGuideSeries(animal.entryGuideSerie, animal.exitGuideSerie)}
                  disabled
                />
              </label>
            </div>

            {animal.ovinoCaprino && (
              <div className="animal-specific-block">
                <h3>Datos ovino/caprino</h3>
                <div className="grid-form">
                  <label className="farm-form-field">
                    <ModalFieldLabel>Genotipado</ModalFieldLabel>
                    <input value={form.genotyping} onChange={(event) => onChange('genotyping', event.target.value)} />
                  </label>
                  <label className="farm-form-field">
                    <ModalFieldLabel>Alelo dominante</ModalFieldLabel>
                    <input value={form.dominantAllele} onChange={(event) => onChange('dominantAllele', event.target.value)} />
                  </label>
                  <label className="farm-form-field">
                    <ModalFieldLabel>Alelo bajo</ModalFieldLabel>
                    <input value={form.lowAllele} onChange={(event) => onChange('lowAllele', event.target.value)} />
                  </label>
                  <label className="farm-form-field">
                    <ModalFieldLabel>Fecha de identificación</ModalFieldLabel>
                    <input type="date" value={form.ovinoIdentificationDate} onChange={(event) => onChange('ovinoIdentificationDate', event.target.value)} />
                  </label>
                </div>
              </div>
            )}

            {animal.porcino && (
              <div className="animal-specific-block">
                <h3>Datos porcino</h3>
                <div className="grid-form">
                  <label className="farm-form-field">
                    <ModalFieldLabel>Tipo de animal</ModalFieldLabel>
                    <input className={errors.animalType ? 'farm-input farm-input-error' : 'farm-input'} value={form.animalType} onChange={(event) => onChange('animalType', event.target.value)} />
                    {errors.animalType && <p className="farm-field-error">{errors.animalType}</p>}
                  </label>
                  <label className="farm-form-field">
                    <ModalFieldLabel>Fecha identificación</ModalFieldLabel>
                    <input type="date" value={form.identificationDate} onChange={(event) => onChange('identificationDate', event.target.value)} />
                  </label>
                  <label className="farm-form-field">
                    <ModalFieldLabel>Nº registro porcino</ModalFieldLabel>
                    <input value={form.pigRegistrationNumber} onChange={(event) => onChange('pigRegistrationNumber', event.target.value)} />
                  </label>
                  <label className="farm-form-field">
                    <ModalFieldLabel>Marca / crotal</ModalFieldLabel>
                    <input value={form.tag} onChange={(event) => onChange('tag', event.target.value)} />
                  </label>
                </div>
              </div>
            )}

              <div className="animal-specific-block">
                <h3>Histórico de baja</h3>
                <div className="grid-form">
                  <label className="farm-form-field">
                    <ModalFieldLabel>Causa de baja</ModalFieldLabel>
                    <input value={formatAnimalCause(animal.dischargeCause)} disabled />
                  </label>
                  <label className="farm-form-field">
                    <ModalFieldLabel>Fecha de baja</ModalFieldLabel>
                    <input value={formatDate(animal.dischargeDate)} disabled />
                  </label>
                  <label className="farm-form-field">
                    <ModalFieldLabel>Destino</ModalFieldLabel>
                    <input value={animal.destinationCode ?? 'No informado'} disabled />
                  </label>
                </div>
              </div>
          </>
        )}
      </ModalBody>

      <ModalFooter>
        <button className="danger-button" type="button" onClick={onDelete} disabled={!animal || loading || saving || deleting}>
          <Trash2 size={15} />
          {deleting ? 'Eliminando...' : 'Eliminar registro'}
        </button>
        <div className="animal-modal-actions">
          <button className="secondary-button" type="button" onClick={onClose}>Cerrar</button>
          <button className="primary-button" type="button" onClick={onSave} disabled={!animal || loading || saving || deleting}>
            {saving ? 'Guardando...' : 'Guardar cambios'}
          </button>
        </div>
      </ModalFooter>
    </ModalDialog>
  );
}
