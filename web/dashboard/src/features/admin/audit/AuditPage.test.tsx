import { screen } from '@testing-library/react';
import { renderWithProviders } from '../shared/renderWithProviders';
import { AuditPage } from './AuditPage';
import { auditDetail, auditWho, formatChanges } from './describe';
import { mockAudit } from './mocks';

describe('AuditPage', () => {
  it("v2 yozuvlarni ko'rsatadi (vaqt formati, amal badge, tafsilot, kim)", async () => {
    renderWithProviders(<AuditPage />);
    expect(await screen.findByText('12.10 09:31')).toBeInTheDocument();
    expect(screen.getByText("Qo'lda check-in")).toBeInTheDocument();
    expect(screen.getByText('Baho bekor qilindi')).toBeInTheDocument();
    expect(screen.getByText('Tizimga kirdi')).toBeInTheDocument();
    expect(screen.getByText('Sozlama — geofenceRadius: 200 → 250')).toBeInTheDocument();
    expect(screen.getByText('Baxtiyor Rasulov · tyutor')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Filtr: barcha amallar' })).toBeInTheDocument();
    expect(screen.getByText('1–6 / 6')).toBeInTheDocument();
  });
});

describe('describe', () => {
  it('foydalanuvchisiz (interceptor) yozuv — Tizim; changes JSON → "Maydon: eski → yangi"', () => {
    const entry = {
      ...mockAudit[0]!,
      userId: null,
      userName: null,
      userRole: null,
      reason: null,
      changes: '{"LastLoginAt": {"new": "09/14/2026 17:35:01 +00:00", "old": null}}',
    };
    expect(auditWho(entry)).toBe('Tizim');
    expect(auditDetail(entry)).toBe(
      'Davomat 01a0a0e1 — LastLoginAt: — → 09/14/2026 17:35:01 +00:00',
    );
  });

  it("changes noto'g'ri JSON bo'lsa xom matn, bo'sh bo'lsa null", () => {
    expect(formatChanges('oops')).toBe('oops');
    expect(formatChanges(null)).toBeNull();
    expect(formatChanges('{}')).toBeNull();
  });
});
