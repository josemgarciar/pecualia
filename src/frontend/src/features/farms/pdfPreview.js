import { BOOK_PREVIEW_MAX_PAGES, BOOK_PREVIEW_TARGET_WIDTH } from './farmOptions.js';

async function renderPageImage(page, createCanvas) {
  const baseViewport = page.getViewport({ scale: 1 });
  const scale = Math.min(1.25, BOOK_PREVIEW_TARGET_WIDTH / baseViewport.width);
  const viewport = page.getViewport({ scale });
  const canvas = createCanvas();
  const context = canvas.getContext('2d', { alpha: false });
  if (!context) {
    throw new Error('No se pudo preparar la vista previa del PDF.');
  }

  try {
    canvas.width = Math.ceil(viewport.width);
    canvas.height = Math.ceil(viewport.height);
    await page.render({ canvas, canvasContext: context, viewport }).promise;
    return { src: canvas.toDataURL('image/png'), width: canvas.width, height: canvas.height };
  } finally {
    page.cleanup();
    canvas.width = 0;
    canvas.height = 0;
  }
}

export async function renderPdfPreviewPages({ getDocument, data, signal, createCanvas = () => document.createElement('canvas') }) {
  signal.throwIfAborted();
  const loadingTask = getDocument({ data });
  let destruction;
  const destroy = () => destruction ??= Promise.resolve().then(() => loadingTask.destroy());
  const abort = () => {
    // The finally block observes cleanup failures; the event handler cannot await them.
    void destroy().catch(() => {});
  };
  signal.addEventListener('abort', abort, { once: true });

  try {
    const pdfDocument = await loadingTask.promise;
    const pages = [];
    const pagesToRender = Math.min(pdfDocument.numPages, BOOK_PREVIEW_MAX_PAGES);
    for (let pageNumber = 1; pageNumber <= pagesToRender; pageNumber += 1) {
      signal.throwIfAborted();
      const page = await pdfDocument.getPage(pageNumber);
      signal.throwIfAborted();
      pages.push({ pageNumber, ...await renderPageImage(page, createCanvas) });
    }
    signal.throwIfAborted();
    return { pages, pageCount: pdfDocument.numPages };
  } finally {
    signal.removeEventListener('abort', abort);
    await destroy();
  }
}
