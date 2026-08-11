import { useEffect, useMemo, useState } from 'react';
import { CheckCircle2, Eye, PencilLine } from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import {
  ModalBody,
  ModalDialog,
  ModalFieldLabel,
  ModalFooter,
  ModalHeader,
  ModalStepper
} from '../../shared/components/modal/Modal';

const FIELD_MODES = [
  { value: 'Unchanged', label: 'Sin cambios' },
  { value: 'Set', label: 'Establecer' },
  { value: 'Clear', label: 'Borrar' }
];

const GUIDE_ACTIONS = [
  { value: 'Unchanged', label: 'Sin cambios' },
  { value: 'SetEntry', label: 'Crear o reutilizar guía de entrada' },
  { value: 'SetExit', label: 'Crear o reutilizar guía de salida' },
  { value: 'ClearLatestEntry', label: 'Desvincular última guía de entrada' },
  { value: 'ClearLatestExit', label: 'Desvincular última guía de salida' }
];

const REGA_PATTERN = /^ES\d{12}$/;

function nowLocal() {
  const now = new Date();
  now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
  return now.toISOString().slice(0, 16);
}

function toIso(value) {
  return value ? new Date(value).toISOString() : null;
}

function initialForm() {
  const now = nowLocal();
  return {
    registrationCauseMode: 'Unchanged',
    registrationCause: 'Entrada',
    registrationDateMode: 'Unchanged',
    registrationDate: now.slice(0, 10),
    dischargeCauseMode: 'Unchanged',
    dischargeCause: 'Salida',
    dischargeDateMode: 'Unchanged',
    dischargeDate: now.slice(0, 10),
    guideAction: 'Unchanged',
    counterpartyType: 'External',
    counterpartyFarmId: '',
    counterpartyExternalCode: '',
    counterpartyExternalName: '',
    codRemo: '',
    serie: '',
    departureDate: now,
    arrivalDate: now,
    solicitationDate: now,
    meansOfTransport: '',
    transportName: '',
    vehicleRegistrationNumber: ''
  };
}

function buildChanges(form) {
  return {
    registrationCause: {
      mode: form.registrationCauseMode,
      value: form.registrationCauseMode === 'Set' ? form.registrationCause : null
    },
    dischargeCause: {
      mode: form.dischargeCauseMode,
      value: form.dischargeCauseMode === 'Set' ? form.dischargeCause : null
    },
    registrationDate: {
      mode: form.registrationDateMode,
      value: form.registrationDateMode === 'Set' ? (form.registrationDate || null) : null
    },
    dischargeDate: {
      mode: form.dischargeDateMode,
      value: form.dischargeDateMode === 'Set' ? (form.dischargeDate || null) : null
    },
    guide: {
      action: form.guideAction,
      counterpartyType: form.guideAction.startsWith('Set') ? form.counterpartyType : null,
      counterpartyFarmId: form.counterpartyType === 'Internal' && form.counterpartyFarmId
        ? Number(form.counterpartyFarmId)
        : null,
      counterpartyExternalCode: form.counterpartyType === 'External' ? form.counterpartyExternalCode.trim() || null : null,
      counterpartyExternalName: form.counterpartyType === 'External' ? form.counterpartyExternalName.trim() || null : null,
      codRemo: form.codRemo.trim() || null,
      serie: form.serie.trim() || null,
      departureDate: toIso(form.departureDate),
      arrivalDate: toIso(form.arrivalDate),
      solicitationDate: toIso(form.solicitationDate),
      meansOfTransport: form.meansOfTransport.trim() || null,
      transportName: form.transportName.trim() || null,
      vehicleRegistrationNumber: form.vehicleRegistrationNumber.trim() || null
    }
  };
}

