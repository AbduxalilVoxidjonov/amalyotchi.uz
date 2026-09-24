import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeAll, describe, expect, it, vi } from 'vitest';
import type { AttendanceAttempt, AttendancePunch, StudentAttendanceDay } from '../types';
import { AttendanceDayTable } from './AttendanceDayTable';

// jsdom'da `createObjectURL` yo'q — AuthImage'ning muvaffaqiyatli yo'li uchun stub.
beforeAll(() => {
  const url = globalThis.URL as unknown as {
    createObjectURL?: (b: Blob) => string;
    revokeObjectURL?: (u: string) => void;
  };
  url.createObjectURL = vi.fn(() => 'blob:mock-photo');
  url.revokeObjectURL = vi.fn();
});

const punch = (at: string, photoUrl: string | null): AttendancePunch => ({
  at,
  atIso: `2026-09-09T${at}:00+05:00`,
  distanceM: 40,
  accuracyM: 8,
  lat: 41.31,
  lng: 69.28,
  photoUrl,
  outOfRadius: false,
});

const attempt = (over: Partial<AttendanceAttempt> & Pick<AttendanceAttempt, 'id' | 'at'>) =>
  ({
    kind: 'checkIn',
    atIso: `2026-09-09T${over.at}:00+05:00`,
    accepted: true,
    rejectReason: null,
    rejectMessage: null,
    distanceM: 40,
    accuracyM: 8,
    radiusM: 150,
    lat: 41.31,
    lng: 69.28,
    photoUrl: null,
    ...over,
  }) satisfies AttendanceAttempt;

function day(over: Partial<StudentAttendanceDay> & Pick<StudentAttendanceDay, 'date'>) {
  return {
    status: 'present',
    isWorkDay: true,
    checkIn: null,
    checkOut: null,
    autoClosed: false,
    suspicious: false,
    suspiciousReason: null,
    manual: false,
    manualReason: null,
    leaveRequestId: null,
    diary: null,
    attempts: 0,
    rejectedAttempts: 0,
    events: [],
    ...over,
  } satisfies StudentAttendanceDay;
}

const DAYS: StudentAttendanceDay[] = [
  day({
    date: '2026-09-09',
    checkIn: punch('09:05', '/api/files/in-0909'),
    checkOut: punch('17:10', '/api/files/out-0909'),
    attempts: 3,
    rejectedAttempts: 2,
    // Ataylab tartibsiz — galereya vaqt bo'yicha saralaydi.
    events: [
      attempt({ id: 'a3', at: '09:05', photoUrl: '/api/files/in-0909' }),
      attempt({
        id: 'a1',
        at: '08:48',
        accepted: false,
        rejectReason: 'outOfRadius',
        rejectMessage: 'Korxona hududidan tashqaridasiz: 540 m (ruxsat etilgan 150 m)',
        distanceM: 540,
        photoUrl: '/api/files/rej-0909',
      }),
      attempt({
        id: 'a2',
        at: '08:57',
        accepted: false,
        rejectReason: 'qrInvalid',
        rejectMessage: null,
        distanceM: 60,
      }),
      attempt({ id: 'a4', kind: 'checkOut', at: '17:10', photoUrl: '/api/files/out-0909' }),
    ],
  }),
  day({
    date: '2026-09-10',
    checkIn: punch('09:01', '/api/files/in-0910'),
    attempts: 1,
    events: [],
  }),
];

function renderTable(area: 'tutor' | 'admin' = 'tutor') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <AttendanceDayTable days={DAYS} radiusM={150} area={area} />
    </QueryClientProvider>,
  );
}

