import { useMemo, useRef, useState } from 'react';
import { AlertCircle, CheckCircle2, FileSpreadsheet, RefreshCw, Trash2, Upload } from 'lucide-react';
import { apiRequest } from '../../shared/api/client';
import { isValidRegaCode, normalizeRegaCode } from '../../shared/validation/identifiers';

const PAGE_SIZE = 25;
const MAX_FILE_SIZE = 5 * 1024 * 1024;

const statusOptions = [
  { value: '', label: 'Todos los estados' },
  { value: 'valid', label: 'Válidas' },
  { value: 'warning', label: 'Con avisos' },
  { value: 'duplicate', label: 'Duplicadas' },
  { value: 'existing', label: 'Ya existentes' },
  { value: 'conflict', label: 'En otra explotación' },
  { value: 'farm_mismatch', label: 'REGA no coincide' },
  { value: 'invalid', label: 'No válidas' }
];

const statusLabels = {
  valid: 'Válida',
  warning: 'Aviso',
  duplicate: 'Duplicada',
  existing: 'Ya existente',
  conflict: 'Otro destino',
  farm_mismatch: 'REGA distinto',
  invalid: 'No válida'
};

const sexLabels = {
  Female: 'Hembra',
  female: 'Hembra',
  Male: 'Macho',
  male: 'Macho'
};

function ImportSummary({ summary }) {
  const rejected = summary.totalRows - summary.processableRows;

  return (
    <div className="farm-import-summary" aria-label="Resumen de importación">
      <div><strong>{summary.totalRows}</strong><span>Filas</span></div>
      <div className="farm-import-summary-success"><strong>{summary.processableRows}</strong><span>Importables</span></div>
      <div className={rejected ? 'farm-import-summary-danger' : ''}><strong>{rejected}</strong><span>Rechazadas</span></div>
      <div><strong>{summary.warningRows}</strong><span>Avisos</span></div>
    </div>
  );
}