function validateForm(form) {
  const fieldChecks = [
    [form.registrationCauseMode, form.registrationCause, 'causa de alta'],
    [form.registrationDateMode, form.registrationDate, 'fecha de alta'],
    [form.dischargeCauseMode, form.dischargeCause, 'causa de baja'],
    [form.dischargeDateMode, form.dischargeDate, 'fecha de baja']
  ];
  const missingField = fieldChecks.find(([mode, value]) => mode === 'Set' && !value);
  if (missingField) {
    return `Indica un valor para la ${missingField[2]}.`;
  }

  const hasFieldChange = fieldChecks.some(([mode]) => mode !== 'Unchanged');
  if (!hasFieldChange && form.guideAction === 'Unchanged') {
    return 'Selecciona al menos un dato para modificar.';
  }

  if (!form.guideAction.startsWith('Set')) {
    return '';
  }

  if (form.counterpartyType === 'Internal' && !form.counterpartyFarmId) {
    return 'Selecciona la explotación contraparte.';
  }
  if (form.counterpartyType === 'External' && !form.counterpartyExternalName.trim()) {
    return 'Indica el nombre de la contraparte externa.';
  }
  if (form.counterpartyType === 'External' && !REGA_PATTERN.test(form.counterpartyExternalCode.trim().toUpperCase())) {
    return 'El código REGA externo debe tener formato ES seguido de 12 dígitos.';
  }
  if (!form.codRemo.trim() || !form.serie.trim()) {
    return 'El REMO y la serie de la guía son obligatorios.';
  }
  if (!form.departureDate || !form.arrivalDate) {
    return 'Las fechas de salida y llegada de la guía son obligatorias.';
  }

  const departure = new Date(form.departureDate);
  const arrival = new Date(form.arrivalDate);
  const solicitation = form.solicitationDate ? new Date(form.solicitationDate) : null;
  if (Number.isNaN(departure.getTime()) || Number.isNaN(arrival.getTime())) {
    return 'Las fechas de la guía no son válidas.';
  }
  if (arrival < departure) {
    return 'La llegada de la guía no puede ser anterior a la salida.';
  }
  if (solicitation && (Number.isNaN(solicitation.getTime()) || solicitation > departure)) {
    return 'La solicitud de la guía no puede ser posterior a la salida.';
  }

  return '';
}

function requestErrorMessage(error) {
  if (error instanceof TypeError) {
    return 'No se pudo conectar con el servidor. Comprueba la conexión y vuelve a intentarlo.';
  }
  return error?.message || 'La operación no se pudo completar.';
}

function isPreviewResponse(response) {
  return response &&
    Array.isArray(response.resolvedAnimalIds) &&
    Array.isArray(response.rows) &&
    typeof response.stateFingerprint === 'string' &&
    typeof response.totalAnimals === 'number' &&
    typeof response.conflictAnimals === 'number' &&
    response.guide;
}

function isCommitResponse(response) {
  return response &&
    typeof response.operationId === 'string' &&
    typeof response.updatedAnimals === 'number' &&
    typeof response.linkedAnimals === 'number' &&
    typeof response.unlinkedAnimals === 'number';
}