describe('AttendanceDayTable', () => {
  it("jadvalda kirish va chiqish thumbnail'lari yonma-yon; rasm bosilsa kun oynasi ochilmaydi", async () => {
    const user = userEvent.setup();
    renderTable();
    const table = screen.getByRole('table', { name: 'Kundalik jadval' });
    const row = within(table).getByText('09.09.2026').closest('[role="row"]') as HTMLElement;

    const inThumb = within(row).getByRole('button', {
      name: '09.09.2026 check-in rasmi — kattalashtirish',
    });
    const outThumb = within(row).getByRole('button', {
      name: '09.09.2026 check-out rasmi — kattalashtirish',
    });
    expect(inThumb).toHaveAttribute('title', '09.09.2026 check-in rasmi');
    expect(outThumb).toHaveAttribute('title', '09.09.2026 check-out rasmi');

    // Faqat kirish rasmi bor kun — chiqish o'rnida bo'sh joy.
    const row10 = within(table).getByText('10.09.2026').closest('[role="row"]') as HTMLElement;
    expect(within(row10).getByRole('img', { name: "Chiqish rasmi yo'q" })).toBeInTheDocument();

    await user.click(outThumb);
    const preview = await screen.findByRole('dialog', { name: '09.09.2026 check-out rasmi' });
    expect(preview).toBeInTheDocument();
    expect(screen.queryByRole('dialog', { name: /kun tafsiloti/ })).not.toBeInTheDocument();
  });

  it.each(['tutor', 'admin'] as const)(
    'kun oynasida urinishlar galereyasi — qabul/rad, sabab, masofa va rasmlar (%s)',
    async (area) => {
      const user = userEvent.setup();
      renderTable(area);
      await user.click(screen.getByText('09.09.2026'));
      const dialog = await screen.findByRole('dialog', { name: /09\.09\.2026 — kun tafsiloti/ });
      const gallery = within(dialog).getByRole('region', { name: 'Urinishlar' });
      expect(within(gallery).getByText('Urinishlar · 4 ta · 2 rad etilgan')).toBeInTheDocument();

      const cards = within(gallery).getAllByRole('listitem');
      // Vaqt tartibida.
      expect(cards.map((c) => c.getAttribute('aria-label'))).toEqual([
        'Kirish 08:48 — Rad etildi',
        'Kirish 08:57 — Rad etildi',
        'Kirish 09:05 — Qabul qilindi',
        'Chiqish 17:10 — Qabul qilindi',
      ]);

      const [rejOut, rejQr, accIn, accOut] = cards as [
        HTMLElement,
        HTMLElement,
        HTMLElement,
        HTMLElement,
      ];
      expect(within(rejOut).getByText('Rad etildi')).toHaveAttribute('data-status', 'bad');
      expect(
        within(rejOut).getByText('Korxona hududidan tashqaridasiz: 540 m (ruxsat etilgan 150 m)'),
      ).toBeInTheDocument();
      expect(within(rejOut).getByText('540 m')).toHaveAttribute('data-out-of-radius', 'true');
      expect(within(rejOut).getByText(/radius 150 m dan tashqarida/)).toBeInTheDocument();
      const rejPhoto = within(rejOut).getByRole('button', {
        name: '09.09.2026 08:48 kirish urinishi rasmi — kattalashtirish',
      });
      await waitFor(() =>
        expect(within(rejPhoto).getByRole('img')).toHaveAttribute('src', 'blob:mock-photo'),
      );

      // `rejectMessage` yo'q — sabab yorlig'i; rasm yo'q — placeholder.
      expect(within(rejQr).getByText('QR kod mos emas')).toBeInTheDocument();
      expect(within(rejQr).getByText("Selfi yo'q")).toBeInTheDocument();
      expect(within(rejQr).getByText('60 m')).not.toHaveAttribute('data-out-of-radius');

      expect(within(accIn).getByText('Qabul qilindi')).toHaveAttribute('data-status', 'ok');
      expect(within(accIn).queryByText('QR kod mos emas')).not.toBeInTheDocument();
      expect(within(accOut).getByText('Chiqish')).toBeInTheDocument();
      expect(
        within(accOut).getByRole('button', {
          name: '09.09.2026 17:10 chiqish urinishi rasmi — kattalashtirish',
        }),
      ).toBeInTheDocument();

      // Rasm bosilsa kattalashadi (kun oynasi ustida).
      await user.click(rejPhoto);
      expect(
        await screen.findByRole('dialog', { name: '09.09.2026 08:48 kirish urinishi rasmi' }),
      ).toBeInTheDocument();
    },
  );

  it("urinish bo'lmagan kunda galereya bo'limi chiqmaydi", async () => {
    const user = userEvent.setup();
    renderTable();
    await user.click(screen.getByText('10.09.2026'));
    const dialog = await screen.findByRole('dialog', { name: /10\.09\.2026 — kun tafsiloti/ });
    expect(within(dialog).queryByRole('region', { name: 'Urinishlar' })).not.toBeInTheDocument();
  });
});
