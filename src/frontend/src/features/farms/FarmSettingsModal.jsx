import { speciesToneMap, regimeOptions, provinceOptions } from './farmOptions.js';
import { useState } from 'react';
import { Edit3, Trash2, TriangleAlert } from 'lucide-react';
import { ModalBody, ModalDialog, ModalFooter, ModalHeader } from '../../shared/components/modal/Modal';
import { FarmAnimalImportPanel } from './FarmAnimalImportPanel';

export function FarmSettingsModal({ farm, form, errors, requestError, submitting, deleting, onChange, onClose, onSubmit, onDelete, onAnimalsImported }) {
  const supportsAnimalImport = farm.livestockSpecies === 'Ovine' || farm.livestockSpecies === 'Caprine';
  const [activeSettingsTab, setActiveSettingsTab] = useState('data');
  const [importDocument, setImportDocument] = useState(null);
  const [deleteConfirmationOpen, setDeleteConfirmationOpen] = useState(false);
  const [deleteConfirmation, setDeleteConfirmation] = useState('');
  const deletionConfirmed = deleteConfirmation.trim() === farm.regaCode;

  return (
    <ModalDialog size="wide">
      <ModalHeader
        icon={<Edit3 size={18} />}
        title="Ajustes de la explotación"
        subtitle={`Edita los datos administrativos y operativos de ${farm.name}.`}
        onClose={onClose}
        closeDisabled={submitting || deleting}
      />
      {supportsAnimalImport && (
        <div className="farm-settings-tabs" role="tablist" aria-label="Ajustes de la explotación">
          <button
            className={activeSettingsTab === 'data' ? 'farm-settings-tab farm-settings-tab-active' : 'farm-settings-tab'}
            type="button"
            role="tab"
            aria-selected={activeSettingsTab === 'data'}
            onClick={() => setActiveSettingsTab('data')}
          >
            Datos
          </button>
          <button
            className={activeSettingsTab === 'animals' ? 'farm-settings-tab farm-settings-tab-active' : 'farm-settings-tab'}
            type="button"
            role="tab"
            aria-selected={activeSettingsTab === 'animals'}
            onClick={() => setActiveSettingsTab('animals')}
            data-testid="farm-settings-import-tab"
          >
            Importar animales
          </button>
        </div>
      )}
      <ModalBody className="farm-settings-body">
        {activeSettingsTab === 'data' ? (
          <>
            {requestError && <div className="error-banner">{requestError}</div>}

            <div className="farm-settings-grid">
          <div className="farm-form-field">
            <span className="farm-field-label">NOMBRE DE LA EXPLOTACIÓN <span className="farm-field-label-required">*</span></span>
            <input name="farmName" className={errors.name ? 'farm-input farm-input-error' : 'farm-input'} value={form.name} onChange={(event) => onChange('name', event.target.value)} />
            {errors.name && <p className="farm-field-error">{errors.name}</p>}
          </div>

          <div className="farm-form-field">
            <span className="farm-field-label">CÓDIGO REGA <span className="farm-field-label-required">*</span></span>
            <input name="farmRegaCode" className={errors.regaCode ? 'farm-input farm-input-error' : 'farm-input'} value={form.regaCode} onChange={(event) => onChange('regaCode', event.target.value)} />
            {errors.regaCode && <p className="farm-field-error">{errors.regaCode}</p>}
          </div>

          <div className="farm-form-field">
            <span className="farm-field-label">ESPECIE</span>
            <input name="farmSpecies" className="farm-input" value={speciesToneMap[farm.livestockSpecies]?.label ?? farm.livestockSpecies} disabled />
          </div>

          <div className="farm-form-field">
            <span className="farm-field-label">RÉGIMEN <span className="farm-field-label-required">*</span></span>
            <div className="select-wrapper">
              <select name="farmRegime" className={errors.regime ? 'farm-input farm-input-error' : 'farm-input'} value={form.regime} onChange={(event) => onChange('regime', event.target.value)}>
                <option value="">Selecciona régimen</option>
                {regimeOptions.map((option) => (
                  <option key={option.value} value={option.value}>{option.label}</option>
                ))}
              </select>
            </div>
            {errors.regime && <p className="farm-field-error">{errors.regime}</p>}
          </div>

          {farm.livestockSpecies === 'Porcine' && (
            <>
              <div className="farm-form-field">
                <span className="farm-field-label">Nº REGISTRO PORCINO <span className="farm-field-label-required">*</span></span>
                <input name="farmPorcineRegistryNumber" className={errors.porcineRegistryNumber ? 'farm-input farm-input-error' : 'farm-input'} value={form.porcineRegistryNumber} onChange={(event) => onChange('porcineRegistryNumber', event.target.value)} />
                {errors.porcineRegistryNumber && <p className="farm-field-error">{errors.porcineRegistryNumber}</p>}
              </div>
              <div className="farm-form-field">
                <span className="farm-field-label">CAPACIDAD MÁXIMA MADRES</span>
                <input name="farmPorcineMothersCapacity" type="number" min="0" className={errors.porcineMothersCapacity ? 'farm-input farm-input-error' : 'farm-input'} value={form.porcineMothersCapacity} onChange={(event) => onChange('porcineMothersCapacity', event.target.value)} />
                {errors.porcineMothersCapacity && <p className="farm-field-error">{errors.porcineMothersCapacity}</p>}
              </div>

              <div className="farm-form-field">
                <span className="farm-field-label">CAPACIDAD MÁXIMA CEBO</span>
                <input name="farmPorcineFatteningCapacity" type="number" min="0" className={errors.porcineFatteningCapacity ? 'farm-input farm-input-error' : 'farm-input'} value={form.porcineFatteningCapacity} onChange={(event) => onChange('porcineFatteningCapacity', event.target.value)} />
                {errors.porcineFatteningCapacity && <p className="farm-field-error">{errors.porcineFatteningCapacity}</p>}
              </div>
            </>
          )}

          <div className="farm-form-field">
            <span className="farm-field-label">RESPONSABLE</span>
            <input name="farmResponsible" className="farm-input" value={form.responsible} onChange={(event) => onChange('responsible', event.target.value)} />
          </div>

          <div className="farm-form-field">
            <span className="farm-field-label">TIPO DE EXPLOTACIÓN</span>
            <input name="farmLivestockType" className="farm-input" value={form.livestockType} onChange={(event) => onChange('livestockType', event.target.value)} />
          </div>

          <div className="farm-form-field">
            <span className="farm-field-label">CLASIFICACIÓN ZOOTÉCNICA</span>
            <input name="farmZootechnicClassification" className="farm-input" value={form.zootechnicClassification} onChange={(event) => onChange('zootechnicClassification', event.target.value)} />
          </div>

          <div className="farm-form-field farm-settings-grid-full">
            <span className="farm-field-label">DIRECCIÓN / PARAJE</span>
            <input name="farmAddress" className="farm-input" value={form.address} onChange={(event) => onChange('address', event.target.value)} />
          </div>

          <div className="farm-form-field">
            <span className="farm-field-label">LOCALIDAD <span className="farm-field-label-required">*</span></span>
            <input name="farmTown" className={errors.town ? 'farm-input farm-input-error' : 'farm-input'} value={form.town} onChange={(event) => onChange('town', event.target.value)} />
            {errors.town && <p className="farm-field-error">{errors.town}</p>}
          </div>

          <div className="farm-form-field">
            <span className="farm-field-label">PROVINCIA <span className="farm-field-label-required">*</span></span>
            <div className="select-wrapper">
              <select name="farmProvince" className={errors.province ? 'farm-input farm-input-error' : 'farm-input'} value={form.province} onChange={(event) => onChange('province', event.target.value)}>
                <option value="">Selecciona una provincia</option>
                {provinceOptions.map((province) => (
                  <option key={province} value={province}>{province}</option>
                ))}
              </select>
            </div>
            {errors.province && <p className="farm-field-error">{errors.province}</p>}
          </div>

          <div className="farm-form-field">
            <span className="farm-field-label">CÓDIGO POSTAL</span>
            <input name="farmZipCode" className="farm-input" value={form.zipCode} onChange={(event) => onChange('zipCode', event.target.value)} />
          </div>

          <div className="farm-form-field">
            <span className="farm-field-label">HUSO</span>
            <input name="farmSpindle" type="number" min="1" className={errors.spindle ? 'farm-input farm-input-error' : 'farm-input'} value={form.spindle} onChange={(event) => onChange('spindle', event.target.value)} />
            {errors.spindle && <p className="farm-field-error">{errors.spindle}</p>}
          </div>

          <div className="farm-form-field">
            <span className="farm-field-label">COORDENADA X</span>
            <input name="farmXCoordinate" className={errors.xCoordinate ? 'farm-input farm-input-error' : 'farm-input'} value={form.xCoordinate} onChange={(event) => onChange('xCoordinate', event.target.value)} />
            {errors.xCoordinate && <p className="farm-field-error">{errors.xCoordinate}</p>}
          </div>

          <div className="farm-form-field">
            <span className="farm-field-label">COORDENADA Y</span>
            <input name="farmYCoordinate" className={errors.yCoordinate ? 'farm-input farm-input-error' : 'farm-input'} value={form.yCoordinate} onChange={(event) => onChange('yCoordinate', event.target.value)} />
            {errors.yCoordinate && <p className="farm-field-error">{errors.yCoordinate}</p>}
          </div>
            </div>

            <section className="farm-settings-danger-zone" aria-labelledby="farm-delete-title">
              <div>
                <h3 id="farm-delete-title">Eliminar explotación</h3>
                <p>Elimina permanentemente esta explotación, sus animales y sus registros propios.</p>
              </div>
              {!deleteConfirmationOpen ? (
                <button
                  className="danger-button"
                  type="button"
                  onClick={() => setDeleteConfirmationOpen(true)}
                  disabled={submitting || deleting}
                  data-testid="farm-delete-open"
                >
                  <Trash2 size={15} />
                  Eliminar explotación
                </button>
              ) : (
                <div className="farm-delete-confirmation">
                  <div className="farm-delete-warning">
                    <TriangleAlert size={20} aria-hidden="true" />
                    <p>
                      Esta acción no se puede deshacer. Las guías compartidas con otra explotación se conservarán
                      como histórico, pero se eliminarán todos los animales de <strong>{farm.name}</strong>.
                    </p>
                  </div>
                  <label className="farm-form-field" htmlFor="farm-delete-confirmation">
                    <span className="farm-field-label">
                      Escribe <strong>{farm.regaCode}</strong> para confirmar
                    </span>
                    <input
                      id="farm-delete-confirmation"
                      className="farm-input"
                      value={deleteConfirmation}
                      onChange={(event) => setDeleteConfirmation(event.target.value)}
                      autoComplete="off"
                      disabled={deleting}
                      data-testid="farm-delete-confirmation"
                    />
                  </label>
                </div>
              )}
            </section>
          </>
        ) : (
          <div className="stack">
            <div className="farm-import-intro">
              <h3>Importar animales en {farm.name}</h3>
              <p>
                Solo se incorporarán animales cuyo REGA de pertenencia y ubicación coincida con {farm.regaCode}.
                Las filas con errores se mostrarán y quedarán fuera de la importación.
              </p>
            </div>
            <FarmAnimalImportPanel
              species={farm.livestockSpecies}
              regaCode={farm.regaCode}
              farmId={farm.id}
              document={importDocument}
              onDocumentChange={setImportDocument}
              onImported={onAnimalsImported}
            />
          </div>
        )}
      </ModalBody>

      {activeSettingsTab === 'data' ? (
        <ModalFooter align="end">
          {deleteConfirmationOpen ? (
            <>
              <button
                className="secondary-button"
                type="button"
                onClick={() => {
                  setDeleteConfirmationOpen(false);
                  setDeleteConfirmation('');
                }}
                disabled={deleting}
              >
                Cancelar eliminación
              </button>
              <button
                className="danger-button farm-delete-confirm-button"
                type="button"
                onClick={onDelete}
                disabled={!deletionConfirmed || deleting}
                data-testid="farm-delete-confirm"
              >
                <Trash2 size={15} />
                {deleting ? 'Eliminando...' : 'Eliminar definitivamente'}
              </button>
            </>
          ) : (
            <>
              <button className="secondary-button" type="button" onClick={onClose} disabled={submitting}>Cancelar</button>
              <button className="primary-button" type="button" onClick={onSubmit} disabled={submitting}>
                {submitting ? 'Guardando...' : 'Guardar cambios'}
              </button>
            </>
          )}
        </ModalFooter>
      ) : (
        <ModalFooter align="end">
          <button className="secondary-button" type="button" onClick={onClose}>Cerrar</button>
        </ModalFooter>
      )}
    </ModalDialog>
  );
}
