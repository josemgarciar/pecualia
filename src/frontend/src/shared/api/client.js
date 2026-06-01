const JSON_HEADERS = {
  'Content-Type': 'application/json'
};

function buildApiUrl(path) {
  if (/^https?:\/\//i.test(path)) {
    return path;
  }

  const baseUrl = import.meta.env.VITE_API_BASE_URL?.trim();
  if (!baseUrl) {
    return path;
  }

  return new URL(path, `${baseUrl.replace(/\/+$/, '')}/`).toString();
}

async function parseResponse(response) {
  const isJson = response.headers.get('content-type')?.includes('application/json');
  const payload = isJson ? await response.json() : null;

  if (!response.ok) {
    throw new Error(payload?.error ?? 'La operación no se pudo completar.');
  }

  return payload;
}

export async function apiRequest(path, { method = 'GET', body } = {}) {
  const headers = { ...JSON_HEADERS };
  if (!body) {
    delete headers['Content-Type'];
  }

  const response = await fetch(buildApiUrl(path), {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
    credentials: 'include'
  });

  return parseResponse(response);
}

export async function apiBlobRequest(path, { method = 'GET', signal } = {}) {
  const headers = {};

  const response = await fetch(buildApiUrl(path), {
    method,
    headers,
    signal,
    credentials: 'include'
  });

  if (!response.ok) {
    const isJson = response.headers.get('content-type')?.includes('application/json');
    const payload = isJson ? await response.json() : null;
    throw new Error(payload?.error ?? 'La operación no se pudo completar.');
  }

  const disposition = response.headers.get('content-disposition') ?? '';
  const match = disposition.match(/filename="?([^"]+)"?/i);

  return {
    blob: await response.blob(),
    filename: match?.[1] ?? 'documento.pdf'
  };
}
