/**
 * Backend xato formati — RFC 7807 ProblemDetails.
 * Manba: src/Amaliyotchi.Api/Infrastructure/GlobalExceptionHandler.cs
 *
 *   400 "Ma'lumotlar noto'g'ri"  + extensions.errors: { [field]: string[] } (FluentValidation)
 *   400 "Noto'g'ri amal"         (DomainException)
 *   403 "Ruxsat yo'q"            (ForbiddenException — login xatosi ham shu!)
 *   404 "Topilmadi"
 *   409 "Ziddiyat"
 *   500 "Ichki xatolik"
 *   Har birida extensions.traceId bor.
 *
 * 401 esa JwtBearer middleware'dan keladi va odatda BODY'siz bo'ladi.
 */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  /** FluentValidation xatolari: maydon nomi (camelCase) → xabarlar. */
  errors?: Record<string, string[]>;
  [key: string]: unknown;
}

export function isProblemDetails(value: unknown): value is ProblemDetails {
  if (typeof value !== 'object' || value === null) return false;
  const v = value as Record<string, unknown>;
  return (
    typeof v['status'] === 'number' ||
    typeof v['title'] === 'string' ||
    typeof v['detail'] === 'string'
  );
}
