import assert from 'node:assert/strict';
import { test } from 'node:test';
import { renderPdfPreviewPages } from '../src/features/farms/pdfPreview.js';

function createFixture(t, overrides = {}) {
  const controller = new AbortController();
  const canvases = [];
  const page = {
    getViewport: ({ scale }) => ({ width: 600 * scale, height: 800 * scale }),
    render: t.mock.fn(() => ({ promise: Promise.resolve() })),
    cleanup: t.mock.fn()
  };
  // PDF.js 6 documents deliberately have no destroy method.
  const document = { numPages: 5, getPage: t.mock.fn(async () => page) };
  const task = { promise: Promise.resolve(document), destroy: t.mock.fn(async () => {}) };
  const options = {
    getDocument: t.mock.fn(() => task),
    data: new Uint8Array([1]),
    signal: controller.signal,
    createCanvas: () => {
      const canvas = { getContext: () => ({}), toDataURL: () => 'data:image/png;base64,preview', width: 0, height: 0 };
      canvases.push(canvas);
      return canvas;
    },
    ...overrides
  };
  return { controller, canvases, page, document, task, options };
}

test('PDF6 previews render at most three pages and dispose through the loading task exactly once', async (t) => {
  const { task, options, page, canvases, controller } = createFixture(t);
  const result = await renderPdfPreviewPages(options);
  assert.equal(result.pageCount, 5);
  assert.deepEqual(result.pages.map(({ pageNumber }) => pageNumber), [1, 2, 3]);
  assert.equal(result.pages[0].width, 750);
  assert.equal(task.destroy.mock.callCount(), 1);
  assert.equal(page.cleanup.mock.callCount(), 3);
  assert.ok(canvases.every((canvas) => canvas.width === 0 && canvas.height === 0));
  controller.abort();
  await Promise.resolve();
  assert.equal(task.destroy.mock.callCount(), 1);
});

test('cancelling before PDF creation does not start a worker', async (t) => {
  const { options, controller } = createFixture(t);
  controller.abort();
  await assert.rejects(renderPdfPreviewPages(options), { name: 'AbortError' });
  assert.equal(options.getDocument.mock.callCount(), 0);
});

test('cancelling a pending PDF load destroys once without leaking a rejected promise', async (t) => {
  const { options, controller, task } = createFixture(t);
  let rejectLoading;
  task.promise = new Promise((resolve, reject) => { rejectLoading = reject; });
  task.destroy = t.mock.fn(async () => rejectLoading(new DOMException('Cancelled', 'AbortError')));
  const rendering = renderPdfPreviewPages(options);
  const assertion = assert.rejects(rendering, { name: 'AbortError' });
  controller.abort();
  await assertion;
  assert.equal(task.destroy.mock.callCount(), 1);
});

test('cancelling during page rendering releases the canvas and loading task once', async (t) => {
  const { options, controller, task, page, canvases } = createFixture(t);
  let markRendering;
  const started = new Promise((resolve) => { markRendering = resolve; });
  let rejectRender;
  page.render = t.mock.fn(() => {
    const promise = new Promise((resolve, reject) => { rejectRender = reject; });
    markRendering();
    return { promise };
  });
  task.destroy = t.mock.fn(async () => rejectRender(new DOMException('Cancelled', 'AbortError')));
  const rendering = renderPdfPreviewPages(options);
  const assertion = assert.rejects(rendering, { name: 'AbortError' });
  await started;
  controller.abort();
  await assertion;
  assert.equal(task.destroy.mock.callCount(), 1);
  assert.equal(page.cleanup.mock.callCount(), 1);
  assert.equal(canvases[0].width, 0);
});

test('PDF cleanup failures propagate to the UI error handler as a handled rejection', async (t) => {
  const { options, task } = createFixture(t);
  task.destroy = t.mock.fn(async () => { throw new Error('Worker cleanup failed'); });
  await assert.rejects(renderPdfPreviewPages(options), { message: 'Worker cleanup failed' });
  assert.equal(task.destroy.mock.callCount(), 1);
});

test('failed PDF loads still release their loading task', async (t) => {
  const { options, task } = createFixture(t);
  task.promise = Promise.reject(new Error('Invalid PDF'));
  await assert.rejects(renderPdfPreviewPages(options), { message: 'Invalid PDF' });
  assert.equal(task.destroy.mock.callCount(), 1);
});
