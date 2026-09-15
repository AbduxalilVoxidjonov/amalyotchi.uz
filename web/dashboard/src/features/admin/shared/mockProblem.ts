import { HttpResponse } from 'msw';

const PROBLEM_HEADERS = { 'Content-Type': 'application/problem+json' };

/** MSW handler'lar uchun `ProblemDetails` javobi (`ApiError.fromProblem` shakli bilan mos). */
export function problemResponse(status: number, title: string, detail: string, extra?: object) {
  return HttpResponse.json(
    { status, title, detail, ...extra },
    { status, headers: PROBLEM_HEADERS },
  );
}
