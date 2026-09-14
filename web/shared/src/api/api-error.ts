import type { ProblemDetails } from './problem-details';

export type ApiErrorKind =
  | 'validation' // 400 + errors
  | 'bad-request' // 400
  | 'unauthorized' // 401
  | 'forbidden' // 403
  | 'not-found' // 404
  | 'conflict' // 409
  | 'rate-limited' // 429
  | 'server' // 5xx
  | 'network' // fetch o'zi muvaffaqiyatsiz (offline, CORS, DNS)
  | 'aborted' // AbortSignal
  | 'unknown';

/**
 * Barcha API xatolari shu sinf orqali tashlanadi. UI qatlamida `instanceof ApiError`
 * tekshirib `kind` / `fieldErrors` / `message` dan foydalaniladi.
 */
export class ApiError extends Error {
  readonly status: number;
  readonly kind: ApiErrorKind;
  readonly title: string | undefined;
  readonly traceId: string | undefined;
  readonly problem: ProblemDetails | undefined;
  /** FluentValidation: maydon → xabarlar (faqat kind === 'validation'). */
  readonly fieldErrors: Record<string, string[]>;

  constructor(init: {
    status: number;
    message: string;
    kind?: ApiErrorKind;
    problem?: ProblemDetails | undefined;
    cause?: unknown;
  }) {
    super(init.message, init.cause !== undefined ? { cause: init.cause } : undefined);
    this.name = 'ApiError';
    this.status = init.status;
    this.problem = init.problem;
    this.title = init.problem?.title;
    this.traceId = init.problem?.traceId;
    this.fieldErrors = init.problem?.errors ?? {};
    this.kind = init.kind ?? ApiError.kindFromStatus(init.status, this.fieldErrors);
  }

  static fromProblem(
    status: number,
    problem: ProblemDetails | undefined,
    fallback: string,
  ): ApiError {
    const message = problem?.detail ?? problem?.title ?? fallback;
    return new ApiError({ status, message, problem });
  }

  static network(cause: unknown): ApiError {
    return new ApiError({
      status: 0,
      kind: 'network',
      message: "Server bilan aloqa yo'q. Internetni tekshirib, qayta urinib ko'ring.",
      cause,
    });
  }

  static aborted(cause: unknown): ApiError {
    return new ApiError({ status: 0, kind: 'aborted', message: "So'rov bekor qilindi.", cause });
  }

  static kindFromStatus(status: number, fieldErrors: Record<string, string[]>): ApiErrorKind {
    if (status === 400) return Object.keys(fieldErrors).length > 0 ? 'validation' : 'bad-request';
    if (status === 401) return 'unauthorized';
    if (status === 403) return 'forbidden';
    if (status === 404) return 'not-found';
    if (status === 409) return 'conflict';
    if (status === 429) return 'rate-limited';
    if (status >= 500) return 'server';
    return 'unknown';
  }

  /** Bitta maydon uchun birinchi validatsiya xabari. */
  fieldError(field: string): string | undefined {
    return this.fieldErrors[field]?.[0] ?? this.fieldErrors[capitalize(field)]?.[0];
  }
}

function capitalize(s: string): string {
  return s.length === 0 ? s : s[0]!.toUpperCase() + s.slice(1);
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError;
}

/** UI uchun xavfsiz xabar: ApiError bo'lsa uning matni, aks holda umumiy matn. */
export function errorMessage(error: unknown, fallback = 'Kutilmagan xatolik yuz berdi.'): string {
  if (isApiError(error)) return error.message;
  if (error instanceof Error && error.message) return error.message;
  return fallback;
}
