import { http, HttpResponse, type HttpHandler } from 'msw';
import type { CheckInQrArea, CompanyCheckInQr } from './types';

/** Boshlang'ich rotatsiya vaqti (mock'larda qat'iy). */
const BASE_ROTATED_AT = Date.parse('2026-09-01T09:00:00+05:00');

/** FNV-1a (32 bit) — deterministik "tasodifiy" hex uchun. */
function fnv1a(input: string): number {
  let h = 0x811c9dc5;
  for (let i = 0; i < input.length; i += 1) {
    h ^= input.charCodeAt(i);
    h = Math.imul(h, 0x01000193);
  }
  return h >>> 0;
}

/** `AMLQR:1:<32 hex>` — korxona id va rotatsiya tartib raqamidan (bir xil kirish → bir xil payload). */
export function mockQrPayload(companyId: string, version: number): string {
  const hex = [0, 1, 2, 3]
    .map((i) => fnv1a(`${companyId}:${version}:${i}`).toString(16).padStart(8, '0'))
    .join('');
  return `AMLQR:1:${hex}`;
}

/** Korxona → rotatsiyalar soni (0 — boshlang'ich QR). Testlar orasida `resetCheckinQrMock()`. */
let versions: Record<string, number> = {};

export function resetCheckinQrMock() {
  versions = {};
}

function build(companyId: string, companyName: string): CompanyCheckInQr {
  const version = versions[companyId] ?? 0;
  return {
    companyId,
    companyName,
    payload: mockQrPayload(companyId, version),
    // Har rotatsiya — boshlang'ichdan keyin +1 kun (deterministik).
    rotatedAt: new Date(BASE_ROTATED_AT + version * 86_400_000).toISOString(),
  };
}

function notFound() {
  return HttpResponse.json(
    { status: 404, title: 'Topilmadi', detail: 'Korxona topilmadi.' },
    { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
  );
}

/**
 * `GET …/checkin-qr` va `POST …/checkin-qr/rotate` handler'lari.
 * `lookupName` — korxona shu rol ko'lamida bo'lsa uning nomi, aks holda null (→ 404).
 */
export function checkinQrHandlers(
  area: CheckInQrArea,
  lookupName: (companyId: string) => string | null,
): HttpHandler[] {
  const base = `/api/${area}/companies/:id/checkin-qr`;
  return [
    http.get(base, ({ params }) => {
      const id = String(params['id']);
      const name = lookupName(id);
      if (name === null) return notFound();
      return HttpResponse.json(build(id, name));
    }),
    http.post(`${base}/rotate`, ({ params }) => {
      const id = String(params['id']);
      const name = lookupName(id);
      if (name === null) return notFound();
      versions[id] = (versions[id] ?? 0) + 1;
      return HttpResponse.json(build(id, name));
    }),
  ];
}
