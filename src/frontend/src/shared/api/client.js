const JSON_HEADERS = {
  'Content-Type': 'application/json'
};

async function parseResponse(response) {
  const isJson = response.headers.get('content-type')?.includes('application/json');
  const payload = isJson ? await response.json() : null;

  if (!response.ok) {
    throw new Error(payload?.error ?? 'La operación no se pudo completar.');
  }

  return payload;
}

export async function apiRequest(path, { method = 'GET', body, token } = {}) {
  const headers = { ...JSON_HEADERS };
  if (!body) {
    delete headers['Content-Type'];
  }
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const response = await fetch(path, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined
  });

  return parseResponse(response);
}

export async function apiBlobRequest(path, { method = 'GET', token } = {}) {
  const headers = {};
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const response = await fetch(path, {
    method,
    headers
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
