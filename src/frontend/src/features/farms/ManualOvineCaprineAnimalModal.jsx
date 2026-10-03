import { ChevronDown, Tag } from 'lucide-react';
import { ModalBody, ModalDialog, ModalFieldLabel, ModalFooter, ModalHeader } from '../../shared/components/modal/Modal';

export function ManualOvineCaprineAnimalModal({
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
  const speciesLabel = farm.livestockSpecies === 'Caprine' ? 'caprino' : 'ovino';

  return (
    <ModalDialog cardAs="form" size="wide" onSubmit={onSubmit}>
      <ModalHeader
        icon={<Tag size={18} />}
        title={`Registrar ${speciesLabel} individual`}
        subtitle={`Alta manual de entrada de un animal ${speciesLabel} identificado dentro de ${farm.name}.`}
        onClose={onClose}
      />
      <ModalBody className="operation-modal-body">
        {requestError && <div className="error-banner">{requestError}</div>}

        <div className="grid-form">
          <label className="farm-form-field">
            <span className="farm-field-label">Identificación individual <span className="farm-field-label-required">*</span></span>
            <input className={errors.identification ? 'farm-input farm-input-error' : 'farm-input'} value={form.identification} onChange={(event) => onChange('identification', event.target.value)} placeholder="ES100003542349" required />
            {errors.identification && <p className="farm-field-error">{errors.identification}</p>}
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
            <span className="farm-field-label">Fecha de nacimiento</span>
            <input type="date" value={form.birthDate} onChange={(event) => onChange('birthDate', event.target.value)} />
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Fecha de alta</span>
            <input type="date" value={form.registrationDate} onChange={(event) => onChange('registrationDate', event.target.value)} />
          </label>
          <label className="farm-form-field">
            <span className="farm-field-label">Causa de alta</span>
            <input value="Entrada (E)" disabled />
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
        </div>

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
