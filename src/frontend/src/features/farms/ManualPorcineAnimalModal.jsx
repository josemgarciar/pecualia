import { porcineAnimalTypeOptions } from './farmOptions.js';
import { ChevronDown, Tag } from 'lucide-react';
import { ModalBody, ModalDialog, ModalFooter, ModalHeader } from '../../shared/components/modal/Modal';

export function ManualPorcineAnimalModal({
  farm,
  form,
  errors,
  requestError,
  submitting,
  breedOptions,
  loadingBreedOptions,
  onChange,
  onClose,
  onSubmit
}) {
  return (
    <ModalDialog cardAs="form" size="wide" onSubmit={onSubmit}>
      <ModalHeader
        icon={<Tag size={18} />}
        title="Registrar porcino individual"
        subtitle={`Alta manual excepcional de un animal porcino identificado dentro de ${farm.name}.`}
        onClose={onClose}
      />
      <ModalBody className="operation-modal-body">
        {requestError && <div className="error-banner">{requestError}</div>}

        <div className="grid-form">
          <label className="farm-form-field">
            <span className="farm-field-label">Identificación individual <span className="farm-field-label-required">*</span></span>
            <input className={errors.identification ? 'farm-input farm-input-error' : 'farm-input'} value={form.identification} onChange={(event) => onChange('identification', event.target.value)} placeholder="GT1800001004" required />
            {errors.identification && <p className="farm-field-error">{errors.identification}</p>}
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Tipo de animal <span className="farm-field-label-required">*</span></span>
            <div className="select-wrapper">
              <select className={errors.animalType ? 'farm-input farm-input-error' : 'farm-input'} value={form.animalType} onChange={(event) => onChange('animalType', event.target.value)} required>
                <option value="">Selecciona un tipo</option>
                {porcineAnimalTypeOptions.map((option) => (
                  <option key={option} value={option}>{option}</option>
                ))}
              </select>
              <ChevronDown size={16} />
            </div>
            {errors.animalType && <p className="farm-field-error">{errors.animalType}</p>}
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Raza</span>
            <div className="select-wrapper">
              <select value={form.breed} onChange={(event) => onChange('breed', event.target.value)} disabled={loadingBreedOptions}>
                <option value="">{loadingBreedOptions ? 'Cargando razas...' : 'Selecciona una raza'}</option>
                {breedOptions.map((option) => (
                  <option key={option.name} value={option.name}>
                    {option.name} ({option.code})
                  </option>
                ))}
              </select>
              <ChevronDown size={16} />
            </div>
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Sexo</span>
            <div className="select-wrapper">
              <select value={form.sex} onChange={(event) => onChange('sex', event.target.value)}>
                <option value="">No informado</option>
                <option value="Female">Hembra</option>
                <option value="Male">Macho</option>
              </select>
              <ChevronDown size={16} />
            </div>
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Año de nacimiento</span>
            <input className={errors.birthYear ? 'farm-input farm-input-error' : 'farm-input'} type="number" min="1900" max="2100" value={form.birthYear} onChange={(event) => onChange('birthYear', event.target.value)} placeholder="2026" />
            {errors.birthYear && <p className="farm-field-error">{errors.birthYear}</p>}
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Fecha de alta</span>
            <input type="date" value={form.registrationDate} onChange={(event) => onChange('registrationDate', event.target.value)} />
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Causa de alta</span>
            <div className="select-wrapper">
              <select value={form.registrationCause} onChange={(event) => onChange('registrationCause', event.target.value)}>
                <option value="Entrada">Entrada (E)</option>
                <option value="Autorreposicion">Autorreposición (A)</option>
              </select>
              <ChevronDown size={16} />
            </div>
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Código REGA de origen</span>
            <input className={errors.originCode ? 'farm-input farm-input-error' : 'farm-input'} value={form.originCode} onChange={(event) => onChange('originCode', event.target.value)} placeholder="ES061230000145" />
            {errors.originCode && <p className="farm-field-error">{errors.originCode}</p>}
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Fecha de identificación</span>
            <input type="date" value={form.identificationDate} onChange={(event) => onChange('identificationDate', event.target.value)} />
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Nº registro porcino</span>
            <input value={form.pigRegistrationNumber} onChange={(event) => onChange('pigRegistrationNumber', event.target.value)} />
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Marca / crotal</span>
            <input value={form.tag} onChange={(event) => onChange('tag', event.target.value)} />
          </label>
        </div>
      </ModalBody>

      <ModalFooter align="end">
        <button className="secondary-button" type="button" onClick={onClose}>Cancelar</button>
        <button className="primary-button" type="submit" disabled={submitting}>
          {submitting ? 'Guardando...' : 'Registrar animal'}
        </button>
      </ModalFooter>
    </ModalDialog>
  );
}
