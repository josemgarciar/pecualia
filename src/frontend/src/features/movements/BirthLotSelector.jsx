import { useEffect, useState } from 'react';
import { apiRequest } from '../../shared/api/client';

export function BirthLotSelector({ farmId, date, movementId, selections, onChange, refreshVersion = 0 }) {
  const [options, setOptions] = useState(null);
  const [error, setError] = useState('');
  useEffect(() => {
    let cancelled = false;
    setOptions(null);
    setError('');
    if (!date) return () => { cancelled = true; };
    const query = new URLSearchParams({ date });
    if (movementId) query.set('movementId', movementId);
    apiRequest(`/api/farms/${farmId}/movement-birth-lots?${query}`)
      .then(result => { if (!cancelled) setOptions(result); })
      .catch(reason => { if (!cancelled) setError(reason.message); });
    return () => { cancelled = true; };
  }, [farmId, date, movementId, refreshVersion]);

  if (error) return <p role="alert">{error}</p>;
  if (!options) return <p>{date ? 'Cargando lotes de nacimiento…' : 'Indica la fecha de salida para ver los lotes.'}</p>;
  const lots = options.lots;
  return <div className="stack">
    <p>Lotes de los últimos 12 meses, según su edad en la fecha de salida. Indica cuántos animales salen de cada lote.</p>
    {options.unallocatedMovements > 0 && <div role="status">
      <p>Guías {movementId ? 'adicionales ' : ''}pendientes de asignar a lotes: {options.unallocatedMovements}.
        {' '}Pueden tener una fecha anterior o posterior a esta salida. Las cantidades disponibles aún no descuentan esas guías.</p>
      <ul>{(options.unallocatedGuides ?? []).map(guide => <li key={guide.id}>
        Guía {guide.serie || `#${guide.id}`} · {guide.departureDate.slice(0, 10).split('-').reverse().join('/')} · {guide.numberOfAnimals} animales
      </li>)}</ul>
      <p>{movementId
        ? 'Puedes asignar y guardar los lotes de esta guía. Después, completa las guías pendientes indicadas.'
        : 'Completa las asignaciones de esas guías antes de registrar una nueva salida.'}</p>
    </div>}
    {lots.length === 0 ? <p>No hay lotes de nacimiento de los últimos 12 meses para la fecha indicada.</p> :
      <div className="birth-lot-table-scroll"><table className="birth-lot-table"><thead><tr><th>Nacimiento</th><th>Edad en la salida</th><th>Nacidos</th><th>Disponibles</th><th>Animales que salen</th></tr></thead>
        <tbody>{lots.map(lot => <tr key={lot.birthId}>
          <td>{lot.birthDate.split('-').reverse().join('/')}</td><td>{lot.category === 'Under4Months' ? 'Menores de 4 meses' : 'De 4 a 12 meses'}</td><td>{lot.born}</td><td>{lot.available}</td>
          <td><input aria-label={`Animales del lote ${lot.birthDate}`} type="number" min="0" max={lot.available} step="1"
            disabled={lot.available === 0 || (!movementId && options.unallocatedMovements > 0)}
            value={selections.find(item => item.birthId === lot.birthId)?.quantity ?? ''}
            onChange={event => {
              const quantity = Number(event.target.value);
              const remaining = selections.filter(item => item.birthId !== lot.birthId);
              onChange(quantity > 0 ? [...remaining, { birthId: lot.birthId, quantity, birthDate: lot.birthDate }] : remaining);
            }} /></td>
        </tr>)}</tbody></table></div>}
    <p>Total seleccionado: <strong>{selections.reduce((sum, lot) => sum + lot.quantity, 0)}</strong></p>
  </div>;
}

export function MovementBirthLotEditor({ farmId, movement }) {
  const [selections, setSelections] = useState(movement.birthLots ?? []);
  const [message, setMessage] = useState('');
  const [saving, setSaving] = useState(false);
  const [refreshVersion, setRefreshVersion] = useState(0);
  async function save() {
    setSaving(true);
    setMessage('');
    try {
      await apiRequest(`/api/movements/${movement.id}/birth-lots`, { method: 'PUT', body: { birthLots: selections } });
      setRefreshVersion(version => version + 1);
      setMessage('Lotes guardados. El censo y el libro se recalcularán con estas asignaciones al abrirlos.');
    } catch (error) { setMessage(error.message); }
    finally { setSaving(false); }
  }
  return <section className="stack">
    <h3>Lotes de nacimiento de esta salida</h3>
    <p>La selección debe sumar los {movement.numberOfAnimals} animales de la guía.</p>
    <BirthLotSelector farmId={farmId} date={movement.departureDate.slice(0, 10)}
      movementId={movement.id} selections={selections} onChange={setSelections} refreshVersion={refreshVersion} />
    <button type="button" className="primary-button" disabled={saving || selections.reduce((sum, lot) => sum + lot.quantity, 0) !== movement.numberOfAnimals} onClick={save}>
      {saving ? 'Guardando…' : 'Guardar lotes de la guía'}
    </button>
    {message && <p role="status">{message}</p>}
  </section>;
}
