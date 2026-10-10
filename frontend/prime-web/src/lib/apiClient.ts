import { supabase } from './supabaseClient';
import { devActAsHeader, getDevActAs } from './devActAs';

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:7221';

/**
 * Uniform API error shape from Prime.WebApi (CLAUDE.md §63):
 * { code, message, details, traceId }
 */
export interface ApiError {
  code: string;
  message: string;
  details: unknown;
  traceId: string;
}

export class ApiRequestError extends Error {
  readonly apiError: ApiError;
  readonly status: number;

  constructor(apiError: ApiError, status: number) {
    super(apiError.message);
    this.apiError = apiError;
    this.status = status;
  }
}

/**
 * Fetch wrapper that attaches the current Supabase session's access token
 * as a bearer token, and throws ApiRequestError (carrying the §63 error
 * shape) on non-2xx responses instead of returning a raw Response.
 */
export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const {
    data: { session },
  } = await supabase.auth.getSession();

  const headers = new Headers(init.headers);
  headers.set('Accept', 'application/json');
  if (session?.access_token) {
    headers.set('Authorization', `Bearer ${session.access_token}`);
  }
  const actAs = getDevActAs();
  if (actAs) {
    headers.set(devActAsHeader, actAs);
  }

  const response = await fetch(`${apiBaseUrl}${path}`, { ...init, headers });

  if (!response.ok) {
    const body = (await response.json().catch(() => null)) as ApiError | null;
    throw new ApiRequestError(
      body ?? { code: 'UNKNOWN_ERROR', message: response.statusText, details: null, traceId: '' },
      response.status,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/**
 * Downloads a file the API writes (a report as CSV or Excel) and hands it to the browser to save. Errors arrive in the
 * §63 shape like any call. `fileName` is used when the response does not name the file.
 */
export async function apiDownload(path: string, fileName: string): Promise<void> {
  const {
    data: { session },
  } = await supabase.auth.getSession();
  const headers = new Headers();
  if (session?.access_token) {
    headers.set('Authorization', `Bearer ${session.access_token}`);
  }
  const actAs = getDevActAs();
  if (actAs) {
    headers.set(devActAsHeader, actAs);
  }
  const response = await fetch(`${apiBaseUrl}${path}`, { headers });
  if (!response.ok) {
    const body = (await response.json().catch(() => null)) as ApiError | null;
    throw new ApiRequestError(
      body ?? { code: 'UNKNOWN_ERROR', message: response.statusText, details: null, traceId: '' },
      response.status,
    );
  }
  const named = /filename\*=UTF-8''([^;]+)/i.exec(response.headers.get('Content-Disposition') ?? '')?.[1];
  const url = URL.createObjectURL(await response.blob());
  const link = document.createElement('a');
  link.href = url;
  link.download = named ? decodeURIComponent(named) : fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

export function apiGet<T>(path: string, query?: Record<string, string | number | boolean | undefined | null>): Promise<T> {
  const qs = query
    ? '?' +
      Object.entries(query)
        .filter(([, v]) => v !== undefined && v !== null && v !== '')
        .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`)
        .join('&')
    : '';
  return apiFetch<T>(`${path}${qs && qs !== '?' ? qs : ''}`);
}

/**
 * The record version the screen displayed (its DTO's `rowVersion`). Sent as If-Match, the API refuses the write with
 * 409 CONCURRENCY_CONFLICT when someone else changed the record since (docs/analysis/production-hardening.md §4.4).
 */
export interface WriteOptions {
  ifMatch?: number;
}

function jsonHeaders(options?: WriteOptions): Record<string, string> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (options?.ifMatch !== undefined && options.ifMatch !== 0) {
    headers['If-Match'] = `"${options.ifMatch}"`;
  }
  return headers;
}

export function apiPut<T>(path: string, body: unknown, options?: WriteOptions): Promise<T> {
  return apiFetch<T>(path, {
    method: 'PUT',
    headers: jsonHeaders(options),
    body: JSON.stringify(body),
  });
}

export function apiPost<T>(path: string, body: unknown, options?: WriteOptions): Promise<T> {
  return apiFetch<T>(path, {
    method: 'POST',
    headers: jsonHeaders(options),
    body: JSON.stringify(body),
  });
}

/** The record was changed by someone else since it was loaded; nothing was saved (409). */
export function isConcurrencyConflict(error: unknown): boolean {
  return error instanceof ApiRequestError && error.apiError.code === 'CONCURRENCY_CONFLICT';
}
