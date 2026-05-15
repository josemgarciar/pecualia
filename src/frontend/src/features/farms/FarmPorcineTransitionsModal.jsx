import { useMemo, useState } from 'react';
import { AlertTriangle, ArrowRightLeft, CalendarClock } from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { ModalBody, ModalDialog, ModalFooter, ModalHeader } from '../../shared/components/modal/Modal';
import { formatDate } from './FarmDetailShared';

function createForm(task) {
  return {
    toRears: String(task?.pendingQuantity ?? 0),
    toSowsReposition: '0',
    toMalesReposition: '0'
  };
}

export function FarmPorcineTransitionsModal({ farm, token, tasks, onClose, onResolved }) {
  const [selectedBirthId, setSelectedBirthId] = useState(tasks[0]?.birthId ?? null);
  const selectedTask = useMemo(
    () => tasks.find((task) => task.birthId === selectedBirthId) ?? tasks[0] ?? null,
    [selectedBirthId, tasks]
  );
  const [form, setForm] = useState(() => createForm(selectedTask));
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  function selectTask(task) {
    setSelectedBirthId(task.birthId);
    setForm(createForm(task));
    setError('');
  }

  function updateField(field, value) {
    setForm((current) => ({ ...current, [field]: value }));
    setError('');
  }

  async function handleSubmit(event) {
    event.preventDefault();
    if (!selectedTask) {
      return;
    }

    const toRears = Number(form.toRears);
    const toSowsReposition = Number(form.toSowsReposition);
    const toMalesReposition = Number(form.toMalesReposition);

    if (![toRears, toSowsReposition, toMalesReposition].every(Number.isInteger) || [toRears, toSowsReposition, toMalesReposition].some((value) => value < 0)) {
      setError('Las cantidades deben ser enteros iguales o mayores que cero.');
      return;
    }

    if (toRears + toSowsReposition + toMalesReposition !== selectedTask.pendingQuantity) {
      setError('La suma debe coincidir exactamente con los animales pendientes de reclasificación.');
      return;
    }

    setSaving(true);
    try {
      await apiRequest(`/api/farms/${farm.id}/porcine-transitions/${selectedTask.birthId}`, {
        method: 'PUT',
        token,
        body: {
          toRears,
          toSowsReposition,
          toMalesReposition
        }
      });
      await onResolved();
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setSaving(false);
    }
  }

  return (
    <ModalDialog cardAs="form" size="wide" shellClassName="porcine-transition-modal" onSubmit={handleSubmit}>
      <ModalHeader
        icon={<ArrowRightLeft size={18} />}
        title="Reclasificaciones porcinas pendientes"
        subtitle={farm.name}
        onClose={onClose}
      />
      <ModalBody className="porcine-transition-layout">
        <div className="porcine-transition-list">
          {tasks.map((task) => (
            <button
              key={task.birthId}
              type="button"
              className={`porcine-transition-item${selectedTask?.birthId === task.birthId ? ' porcine-transition-item-active' : ''}`}
              onClick={() => selectTask(task)}
            >
              <strong>{task.pendingQuantity} animales pendientes</strong>
              <span>Parto: {formatDate(task.birthDate)}</span>
              <span>Desde: {formatDate(task.dueDate)}</span>
              <span className={task.isOverdue ? 'porcine-transition-overdue' : 'porcine-transition-warning'}>
                {task.isOverdue ? `Vencido desde ${formatDate(task.finalTransitionDate)}` : `Límite rama intermedia: ${formatDate(task.finalTransitionDate)}`}
              </span>
            </button>
          ))}
        </div>

        {selectedTask && (
          <div className="porcine-transition-form stack">
            {error && <div className="error-banner">{error}</div>}

            <div className="info-callout">
              <CalendarClock size={18} />
              <p>
                Este lote cumplió 3 meses el <strong>{formatDate(selectedTask.dueDate)}</strong>. Reparte los animales entre las tres ramas intermedias.
              </p>
            </div>

            <div className="grid-form">
              <label className="farm-form-field">
                <span className="farm-field-label">Recría (3-6 meses)</span>
                <input type="number" min="0" value={form.toRears} onChange={(event) => updateField('toRears', event.target.value)} />
              </label>
              <label className="farm-form-field">
                <span className="farm-field-label">Hembras reposición (3-6 meses)</span>
                <input type="number" min="0" value={form.toSowsReposition} onChange={(event) => updateField('toSowsReposition', event.target.value)} />
              </label>
              <label className="farm-form-field">
                <span className="farm-field-label">Machos reposición (3-6 meses)</span>
                <input type="number" min="0" value={form.toMalesReposition} onChange={(event) => updateField('toMalesReposition', event.target.value)} />
              </label>
            </div>
          </div>
        )}
      </ModalBody>
      <ModalFooter align="end">
        <button className="secondary-button" type="button" onClick={onClose}>Cerrar</button>
        <button className="primary-button" type="submit" disabled={saving || !selectedTask}>
          {saving ? 'Guardando...' : 'Guardar reclasificación'}
        </button>
      </ModalFooter>
    </ModalDialog>
  );
}
