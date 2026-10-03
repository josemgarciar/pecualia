import assert from 'node:assert/strict';
import { test } from 'node:test';
import { apiBlobRequest, apiRequest } from '../src/shared/api/client.js';

function mockResponse(t, content, options = {}) {
  return t.mock.method(globalThis, 'fetch', async () => new Response(content, options));
}

const jsonHeaders = { 'content-type': 'application/json' };

test('JSON requests preserve cookie authentication, body, method and cancellation', async (t) => {
  const fetch = mockResponse(t, '{"id":"farm-1"}', { headers: jsonHeaders });
  const signal = new AbortController().signal;
  const result = await apiRequest('/api/farms', { method: 'POST', body: { name: 'Finca' }, signal });
  assert.deepEqual(result, { id: 'farm-1' });
  assert.deepEqual(fetch.mock.calls[0].arguments, ['/api/farms', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: '{"name":"Finca"}', signal, credentials: 'include'
  }]);
});

test('GET has no body or unnecessary JSON content type', async (t) => {
  const fetch = mockResponse(t, '[]', { headers: jsonHeaders });
  assert.deepEqual(await apiRequest('/api/farms'), []);
  assert.deepEqual(fetch.mock.calls[0].arguments[1].headers, {});
  assert.equal(fetch.mock.calls[0].arguments[1].body, undefined);
});

test('false and zero remain legitimate JSON bodies', async (t) => {
  const fetch = mockResponse(t, null, { status: 204 });
  await apiRequest('/api/settings', { method: 'POST', body: false });
  await apiRequest('/api/settings', { method: 'POST', body: 0 });
  assert.equal(fetch.mock.calls[0].arguments[1].body, 'false');
  assert.equal(fetch.mock.calls[1].arguments[1].body, '0');
});

test('empty successful responses return null', async (t) => {
  mockResponse(t, null, { status: 204, headers: jsonHeaders });
  assert.equal(await apiRequest('/api/animals/1', { method: 'DELETE' }), null);
});

for (const request of [apiRequest, apiBlobRequest]) {
  test(`${request.name} preserves domain error messages`, async (t) => {
    mockResponse(t, '{"error":"No puedes eliminar esta explotación"}', { status: 409, headers: jsonHeaders });
    await assert.rejects(request('/api/farms/1'), { message: 'No puedes eliminar esta explotación' });
  });

  test(`${request.name} reads ProblemDetails errors`, async (t) => {
    mockResponse(t, '{"title":"Solicitud inválida","detail":"Identificación duplicada"}', {
      status: 400, headers: { 'content-type': 'application/problem+json' }
    });
    await assert.rejects(request('/api/animals'), { message: 'Identificación duplicada' });
  });

  test(`${request.name} handles HTML proxy errors without leaking the response`, async (t) => {
    mockResponse(t, '<html>upstream unavailable</html>', { status: 502, headers: { 'content-type': 'text/html' } });
    await assert.rejects(request('/api/farms'), { message: 'La operación no se pudo completar.' });
  });

  test(`${request.name} handles malformed JSON error bodies`, async (t) => {
    mockResponse(t, '{broken', { status: 500, headers: jsonHeaders });
    await assert.rejects(request('/api/farms'), { message: 'La operación no se pudo completar.' });
  });
}

test('malformed successful JSON fails instead of being mistaken for empty data', async (t) => {
  mockResponse(t, '{broken', { headers: jsonHeaders });
  await assert.rejects(apiRequest('/api/farms'), { message: 'La respuesta del servidor no es válida.' });
});

test('network errors and cancellation reach the caller', async (t) => {
  const aborted = new DOMException('Cancelled', 'AbortError');
  t.mock.method(globalThis, 'fetch', async () => { throw aborted; });
  await assert.rejects(apiRequest('/api/farms'), (error) => error === aborted);
  await assert.rejects(apiBlobRequest('/api/book'), (error) => error === aborted);
});

for (const [disposition, filename] of [
  ['attachment; filename="libro.pdf"', 'libro.pdf'],
  ['attachment; filename=libro.pdf; size=123', 'libro.pdf'],
  ["attachment; filename=libro.pdf; filename*=UTF-8''explotaci%C3%B3n.pdf", 'explotación.pdf'],
  ["attachment; filename=libro.pdf; filename*=UTF-8''broken%ZZ", 'libro.pdf'],
  ['', 'documento.pdf']
]) {
  test(`downloads preserve binary content and filename: ${disposition || 'default'}`, async (t) => {
    const fetch = mockResponse(t, '%PDF-example', { headers: { 'content-disposition': disposition } });
    const signal = new AbortController().signal;
    const result = await apiBlobRequest('/api/book', { signal });
    assert.equal(result.filename, filename);
    assert.equal(await result.blob.text(), '%PDF-example');
    assert.equal(fetch.mock.calls[0].arguments[1].signal, signal);
    assert.equal(fetch.mock.calls[0].arguments[1].credentials, 'include');
  });
}