function ChangeField({ label, mode, value, type = 'text', options, onMode, onValue }) {
  const fieldName = label.toLowerCase().replaceAll(' ', '-');
  return (
    <div className="bulk-change-field">
      <label>
        <ModalFieldLabel>{label}</ModalFieldLabel>
        <select name={`${fieldName}-mode`} value={mode} onChange={(event) => onMode(event.target.value)}>
          {FIELD_MODES.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
        </select>
      </label>
      {mode === 'Set' && (
        <label>
          <ModalFieldLabel>Nuevo valor</ModalFieldLabel>
          {options ? (
            <select name={`${fieldName}-value`} value={value} onChange={(event) => onValue(event.target.value)}>
              {options.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
            </select>
          ) : (
            <input name={`${fieldName}-value`} type={type} value={value} onChange={(event) => onValue(event.target.value)} />
          )}
        </label>
      )}
    </div>
  );
}

export function FarmAnimalBulkUpdateModal({
  farm,
  selectedCount,
  selection,
  onClose,
  onCommitted
}) {
  const [form, setForm] = useState(initialForm);
  const [step, setStep] = useState(1);
  const [preview, setPreview] = useState(null);
  const [result, setResult] = useState(null);
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [farms, setFarms] = useState([]);
  const [previewPage, setPreviewPage] = useState(1);
  const operationId = useMemo(() => crypto.randomUUID(), []);
  const previewPageSize = 25;
  const previewRows = preview?.rows?.slice(
    (previewPage - 1) * previewPageSize,
    previewPage * previewPageSize
  ) ?? [];

  useEffect(() => {
    let cancelled = false;
    apiRequest('/api/farms/')
      .then((items) => {
        if (!cancelled) {
          setFarms(items.filter((item) => item.id !== farm.id && item.livestockSpecies === farm.livestockSpecies));
        }
      })
      .catch(() => {
        if (!cancelled) {
          setFarms([]);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [farm.id, farm.livestockSpecies]);

  function update(field, value) {
    setForm((current) => {
      const next = { ...current, [field]: value };
      if (field === 'guideAction' && value === 'SetEntry') {
        next.registrationCauseMode = 'Set';
        next.registrationCause = 'Entrada';
        next.registrationDateMode = 'Set';
        next.registrationDate = current.arrivalDate.slice(0, 10);
      }
      if (field === 'guideAction' && value === 'SetExit') {
        next.dischargeCauseMode = 'Set';
        next.dischargeCause = 'Salida';
        next.dischargeDateMode = 'Set';
        next.dischargeDate = current.departureDate.slice(0, 10);
      }
      if (field === 'arrivalDate' && current.guideAction === 'SetEntry') {
        next.registrationDate = value.slice(0, 10);
      }
      if (field === 'departureDate' && current.guideAction === 'SetExit') {
        next.dischargeDate = value.slice(0, 10);
      }
      return next;
    });
    setPreview(null);
    setStep(1);
    setError('');
  }

  async function requestPreview() {
    const validationError = validateForm(form);
    if (validationError) {
      setError(validationError);
      return;
    }

    setSubmitting(true);
    setError('');
    try {
      const response = await apiRequest(`/api/farms/${farm.id}/animals/bulk-update/preview`, {
        method: 'POST',
        body: {
          selection,
          changes: buildChanges(form)
        }
      });
      if (!isPreviewResponse(response)) {
        throw new Error('El servidor devolvió una previsualización no válida. Vuelve a intentarlo.');
      }
      setPreview(response);
      setPreviewPage(1);
      setStep(2);
    } catch (requestError) {
      setError(requestErrorMessage(requestError));
    } finally {
      setSubmitting(false);
    }
  }

  async function commit() {
    if (!preview || preview.conflictAnimals > 0) {
      return;
    }
    setSubmitting(true);
    setError('');
    try {
      const response = await apiRequest(`/api/farms/${farm.id}/animals/bulk-update/commit`, {
        method: 'POST',
        body: {
          operationId,
          animalIds: preview.resolvedAnimalIds,
          stateFingerprint: preview.stateFingerprint,
          changes: buildChanges(form)
        }
      });
      if (!isCommitResponse(response)) {
        throw new Error('El servidor devolvió un resultado no válido. Consulta el estado antes de reintentarlo.');
      }
      setResult(response);
      setStep(3);
      onCommitted(response);
    } catch (requestError) {
      setError(requestErrorMessage(requestError));
    } finally {
      setSubmitting(false);
    }
  }

  const guideFieldsVisible = form.guideAction === 'SetEntry' || form.guideAction === 'SetExit';
  const previewPages = Math.max(1, Math.ceil((preview?.rows?.length ?? 0) / previewPageSize));

  return (
    <ModalDialog size="wide" shellClassName="animal-bulk-modal">
      <ModalHeader
        icon={<PencilLine size={20} />}
        title="Modificación masiva"
        subtitle={`${selectedCount.toLocaleString('es-ES')} animales seleccionados`}
        onClose={onClose}
        closeDisabled={submitting}
      />
      <ModalStepper
        currentStep={step}
        steps={[
          { label: 'Configurar', icon: PencilLine },
          { label: 'Previsualizar', icon: Eye },
          { label: 'Resultado', icon: CheckCircle2 }
        ]}
      />
      <ModalBody className="operation-modal-body animal-bulk-body">
        {error && <div className="error-banner">{error}</div>}

        {step === 1 && (
          <>
            <section className="bulk-section">
              <div>
                <h3>Datos de alta y baja</h3>
                <p>Cada campo puede conservarse, establecerse o borrarse. Causa y fecha deben quedar completas.</p>
              </div>
              <div className="bulk-change-grid">
                <ChangeField
                  label="Causa de alta"
                  mode={form.registrationCauseMode}
                  value={form.registrationCause}
                  options={[
                    { value: 'Entrada', label: 'Entrada' },
                    { value: 'Autorreposicion', label: 'Autorreposición' }
                  ]}
                  onMode={(value) => update('registrationCauseMode', value)}
                  onValue={(value) => update('registrationCause', value)}
                />
                <ChangeField
                  label="Fecha de alta"
                  mode={form.registrationDateMode}
                  value={form.registrationDate}
                  type="date"
                  onMode={(value) => update('registrationDateMode', value)}
                  onValue={(value) => update('registrationDate', value)}
                />
                <ChangeField
                  label="Causa de baja"
                  mode={form.dischargeCauseMode}
                  value={form.dischargeCause}
                  options={[
                    { value: 'Salida', label: 'Salida' },
                    { value: 'Muerte', label: 'Muerte' }
                  ]}
                  onMode={(value) => update('dischargeCauseMode', value)}
                  onValue={(value) => update('dischargeCause', value)}
                />
                <ChangeField
                  label="Fecha de baja"
                  mode={form.dischargeDateMode}
                  value={form.dischargeDate}
                  type="date"
                  onMode={(value) => update('dischargeDateMode', value)}
                  onValue={(value) => update('dischargeDate', value)}
                />
              </div>
            </section>

            <section className="bulk-section">
              <div>
                <h3>Guía asociada</h3>
                <p>La corrección es histórica: no mueve animales entre explotaciones ni genera censos o balances.</p>
              </div>
              <label>
                <ModalFieldLabel>Acción sobre la guía</ModalFieldLabel>
                <select name="guide-action" value={form.guideAction} onChange={(event) => update('guideAction', event.target.value)}>
                  {GUIDE_ACTIONS.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
                </select>
              </label>

              {guideFieldsVisible && (
                <div className="bulk-guide-grid">
                  <label>
                    <ModalFieldLabel required>Tipo de contraparte</ModalFieldLabel>
                    <select name="counterparty-type" value={form.counterpartyType} onChange={(event) => update('counterpartyType', event.target.value)}>
                      <option value="External">Externa</option>
                      <option value="Internal">Otra explotación</option>
                    </select>
                  </label>
                  {form.counterpartyType === 'Internal' ? (
                    <label>
                      <ModalFieldLabel required>Explotación contraparte</ModalFieldLabel>
                      <select name="counterparty-farm" value={form.counterpartyFarmId} onChange={(event) => update('counterpartyFarmId', event.target.value)}>
                        <option value="">Seleccionar...</option>
                        {farms.map((item) => <option key={item.id} value={item.id}>{item.name} · {item.regaCode}</option>)}
                      </select>
                    </label>
                  ) : (
                    <>
                      <label>
                        <ModalFieldLabel required>Nombre externo</ModalFieldLabel>
                        <input name="counterparty-external-name" value={form.counterpartyExternalName} onChange={(event) => update('counterpartyExternalName', event.target.value)} />
                      </label>
                      <label>
                        <ModalFieldLabel required>REGA externo</ModalFieldLabel>
                        <input name="counterparty-external-rega" value={form.counterpartyExternalCode} onChange={(event) => update('counterpartyExternalCode', event.target.value.toUpperCase())} placeholder="ES000000000000" />
                      </label>
                    </>
                  )}
                  <label>
                    <ModalFieldLabel required>Código REMO</ModalFieldLabel>
                    <input name="guide-cod-remo" value={form.codRemo} onChange={(event) => update('codRemo', event.target.value.toUpperCase())} />
                  </label>
                  <label>
                    <ModalFieldLabel required>Serie</ModalFieldLabel>
                    <input name="guide-serie" value={form.serie} onChange={(event) => update('serie', event.target.value.toUpperCase())} />
                  </label>
                  <label>
                    <ModalFieldLabel required>Salida</ModalFieldLabel>
                    <input name="guide-departure-date" type="datetime-local" value={form.departureDate} onChange={(event) => update('departureDate', event.target.value)} />
                  </label>
                  <label>
                    <ModalFieldLabel required>Llegada</ModalFieldLabel>
                    <input name="guide-arrival-date" type="datetime-local" value={form.arrivalDate} onChange={(event) => update('arrivalDate', event.target.value)} />
                  </label>
                  <label>
                    <ModalFieldLabel>Solicitud</ModalFieldLabel>
                    <input name="guide-solicitation-date" type="datetime-local" value={form.solicitationDate} onChange={(event) => update('solicitationDate', event.target.value)} />
                  </label>
                  <label>
                    <ModalFieldLabel>Medio de transporte</ModalFieldLabel>
                    <input name="guide-means-of-transport" value={form.meansOfTransport} onChange={(event) => update('meansOfTransport', event.target.value)} />
                  </label>
                  <label>
                    <ModalFieldLabel>Transportista</ModalFieldLabel>
                    <input name="guide-transport-name" value={form.transportName} onChange={(event) => update('transportName', event.target.value)} />
                  </label>
                  <label>
                    <ModalFieldLabel>Matrícula</ModalFieldLabel>
                    <input name="guide-vehicle-registration" value={form.vehicleRegistrationNumber} onChange={(event) => update('vehicleRegistrationNumber', event.target.value.toUpperCase())} />
                  </label>
                </div>
              )}
            </section>
          </>
        )}

        {step === 2 && preview && (
          <section className="bulk-preview">
            <div className="bulk-preview-summary">
              <div><strong>{preview.totalAnimals}</strong><span>Seleccionados</span></div>
              <div><strong>{preview.validAnimals}</strong><span>Preparados</span></div>
              <div className={preview.conflictAnimals ? 'bulk-summary-conflict' : ''}>
                <strong>{preview.conflictAnimals}</strong><span>Conflictos</span>
              </div>
            </div>
            <div className="filter-summary">
              <div><strong>Guía:</strong> {preview.guide.resolution}</div>
              {preview.guide.movementId && <span>#{preview.guide.movementId}</span>}
            </div>
            <div className="table-scroll bulk-preview-table">
              <table className="animal-table">
                <thead>
                  <tr>
                    <th>Animal</th>
                    <th>Alta resultante</th>
                    <th>Baja resultante</th>
                    <th>Validación</th>
                  </tr>
                </thead>
                <tbody>
                  {previewRows.map((row) => (
                    <tr key={row.animalId}>
                      <td><strong>{row.identification}</strong></td>
                      <td>{row.resultRegistrationCause ?? '—'} · {row.resultRegistrationDate ?? '—'}</td>
                      <td>{row.resultDischargeCause ?? '—'} · {row.resultDischargeDate ?? '—'}</td>
                      <td className={row.isValid ? 'bulk-row-valid' : 'bulk-row-conflict'}>
                        {row.isValid ? 'Preparado' : row.message}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {previewPages > 1 && (
              <div className="animal-pagination bulk-preview-pagination">
                <button className="animal-pagination-button" type="button" disabled={previewPage === 1} onClick={() => setPreviewPage((value) => value - 1)}>Anterior</button>
                <span>Página {previewPage} de {previewPages}</span>
                <button className="animal-pagination-button" type="button" disabled={previewPage === previewPages} onClick={() => setPreviewPage((value) => value + 1)}>Siguiente</button>
              </div>
            )}
          </section>
        )}

        {step === 3 && result && (
          <div className="bulk-result">
            <CheckCircle2 size={42} />
            <h3>Modificación completada</h3>
            <p>Se han actualizado {result.updatedAnimals} animales.</p>
            {(result.linkedAnimals > 0 || result.unlinkedAnimals > 0) && (
              <p>{result.linkedAnimals} vinculados · {result.unlinkedAnimals} desvinculados de guías.</p>
            )}
            <small>Operación {result.operationId}</small>
          </div>
        )}
      </ModalBody>
      <ModalFooter>
        {step === 1 && (
          <>
            <button className="secondary-button" type="button" disabled={submitting} onClick={onClose}>Cancelar</button>
            <button className="primary-button" type="button" disabled={submitting} onClick={requestPreview}>
              {submitting ? 'Previsualizando...' : 'Previsualizar cambios'}
            </button>
          </>
        )}
        {step === 2 && (
          <>
            <button className="secondary-button" type="button" disabled={submitting} onClick={() => setStep(1)}>Volver y editar</button>
            <button className="primary-button" type="button" disabled={submitting || preview.conflictAnimals > 0} onClick={commit}>
              {submitting ? 'Guardando...' : `Confirmar ${preview.totalAnimals} cambios`}
            </button>
          </>
        )}
        {step === 3 && (
          <button className="primary-button" type="button" onClick={onClose}>Cerrar</button>
        )}
      </ModalFooter>
    </ModalDialog>
  );
}
