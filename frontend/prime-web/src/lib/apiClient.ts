import { supabase } from './supabaseClient';

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

export function apiPut<T>(path: string, body: unknown): Promise<T> {
  return apiFetch<T>(path, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

export function apiPost<T>(path: string, body: unknown): Promise<T> {
  return apiFetch<T>(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}