export function FarmAnimalImportPanel({
  species,
  regaCode,
  farmId = null,
  document,
  onDocumentChange,
  onImported
}) {
  const inputRef = useRef(null);
  const [loading, setLoading] = useState(false);
  const [committing, setCommitting] = useState(false);
  const [error, setError] = useState('');
  const [result, setResult] = useState(null);
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);

  const rows = useMemo(() => {
    const allRows = document?.preview?.rows ?? [];
    return statusFilter ? allRows.filter((row) => row.status === statusFilter) : allRows;
  }, [document?.preview?.rows, statusFilter]);
  const pageCount = Math.max(1, Math.ceil(rows.length / PAGE_SIZE));
  const visibleRows = rows.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  async function selectFile(event) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) {
      return;
    }

    setError('');
    setResult(null);
    if (!file.name.toLowerCase().endsWith('.xls')) {
      setError('Selecciona el fichero .xls de Animales pertenecientes.');
      return;
    }
    if (file.size > MAX_FILE_SIZE) {
      setError('El documento supera el tamaño máximo de 5 MB.');
      return;
    }
    if (!isValidRegaCode(regaCode)) {
      setError('Indica primero un código REGA válido para comprobar la pertenencia de los animales.');
      return;
    }

    setLoading(true);
    try {
      const content = await file.text();
      const body = { fileName: file.name, content };
      const preview = farmId
        ? await apiRequest(`/api/farms/${farmId}/animal-imports/preview`, { method: 'POST', body })
        : await apiRequest('/api/farms/animal-imports/preview', {
            method: 'POST',
            body: {
              livestockSpecies: species,
              regaCode: normalizeRegaCode(regaCode),
              ...body
            }
          });
      onDocumentChange?.({ fileName: file.name, content, preview });
      setStatusFilter('');
      setPage(1);
    } catch (requestError) {
      onDocumentChange?.(null);
      setError(requestError.message);
    } finally {
      setLoading(false);
    }
  }

  async function commitImport() {
    if (!farmId || !document) {
      return;
    }

    setCommitting(true);
    setError('');
    try {
      const response = await apiRequest(`/api/farms/${farmId}/animal-imports/commit`, {
        method: 'POST',
        body: { fileName: document.fileName, content: document.content }
      });
      setResult(response);
      onDocumentChange?.(null);
      await onImported?.(response);
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setCommitting(false);
    }
  }

  function removeDocument() {
    onDocumentChange?.(null);
    setError('');
    setResult(null);
    setStatusFilter('');
    setPage(1);
  }

  return (
    <div className="farm-import-panel" data-testid="farm-animal-import">
      <input
        ref={inputRef}
        className="farm-import-file-input"
        type="file"
        name="farmAnimalImportFile"
        accept=".xls,application/vnd.ms-excel"
        onChange={selectFile}
        data-testid="farm-animal-import-file"
      />

      {result && (
        <div className="farm-import-result" role="status">
          <CheckCircle2 size={19} />
          <div>
            <strong>{result.createdAnimals} animales importados</strong>
            <span>{result.rejectedRows} filas no se importaron.</span>
          </div>
        </div>
      )}

      {!document ? (
        <button
          className="farm-import-dropzone"
          type="button"
          onClick={() => inputRef.current?.click()}
          disabled={loading}
        >
          {loading ? <RefreshCw className="spin" size={28} /> : <Upload size={28} />}
          <strong>{loading ? 'Analizando documento...' : 'Seleccionar documento .xls'}</strong>
          <span>Informe “Animales pertenecientes” · máximo 5 MB y 1.000 animales</span>
        </button>
      ) : (
        <>
          <div className="farm-import-file">
            <FileSpreadsheet size={22} />
            <div>
              <strong>{document.fileName}</strong>
              <span>El fichero se valida de nuevo al guardar.</span>
            </div>
            <button className="secondary-button" type="button" onClick={() => inputRef.current?.click()}>
              Reemplazar
            </button>
            <button className="icon-button" type="button" aria-label="Quitar documento" onClick={removeDocument}>
              <Trash2 size={17} />
            </button>
          </div>

          <ImportSummary summary={document.preview.summary} />

          <div className="farm-import-toolbar">
            <label>
              Estado
              <select
                name="farmAnimalImportStatus"
                value={statusFilter}
                onChange={(event) => {
                  setStatusFilter(event.target.value);
                  setPage(1);
                }}
              >
                {statusOptions.map((option) => (
                  <option key={option.value} value={option.value}>{option.label}</option>
                ))}
              </select>
            </label>
            <span>{rows.length} filas visibles</span>
          </div>

          <div className="farm-import-table-shell">
            <table className="farm-import-table">
              <thead>
                <tr>
                  <th>Fila</th>
                  <th>Crotal</th>
                  <th>Nacimiento</th>
                  <th>Raza</th>
                  <th>Sexo</th>
                  <th>Estado</th>
                  <th>Detalle</th>
                </tr>
              </thead>
              <tbody>
                {visibleRows.map((row) => (
                  <tr key={`${row.rowNumber}-${row.identification ?? 'sin-crotal'}`}>
                    <td>{row.rowNumber}</td>
                    <td><strong>{row.identification ?? '—'}</strong></td>
                    <td>{row.birthDate ?? '—'}</td>
                    <td>{row.breed ?? '—'}</td>
                    <td>{sexLabels[row.sex] ?? '—'}</td>
                    <td><span className={`farm-import-status farm-import-status-${row.status}`}>{statusLabels[row.status] ?? row.status}</span></td>
                    <td className="farm-import-message">{row.message}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {pageCount > 1 && (
            <div className="farm-import-pagination">
              <button className="secondary-button" type="button" disabled={page === 1} onClick={() => setPage((current) => current - 1)}>Anterior</button>
              <span>Página {page} de {pageCount}</span>
              <button className="secondary-button" type="button" disabled={page === pageCount} onClick={() => setPage((current) => current + 1)}>Siguiente</button>
            </div>
          )}

          {farmId && (
            <div className="farm-import-commit">
              <p>Se crearán únicamente las filas válidas y con aviso. Las demás permanecerán sin cambios.</p>
              <button
                className="primary-button"
                type="button"
                onClick={commitImport}
                disabled={committing || document.preview.summary.processableRows === 0}
                data-testid="farm-animal-import-commit"
              >
                <Upload size={16} />
                {committing ? 'Importando...' : `Importar ${document.preview.summary.processableRows} animales`}
              </button>
            </div>
          )}
        </>
      )}

      {error && (
        <div className="error-banner farm-import-error" role="alert">
          <AlertCircle size={16} />
          {error}
        </div>
      )}
    </div>
  );
}
