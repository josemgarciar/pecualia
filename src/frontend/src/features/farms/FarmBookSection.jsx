import { useEffect, useMemo, useState } from 'react';
import { Check } from 'lucide-react';
import { apiBlobRequest, apiRequest } from '../../shared/api/client';
import {
  BOOK_PREVIEW_DEBOUNCE_MS,
  BOOK_PREVIEW_MAX_PAGES,
  BOOK_PREVIEW_TARGET_WIDTH,
  buildBookPdfPath
} from './FarmDetailShared';

export function FarmBookSection({ farm }) {
  const [preview, setPreview] = useState(null);
  const [selectedSectionIds, setSelectedSectionIds] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [pdfPreviewPages, setPdfPreviewPages] = useState([]);
  const [pdfPreviewTotalPages, setPdfPreviewTotalPages] = useState(0);
  const [pdfPreviewLoading, setPdfPreviewLoading] = useState(false);
  const [pdfPreviewError, setPdfPreviewError] = useState('');
  const [downloading, setDownloading] = useState(false);
  const [printing, setPrinting] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function loadPreview() {
      setLoading(true);
      setError('');

      try {
        const response = await apiRequest(`/api/farms/${farm.id}/book/preview`);
        if (!cancelled) {
          setPreview(response);
          setSelectedSectionIds(response.sections.map((section) => section.id));
        }
      } catch (requestError) {
        if (!cancelled) {
          setError(requestError.message);
          setPreview(null);
          setSelectedSectionIds([]);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    loadPreview();

    return () => {
      cancelled = true;
    };
  }, [farm.id]);

  const orderedSelectedSectionIds = useMemo(
    () => preview?.sections.filter((section) => selectedSectionIds.includes(section.id)).map((section) => section.id) ?? [],
    [preview, selectedSectionIds]
  );

  const selectedSectionCount = orderedSelectedSectionIds.length;
  const totalSectionCount = preview?.sections.length ?? 0;
  const canGeneratePdf = selectedSectionCount > 0 && !loading;

  useEffect(() => {
    if (!preview || orderedSelectedSectionIds.length === 0) {
      setPdfPreviewPages([]);
      setPdfPreviewTotalPages(0);
      setPdfPreviewLoading(false);
      setPdfPreviewError('');
      return undefined;
    }

    let cancelled = false;
    let loadingTask = null;
    let pdfDocument = null;
    const abortController = new AbortController();

    async function renderPdfPreview() {
      setPdfPreviewLoading(true);
      setPdfPreviewError('');

      try {
        const { blob } = await apiBlobRequest(buildBookPdfPath(farm.id, orderedSelectedSectionIds), {
          signal: abortController.signal
        });

        if (cancelled || abortController.signal.aborted) {
          return;
        }

        const [{ GlobalWorkerOptions, getDocument }, workerModule] = await Promise.all([
          import('pdfjs-dist'),
          import('pdfjs-dist/build/pdf.worker.min.mjs?url')
        ]);
        GlobalWorkerOptions.workerSrc = workerModule.default;

        const pdfBytes = await blob.arrayBuffer();
        loadingTask = getDocument({ data: pdfBytes });
        pdfDocument = await loadingTask.promise;

        if (cancelled || abortController.signal.aborted) {
          return;
        }

        const pageCount = pdfDocument.numPages;
        const pagesToRender = Math.min(pageCount, BOOK_PREVIEW_MAX_PAGES);
        const renderedPages = [];

        for (let pageNumber = 1; pageNumber <= pagesToRender; pageNumber += 1) {
          const page = await pdfDocument.getPage(pageNumber);
          const baseViewport = page.getViewport({ scale: 1 });
          const scale = Math.min(1.25, BOOK_PREVIEW_TARGET_WIDTH / baseViewport.width);
          const viewport = page.getViewport({ scale });
          const canvas = document.createElement('canvas');
          const context = canvas.getContext('2d', { alpha: false });

          if (!context) {
            throw new Error('No se pudo preparar la vista previa del PDF.');
          }

          canvas.width = Math.ceil(viewport.width);
          canvas.height = Math.ceil(viewport.height);

          await page.render({
            canvasContext: context,
            viewport
          }).promise;

          renderedPages.push({
            pageNumber,
            src: canvas.toDataURL('image/png'),
            width: canvas.width,
            height: canvas.height
          });

          page.cleanup();
          canvas.width = 0;
          canvas.height = 0;
        }

        if (!cancelled && !abortController.signal.aborted) {
          setPdfPreviewPages(renderedPages);
          setPdfPreviewTotalPages(pageCount);
        }
      } catch (requestError) {
        if (!cancelled && !abortController.signal.aborted) {
          setPdfPreviewPages([]);
          setPdfPreviewTotalPages(0);
          setPdfPreviewError(requestError.message ?? 'No se pudo generar la vista previa del PDF.');
        }
      } finally {
        if (pdfDocument) {
          await pdfDocument.destroy().catch(() => {});
        } else if (loadingTask) {
          await loadingTask.destroy().catch(() => {});
        }

        if (!cancelled) {
          setPdfPreviewLoading(false);
        }
      }
    }

    const timeoutId = window.setTimeout(() => {
      renderPdfPreview();
    }, BOOK_PREVIEW_DEBOUNCE_MS);

    return () => {
      cancelled = true;
      abortController.abort();
      window.clearTimeout(timeoutId);
      if (loadingTask) {
        loadingTask.destroy().catch(() => {});
      }
      if (pdfDocument) {
        pdfDocument.destroy().catch(() => {});
      }
    };
  }, [farm.id, orderedSelectedSectionIds, preview]);

  function toggleSection(sectionId) {
    setSelectedSectionIds((current) => (
      current.includes(sectionId)
        ? current.filter((entry) => entry !== sectionId)
        : [...current, sectionId]
    ));
  }

  function selectAllSections() {
    setSelectedSectionIds(preview?.sections.map((section) => section.id) ?? []);
  }

  function clearSelectedSections() {
    setSelectedSectionIds([]);
  }

  async function handlePdf(mode) {
    if (!canGeneratePdf) {
      setError('Debes seleccionar al menos un apartado del libro.');
      return;
    }

    if (mode === 'download') {
      setDownloading(true);
    } else {
      setPrinting(true);
    }
    setError('');

    try {
      const { blob, filename } = await apiBlobRequest(buildBookPdfPath(farm.id, orderedSelectedSectionIds));
      const objectUrl = URL.createObjectURL(blob);

      if (mode === 'download') {
        const anchor = document.createElement('a');
        anchor.href = objectUrl;
        anchor.download = filename;
        anchor.click();
      } else {
        window.open(objectUrl, '_blank', 'noopener,noreferrer');
      }

      window.setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setDownloading(false);
      setPrinting(false);
    }
  }

  if (loading) {
    return <div className="panel-card empty-state">Preparando vista previa del libro...</div>;
  }

  return (
    <section className="panel-card stack">
      <div className="section-heading-row">
        <div>
          <h2>Libro de registro</h2>
          <p>Generación oficial imprimible a partir de los datos actuales de la explotación.</p>
        </div>
        <div className="operation-form-actions">
          <button className="secondary-button" type="button" onClick={() => handlePdf('print')} disabled={printing || downloading || !canGeneratePdf}>
            {printing ? 'Abriendo...' : 'Abrir / imprimir PDF'}
          </button>
          <button className="primary-button" type="button" onClick={() => handlePdf('download')} disabled={downloading || printing || !canGeneratePdf}>
            {downloading ? 'Descargando...' : 'Descargar PDF'}
          </button>
        </div>
      </div>

      {error && <div className="error-banner">{error}</div>}

      {preview && (
        <div className="book-layout">
          <aside className="book-config-card stack">
            <div className="detail-header">
              <div>
                <h2>Apartados a incluir</h2>
                <p>Selecciona qué secciones se imprimirán o exportarán en el libro.</p>
              </div>
              <strong>{selectedSectionCount}/{totalSectionCount}</strong>
            </div>

            <div className="book-toolbar">
              <button className="secondary-button" type="button" onClick={selectAllSections}>
                Seleccionar todo
              </button>
              <button className="secondary-button" type="button" onClick={clearSelectedSections} disabled={selectedSectionCount === 0}>
                Limpiar
              </button>
            </div>

            <div className="book-section-list">
              {preview.sections.map((section) => {
                const selected = selectedSectionIds.includes(section.id);

                return (
                  <button
                    key={section.id}
                    type="button"
                    className={selected ? 'book-section-option book-section-option-active' : 'book-section-option'}
                    onClick={() => toggleSection(section.id)}
                  >
                    <span className={selected ? 'book-section-check book-section-check-active' : 'book-section-check'}>
                      {selected ? <Check size={12} /> : null}
                    </span>
                    <span className="book-section-copy">
                      <strong>{section.title}</strong>
                      <small>{section.description}</small>
                    </span>
                    <span className="book-section-count">{section.items}</span>
                  </button>
                );
              })}
            </div>
          </aside>

          <div className="panel-card stack book-document-preview">
            <div className="detail-header">
              <div>
                <h2>Vista previa de impresión</h2>
                <p>Se muestran solo las tres primeras páginas del PDF real.</p>
              </div>
              <strong>
                {pdfPreviewTotalPages > 0
                  ? `${Math.min(pdfPreviewTotalPages, BOOK_PREVIEW_MAX_PAGES)} / ${pdfPreviewTotalPages} pág.`
                  : 'Sin páginas'}
              </strong>
            </div>

            {pdfPreviewError && <div className="error-banner">{pdfPreviewError}</div>}

            <div className="book-document-preview-body">
              {orderedSelectedSectionIds.length === 0 ? (
                <div className="empty-state">Selecciona al menos un apartado para generar la vista previa del PDF.</div>
              ) : (
                <>
                  <div className="book-preview-status">
                    <span>{preview.template === 'official-porcino' ? 'Plantilla oficial porcino' : 'Plantilla oficial ovino/caprino'}</span>
                    {pdfPreviewLoading ? <strong>Actualizando vista previa...</strong> : null}
                  </div>

                  {pdfPreviewPages.length === 0 && pdfPreviewLoading ? (
                    <div className="book-pdf-loading">
                      <div className="book-pdf-skeleton" />
                      <div className="book-pdf-skeleton" />
                      <div className="book-pdf-skeleton" />
                    </div>
                  ) : pdfPreviewPages.length === 0 ? (
                    <div className="empty-state">No hay páginas disponibles para la selección actual.</div>
                  ) : (
                    <div className="book-pdf-preview-list">
                      {pdfPreviewPages.map((page) => (
                        <article key={page.pageNumber} className="book-pdf-page-card">
                          <div className="book-pdf-page-meta">
                            <span>Página {page.pageNumber}</span>
                          </div>
                          <div className="book-pdf-page-frame">
                            <img
                              src={page.src}
                              alt={`Vista previa de la página ${page.pageNumber} del libro de registro`}
                              width={page.width}
                              height={page.height}
                            />
                          </div>
                        </article>
                      ))}
                    </div>
                  )}
                </>
              )}
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
