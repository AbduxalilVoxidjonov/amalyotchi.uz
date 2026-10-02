import { ApiError } from './api-error';
import { isProblemDetails, type ProblemDetails } from './problem-details';

export type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

export type QueryValue = string | number | boolean | null | undefined;
export type QueryParams = Record<string, QueryValue | QueryValue[]>;

export interface RequestOptions {
  /** JSON sifatida yuboriladi (FormData bo'lsa — o'zgarishsiz). */
  body?: unknown;
  /** URL query; undefined/null qiymatlar tashlab yuboriladi. */
  query?: QueryParams;
  headers?: Record<string, string>;
  signal?: AbortSignal;
  /** `false` → Authorization sarlavhasi qo'shilmaydi va 401 da refresh urinilmaydi. Default: true. */
  auth?: boolean;
  /** Ichki: refresh'dan keyingi qayta urinishda `false`. */
  retryOn401?: boolean;
}

export interface ApiClientOptions {
  /** Masalan `''` (relative, Vite proxy) yoki `https://api.example.uz`. Oxiridagi `/` olib tashlanadi. */
  baseUrl?: string;
  getAccessToken?: () => string | null | undefined;
  /**
   * 401 kelganda BIR MARTA chaqiriladi (parallel so'rovlar bitta refresh'ni kutadi — navbat).
   * Yangi access token qaytarsa asl so'rov qayta yuboriladi; `null` → sessiya tugagan.
   */
  refreshSession?: () => Promise<string | null>;
  /** Refresh ham muvaffaqiyatsiz bo'lganda (logout + /login ga yo'naltirish shu yerda). */
  onSessionExpired?: () => void;
  /** Test/SSR uchun; default — global fetch. */
  fetch?: typeof fetch;
  defaultHeaders?: Record<string, string>;
}

export interface ApiClient {
  request<T = unknown>(method: HttpMethod, path: string, options?: RequestOptions): Promise<T>;
  get<T = unknown>(path: string, options?: RequestOptions): Promise<T>;
  post<T = unknown>(path: string, body?: unknown, options?: RequestOptions): Promise<T>;
  put<T = unknown>(path: string, body?: unknown, options?: RequestOptions): Promise<T>;
  patch<T = unknown>(path: string, body?: unknown, options?: RequestOptions): Promise<T>;
  delete<T = unknown>(path: string, options?: RequestOptions): Promise<T>;
}

export function buildUrl(baseUrl: string, path: string, query?: QueryParams): string {
  const base = baseUrl.replace(/\/+$/, '');
  const p = path.startsWith('/') ? path : `/${path}`;
  const url = `${base}${p}`;
  if (!query) return url;

  const params = new URLSearchParams();
  for (const [key, raw] of Object.entries(query)) {
    const values = Array.isArray(raw) ? raw : [raw];
    for (const v of values) {
      if (v === undefined || v === null) continue;
      params.append(key, String(v));
    }
  }
  const qs = params.toString();
  return qs ? `${url}${url.includes('?') ? '&' : '?'}${qs}` : url;
}

async function parseBody(response: Response): Promise<unknown> {
  if (response.status === 204 || response.status === 205) return undefined;
  const contentType = response.headers.get('content-type') ?? '';
  const text = await response.text();
  if (text.length === 0) return undefined;
  if (contentType.includes('json')) {
    try {
      return JSON.parse(text) as unknown;
    } catch {
      return text;
    }
  }
  return text;
}

function toApiError(response: Response, body: unknown): ApiError {
  const problem: ProblemDetails | undefined = isProblemDetails(body) ? body : undefined;
  return ApiError.fromProblem(response.status, problem, fallbackMessage(response.status));
}

/**
 * ProblemDetails bo'lmagan javob (nginx 502/503/504 HTML sahifasi, 413, 429 …) uchun foydalanuvchiga
 * tushunarli matn. `statusText` ("Bad Gateway") yoki "HTTP 502" ko'rsatilmaydi.
 */
function fallbackMessage(status: number): string {
  if (status === 401) return 'Avtorizatsiya talab qilinadi.';
  if (status === 403) return "Bu amal uchun ruxsat yo'q.";
  if (status === 404) return "So'ralgan ma'lumot topilmadi.";
  if (status === 413) return 'Yuborilayotgan fayl hajmi juda katta.';
  if (status === 429) return "So'rovlar juda ko'p. Birozdan so'ng qayta urinib ko'ring.";
  if (status >= 500)
    return "Server vaqtincha javob bermayapti. Birozdan so'ng qayta urinib ko'ring.";
  return "So'rovni bajarib bo'lmadi. Qayta urinib ko'ring.";
}

/**
 * Yengil, native fetch asosidagi API client.
 * - JSON kiritish/chiqarish, ProblemDetails → ApiError.
 * - 401 → `refreshSession` bir marta (single-flight), so'ng bitta qayta urinish; bo'lmasa `onSessionExpired`.
 */
export function createApiClient(options: ApiClientOptions = {}): ApiClient {
  const baseUrl = options.baseUrl ?? '';
  const doFetch: typeof fetch = options.fetch ?? ((input, init) => fetch(input, init));

  // Bitta vaqtda faqat bitta refresh: barcha 401 olgan so'rovlar shu promise'ni kutadi.
  let refreshInFlight: Promise<string | null> | null = null;

  function refreshOnce(): Promise<string | null> {
    if (!options.refreshSession) return Promise.resolve(null);
    if (!refreshInFlight) {
      refreshInFlight = options
        .refreshSession()
        .catch(() => null)
        .finally(() => {
          refreshInFlight = null;
        });
    }
    return refreshInFlight;
  }

  async function request<T>(
    method: HttpMethod,
    path: string,
    opts: RequestOptions = {},
  ): Promise<T> {
    const useAuth = opts.auth ?? true;
    const headers: Record<string, string> = {
      Accept: 'application/json',
      ...options.defaultHeaders,
      ...opts.headers,
    };

    let body: BodyInit | null = null;
    if (opts.body !== undefined) {
      if (typeof FormData !== 'undefined' && opts.body instanceof FormData) {
        body = opts.body;
      } else {
        headers['Content-Type'] = 'application/json';
        body = JSON.stringify(opts.body);
      }
    }

    if (useAuth) {
      const token = options.getAccessToken?.();
      if (token) headers['Authorization'] = `Bearer ${token}`;
    }

    let response: Response;
    try {
      response = await doFetch(buildUrl(baseUrl, path, opts.query), {
        method,
        headers,
        body,
        ...(opts.signal ? { signal: opts.signal } : {}),
      });
    } catch (cause) {
      if (opts.signal?.aborted || (cause instanceof Error && cause.name === 'AbortError')) {
        throw ApiError.aborted(cause);
      }
      throw ApiError.network(cause);
    }

    if (response.status === 401 && useAuth && opts.retryOn401 !== false) {
      const newToken = await refreshOnce();
      if (newToken) {
        return request<T>(method, path, { ...opts, retryOn401: false });
      }
      options.onSessionExpired?.();
      throw toApiError(response, await parseBody(response));
    }

    const parsed = await parseBody(response);
    if (!response.ok) throw toApiError(response, parsed);
    return parsed as T;
  }

  return {
    request,
    get: (path, o) => request('GET', path, o),
    post: (path, body, o) => request('POST', path, { ...o, body }),
    put: (path, body, o) => request('PUT', path, { ...o, body }),
    patch: (path, body, o) => request('PATCH', path, { ...o, body }),
    delete: (path, o) => request('DELETE', path, o),
  };
}
