const OPERATION_FAILED = 'La operación no se pudo completar.';

function buildApiUrl(path) {
  if (/^https?:\/\//i.test(path)) {
    return path;
  }

  const baseUrl = import.meta.env?.VITE_API_BASE_URL?.trim();
  return baseUrl
    ? new URL(path, `${baseUrl.replace(/\/+$/, '')}/`).toString()
    : path;
}

async function readJsonPayload(response) {
  const contentType = response.headers.get('content-type') ?? '';
  if (!/\bapplication\/(?:[\w.-]+\+)?json\b/i.test(contentType) || response.status === 204) {
    return null;
  }

  const content = await response.text();
  if (!content.trim()) {
    return null;
  }

  try {
    return JSON.parse(content);
  } catch {
    if (response.ok) {
      throw new Error('La respuesta del servidor no es válida.');
    }
    return null;
  }
}

async function ensureSuccessfulResponse(response, payload) {
  if (response.ok) {
    return;
  }

  const errorPayload = payload === undefined ? await readJsonPayload(response) : payload;
  const message = errorPayload?.error ?? errorPayload?.detail ?? errorPayload?.title;
  throw new Error(typeof message === 'string' && message.trim() ? message : OPERATION_FAILED);
}

function getDownloadFilename(disposition) {
  const encoded = disposition.match(/(?:^|;)\s*filename\*=UTF-8'[^']*'([^;]+)/i);
  if (encoded) {
    try {
      return decodeURIComponent(encoded[1].trim());
    } catch {
      // Malformed extended names may still have a usable plain filename.
    }
  }

  const plain = disposition.match(/(?:^|;)\s*filename=(?:"([^"]+)"|([^;]+))/i);
  return plain?.[1] ?? plain?.[2]?.trim() ?? 'documento.pdf';
}

export async function apiRequest(path, { method = 'GET', body, signal } = {}) {
  const hasBody = body !== undefined && body !== null;
  const response = await fetch(buildApiUrl(path), {
    method,
    headers: hasBody ? { 'Content-Type': 'application/json' } : {},
    body: hasBody ? JSON.stringify(body) : undefined,
    signal,
    credentials: 'include'
  });

  const payload = await readJsonPayload(response);
  await ensureSuccessfulResponse(response, payload);
  return payload;
}

export async function apiBlobRequest(path, { method = 'GET', signal } = {}) {
  const response = await fetch(buildApiUrl(path), {
    method,
    headers: {},
    signal,
    credentials: 'include'
  });

  await ensureSuccessfulResponse(response);
  return {
    blob: await response.blob(),
    filename: getDownloadFilename(response.headers.get('content-disposition') ?? '')
  };
}
