import { ApiError } from './api-error';

describe('ApiError', () => {
  it('ProblemDetails dan xabar va turni oladi', () => {
    const err = ApiError.fromProblem(
      403,
      { status: 403, title: "Ruxsat yo'q", detail: "Parol noto'g'ri.", traceId: 't-1' },
      'fallback',
    );
    expect(err.kind).toBe('forbidden');
    expect(err.message).toBe("Parol noto'g'ri.");
    expect(err.traceId).toBe('t-1');
  });

  it("errors bo'lsa validation deb belgilaydi va maydon xabarini qaytaradi", () => {
    const err = ApiError.fromProblem(
      400,
      { status: 400, title: "Ma'lumotlar noto'g'ri", errors: { PhoneNumber: ['Kiriting.'] } },
      'fallback',
    );
    expect(err.kind).toBe('validation');
    expect(err.fieldError('phoneNumber')).toBe('Kiriting.');
  });
});
