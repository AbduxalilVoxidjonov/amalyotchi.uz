import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { renderTutorRoute } from '../test-utils';
import { mockFaceOf } from './mockStore';

const list = () => screen.findByRole('list', { name: "Yuz rasmlari ro'yxati" });
const card = async (name: string) =>
  within(await list()).findByRole('article', { name: `Yuz rasmi: ${name}` });

describe('FaceEnrollmentsPage (/tutor/face)', () => {
  it("sukut — Kutilmoqda tab'i: rasm, ism, guruh, yuborilgan vaqt, amallar", async () => {
    renderTutorRoute('/tutor/face');
    expect(await screen.findByRole('tab', { name: /Kutilmoqda/ })).toHaveAttribute(
      'aria-selected',
      'true',
    );
    const items = within(await list()).getAllByRole('article');
    // Yangi yuborilgan birinchi.
    expect(items.map((i) => i.getAttribute('aria-label'))).toEqual([
      'Yuz rasmi: Sobirov Diyor',
      'Yuz rasmi: Karimov Bekzod',
    ]);
    const c = await card('Karimov Bekzod');
    expect(within(c).getByRole('link', { name: 'Karimov Bekzod' })).toHaveAttribute(
      'href',
      '/tutor/students/s-341031',
    );
    expect(within(c).getByText('412-22 · HEMIS 341031')).toBeInTheDocument();
    expect(within(c).getByText('Yuborilgan: 11.10.2026 09:05')).toBeInTheDocument();
    expect(
      within(c).getByRole('button', { name: 'Karimov Bekzod etalon yuz rasmi — kattalashtirish' }),
    ).toBeInTheDocument();
    expect(within(c).getByRole('button', { name: 'Tasdiqlash' })).toBeInTheDocument();
    expect(within(c).getByRole('button', { name: 'Rad etish' })).toBeInTheDocument();
  });

  it("Tasdiqlash → POST approve → ro'yxatdan chiqadi, Tasdiqlangan tab'da ko'rinadi", async () => {
    const user = userEvent.setup();
    renderTutorRoute('/tutor/face');
    const c = await card('Karimov Bekzod');
    await user.click(within(c).getByRole('button', { name: 'Tasdiqlash' }));
    await waitFor(() =>
      expect(
        within(screen.getByRole('list', { name: "Yuz rasmlari ro'yxati" })).queryByRole('article', {
          name: /Karimov/,
        }),
      ).not.toBeInTheDocument(),
    );
    expect(mockFaceOf('s-341031').status).toBe('approved');

    await user.click(screen.getByRole('tab', { name: /Tasdiqlangan/ }));
    const approved = await card('Karimov Bekzod');
    expect(within(approved).getByText('Tasdiqlangan')).toBeInTheDocument();
    expect(within(approved).queryByRole('button', { name: 'Tasdiqlash' })).not.toBeInTheDocument();
  });

  it('Rad etish: sabab majburiy (lokal xato) → sabab bilan POST reject', async () => {
    const user = userEvent.setup();
    let body: unknown = null;
    server.events.on('request:start', async ({ request }) => {
      if (request.method === 'POST' && request.url.endsWith('/face/reject')) {
        body = await request.clone().json();
      }
    });
    renderTutorRoute('/tutor/face');
    const c = await card('Sobirov Diyor');
    await user.click(within(c).getByRole('button', { name: 'Rad etish' }));
    const dialog = await screen.findByRole('dialog', { name: 'Yuz rasmini rad etish' });

    await user.click(within(dialog).getByRole('button', { name: 'Rad etish' }));
    expect(within(dialog).getByRole('alert')).toHaveTextContent('Rad etish sababini yozing.');
    expect(body).toBeNull();

    await user.type(within(dialog).getByLabelText('Rad etish sababi'), '  Yuz xira  ');
    await user.click(within(dialog).getByRole('button', { name: 'Rad etish' }));
    await waitFor(() =>
      expect(
        screen.queryByRole('dialog', { name: 'Yuz rasmini rad etish' }),
      ).not.toBeInTheDocument(),
    );
    expect(body).toEqual({ reason: 'Yuz xira' });
    expect(mockFaceOf('s-341032')).toMatchObject({ status: 'rejected', rejectReason: 'Yuz xira' });
    server.events.removeAllListeners();

    await user.click(screen.getByRole('tab', { name: /Rad etilgan/ }));
    const rejected = await card('Sobirov Diyor');
    expect(within(rejected).getByText('Yuz xira')).toBeInTheDocument();
  });

  it('server 409 (allaqachon ko‘rib chiqilgan) → kartada xato matni', async () => {
    const user = userEvent.setup();
    server.use(
      http.post('/api/tutor/students/:id/face/approve', () =>
        HttpResponse.json(
          { status: 409, title: 'Ziddiyat', detail: 'Yuz rasmi allaqachon ko‘rib chiqilgan.' },
          { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    renderTutorRoute('/tutor/face');
    const c = await card('Karimov Bekzod');
    await user.click(within(c).getByRole('button', { name: 'Tasdiqlash' }));
    expect(await within(c).findByRole('alert')).toHaveTextContent(
      'Yuz rasmi allaqachon ko‘rib chiqilgan.',
    );
  });

  it("bo'sh tab — tushunarli bo'sh holat", async () => {
    server.use(http.get('/api/tutor/face-enrollments', () => HttpResponse.json({ items: [] })));
    renderTutorRoute('/tutor/face');
    expect(await screen.findByText("Tekshiruvni kutayotgan yuz rasmi yo'q")).toBeInTheDocument();
  });
});
