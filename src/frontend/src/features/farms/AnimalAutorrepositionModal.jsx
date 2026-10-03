import { buildConsecutiveIdentificationPreview } from './farmForms.js';
import { ChevronDown, Tag } from 'lucide-react';
import { ModalBody, ModalDialog, ModalFieldLabel, ModalFooter, ModalHeader } from '../../shared/components/modal/Modal';

export function AnimalAutorrepositionModal({
  farm,
  form,
  errors,
  requestError,
  submitting,
  availability,
  loadingBirths,
  breedOptions,
  loadingBreedOptions,
  onChange,
  onClose,
  onSubmit
}) {
  const totalAvailable = availability?.availableAnimals ?? 0;
  const totalEligible = availability?.eligibleAnimals ?? 0;
  let rangePreview = null;
  let rangePreviewError = '';

  try {
    rangePreview = buildConsecutiveIdentificationPreview(form.startIdentification, form.quantity);
  } catch (error) {
    rangePreviewError = error.message;
  }

  return (
    <ModalDialog cardAs="form" size="wide" onSubmit={onSubmit}>
      <ModalHeader
        icon={<Tag size={18} />}
        title="Autorreposición"
        subtitle={`Convierte animales no reproductores sin identificar en reproductores identificados dentro de ${farm.name}.`}
        onClose={onClose}
      />
      <ModalBody className="operation-modal-body">
        {requestError && <div className="error-banner">{requestError}</div>}
        <div className="grid-form">
          <label className="farm-form-field">
            <span className="farm-field-label">Identificación inicial <span className="farm-field-label-required">*</span></span>
            <input className={errors.startIdentification ? 'farm-input farm-input-error' : 'farm-input'} value={form.startIdentification} onChange={(event) => onChange('startIdentification', event.target.value)} placeholder="ES100003542349" required />
            {errors.startIdentification && <p className="farm-field-error">{errors.startIdentification}</p>}
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Número de animales <span className="farm-field-label-required">*</span></span>
            <input className={errors.quantity ? 'farm-input farm-input-error' : 'farm-input'} type="number" min="1" step="1" value={form.quantity} onChange={(event) => onChange('quantity', event.target.value)} required />
            {errors.quantity && <p className="farm-field-error">{errors.quantity}</p>}
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Raza <span className="farm-field-label-required">*</span></span>
            <div className="select-wrapper">
              <select className={errors.breed ? 'farm-input farm-input-error' : 'farm-input'} value={form.breed} onChange={(event) => onChange('breed', event.target.value)} disabled={loadingBreedOptions} required>
                <option value="">{loadingBreedOptions ? 'Cargando razas...' : 'Selecciona una raza'}</option>
                {breedOptions.map((option) => (
                  <option key={option.name} value={option.name}>
                    {option.name} ({option.code})
                  </option>
                ))}
              </select>
              <ChevronDown size={16} />
            </div>
            {errors.breed && <p className="farm-field-error">{errors.breed}</p>}
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Sexo <span className="farm-field-label-required">*</span></span>
            <div className="select-wrapper">
              <select className={errors.sex ? 'farm-input farm-input-error' : 'farm-input'} value={form.sex} onChange={(event) => onChange('sex', event.target.value)} required>
                <option value="">Selecciona sexo</option>
                <option value="Female">Hembra</option>
                <option value="Male">Macho</option>
              </select>
              <ChevronDown size={16} />
            </div>
            {errors.sex && <p className="farm-field-error">{errors.sex}</p>}
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Fecha alta <span className="farm-field-label-required">*</span></span>
            <input className={errors.registrationDate ? 'farm-input farm-input-error' : 'farm-input'} type="date" value={form.registrationDate} onChange={(event) => onChange('registrationDate', event.target.value)} required />
            {errors.registrationDate && <p className="farm-field-error">{errors.registrationDate}</p>}
          </label>
        </div>

        <div className={totalAvailable > 0 ? 'farm-settings-note' : 'info-callout info-callout-danger'}>
          {loadingBirths ? (
            <p>Cargando disponibilidad para autoreposición...</p>
          ) : totalAvailable > 0 ? (
            <>
              <strong>Disponibilidad agregada del censo</strong>
              <p>
                {totalAvailable} animales no identificados disponibles · {totalEligible} con más de 4 meses y aptos para autoreposición
              </p>
            </>
          ) : (
            <p>No hay animales no identificados disponibles para autoreposición.</p>
          )}
        </div>

        <div className={rangePreview ? 'farm-settings-note' : 'info-callout info-callout-danger'}>
          {rangePreview ? (
            <>
              <strong>Rango generado</strong>
              <p>{rangePreview.count} animales: {rangePreview.firstIdentification} a {rangePreview.lastIdentification}</p>
            </>
          ) : (
            <p>{rangePreviewError || 'Introduce una identificación inicial y el número de animales para previsualizar la serie.'}</p>
          )}
        </div>

        {farm.livestockSpecies === 'Porcine' ? (
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
                <ModalFieldLabel>Marca / tag</ModalFieldLabel>
                <input value={form.tag} onChange={(event) => onChange('tag', event.target.value)} />
              </label>
            </div>
          </div>
        ) : (
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
            </div>
          </div>
        )}
      </ModalBody>

      <ModalFooter align="end">
        <button className="secondary-button" type="button" onClick={onClose}>Cancelar</button>
        <button className="primary-button" type="submit" disabled={submitting}>
          {submitting ? 'Registrando...' : 'Crear animales'}
        </button>
      </ModalFooter>
    </ModalDialog>
  );
}
