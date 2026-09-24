import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { server } from '@/mocks/server';
import { CheckInQrCard } from './CheckInQrCard';
import { mockQrPayload } from './mocks';
import type { CheckInQrArea } from './types';

function renderCard(area: CheckInQrArea = 'tutor', companyId = 'c1') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <CheckInQrCard area={area} companyId={companyId} />
    </QueryClientProvider>,
  );
}

afterEach(() => vi.restoreAllMocks());

describe('CheckInQrCard', () => {
  it.each(['tutor', 'admin'] as const)(
    "QR rasmi payload'dan chiziladi, sana va amallar ko'rinadi (%s)",
    async (area) => {
      renderCard(area);
      const card = within(await screen.findByRole('region', { name: 'Check-in QR kodi' }));
      const img = await card.findByRole('img', { name: 'Tech Solutions MChJ check-in QR kodi' });
      expect(img.getAttribute('src')).toMatch(/^data:image\/svg\+xml/);
      expect(card.getByText(mockQrPayload('c1', 0))).toBeInTheDocument();
      expect(card.getByText('Oxirgi yangilanish')).toBeInTheDocument();
      expect(card.getByText('01.09.2026 09:00')).toBeInTheDocument();
      expect(card.getByRole('button', { name: 'Yuklab olish (PNG)' })).toBeEnabled();
      expect(card.getByRole('button', { name: 'Chop etish' })).toBeEnabled();
      expect(card.getByRole('button', { name: 'Yangilash' })).toBeEnabled();
    },
  );

  it('Yangilash → tasdiq oynasi → yangi payload va sana', async () => {
    const user = userEvent.setup();
    renderCard('admin');
    const region = await screen.findByRole('region', { name: 'Check-in QR kodi' });
    const card = within(region);
    await card.findByText(mockQrPayload('c1', 0));

    await user.click(card.getByRole('button', { name: 'Yangilash' }));
    const dialog = await screen.findByRole('dialog', { name: 'QR kodni yangilash' });
    expect(
      within(dialog).getByText("Eski QR kod ishlamay qoladi, yangisini chop etish kerak bo'ladi."),
    ).toBeInTheDocument();

    // Bekor qilish — hech narsa o'zgarmaydi.
    await user.click(within(dialog).getByRole('button', { name: 'Bekor qilish' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(card.getByText(mockQrPayload('c1', 0))).toBeInTheDocument();

    await user.click(card.getByRole('button', { name: 'Yangilash' }));
    const again = await screen.findByRole('dialog', { name: 'QR kodni yangilash' });
    await user.click(within(again).getByRole('button', { name: 'Yangilash' }));

    expect(await card.findByText(mockQrPayload('c1', 1))).toBeInTheDocument();
    expect(card.queryByText(mockQrPayload('c1', 0))).not.toBeInTheDocument();
    expect(card.getByText('02.09.2026 09:00')).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it("rotatsiya xatosi tasdiq oynasida ko'rinadi", async () => {
    server.use(
      http.post('/api/tutor/companies/:id/checkin-qr/rotate', () =>
        HttpResponse.json(
          { status: 500, title: 'Server xatosi', detail: 'QR yangilanmadi.' },
          { status: 500, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    const user = userEvent.setup();
    renderCard('tutor');
    const card = within(await screen.findByRole('region', { name: 'Check-in QR kodi' }));
    await card.findByText(mockQrPayload('c1', 0));
    await user.click(card.getByRole('button', { name: 'Yangilash' }));
    const dialog = await screen.findByRole('dialog', { name: 'QR kodni yangilash' });
    await user.click(within(dialog).getByRole('button', { name: 'Yangilash' }));
    expect(await within(dialog).findByText('QR yangilanmadi.')).toBeInTheDocument();
    expect(card.getByText(mockQrPayload('c1', 0))).toBeInTheDocument();
  });

  it("ko'lamdan tashqari korxona → 404 holati", async () => {
    renderCard('tutor', 'c-unknown');
    const card = within(await screen.findByRole('region', { name: 'Check-in QR kodi' }));
    expect(await card.findByText('QR kod topilmadi')).toBeInTheDocument();
    expect(card.queryByRole('button', { name: 'Yangilash' })).not.toBeInTheDocument();
  });

  it('server xatosi → xabar va "Qayta urinish"', async () => {
    let fail = true;
    server.use(
      http.get('/api/admin/companies/:id/checkin-qr', () =>
        fail
          ? HttpResponse.json(
              { status: 500, title: 'Server xatosi', detail: 'Ichki xato.' },
              { status: 500, headers: { 'Content-Type': 'application/problem+json' } },
            )
          : undefined,
      ),
    );
    const user = userEvent.setup();
    renderCard('admin');
    const card = within(await screen.findByRole('region', { name: 'Check-in QR kodi' }));
    expect(await card.findByText('QR kodni yuklab bo‘lmadi')).toBeInTheDocument();
    expect(card.getByText('Ichki xato.')).toBeInTheDocument();

    fail = false;
    await user.click(card.getByRole('button', { name: 'Qayta urinish' }));
    expect(await card.findByText(mockQrPayload('c1', 0))).toBeInTheDocument();
  });

  it('Yuklab olish (PNG) — korxona nomli PNG fayl yuklanadi', async () => {
    const clicked: HTMLAnchorElement[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      clicked.push(this);
    });
    const user = userEvent.setup();
    renderCard('tutor');
    const card = within(await screen.findByRole('region', { name: 'Check-in QR kodi' }));
    await card.findByText(mockQrPayload('c1', 0));
    await user.click(card.getByRole('button', { name: 'Yuklab olish (PNG)' }));
    await waitFor(() => expect(clicked).toHaveLength(1));
    expect(clicked[0]!.download).toBe('checkin-qr-tech-solutions-mchj.png');
    expect(clicked[0]!.href).toMatch(/^data:image\/png;base64,/);
  });
});
