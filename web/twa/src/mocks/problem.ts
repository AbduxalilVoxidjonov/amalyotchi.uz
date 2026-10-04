import { HttpResponse } from 'msw';

const PROBLEM_HEADERS = { 'Content-Type': 'application/problem+json' };

/** RFC 7807 ProblemDetails — backend bilan bir xil shakl. */
export function problem(
  status: number,
  title: string,
  detail: string,
  extra?: { errors?: Record<string, string[]>; [key: string]: unknown },
) {
  return HttpResponse.json(
    { status, title, detail, traceId: 'mock', ...extra },
    { status, headers: PROBLEM_HEADERS },
  );
}

export const forbidden = (detail: string) => problem(403, "Ruxsat yo'q", detail);
export const unauthorized = () => new HttpResponse(null, { status: 401 });

/** Bearer bo'lmasa 401 (haqiqiy backend JwtBearer kabi). */
export function requireBearer(request: Request): Response | null {
  return request.headers.get('authorization')?.startsWith('Bearer ') ? null : unauthorized();
}
