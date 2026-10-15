import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { renderAdminRoute } from '../shared/renderAdminRoute';

describe('DiariesPage (/admin/diaries)', () => {
  it("kundalik kartalari ko'rinadi; ball → Tasdiqlangan (score tasdiqlaydi); tugmalar qulflanadi", async () => {
    const user = userEvent.setup();
    renderAdminRoute('/admin/diaries');
    const card = await screen.findByRole('article', { name: 'Kundalik: Aliyev Akmal' });
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Kundalik hisobotlar');
    expect(within(card).getByText('412-22 · 11.10.2026 · 17:42')).toBeInTheDocument();
    expect(within(card).getByText('Yuborilgan')).toHaveAttribute('data-status', 'info');
    expect(within(card).getByRole('button', { name: 'ekran_surati_1.png' })).toBeInTheDocument();
    expect(within(card).getByText(/So‘rov validatsiyasi/)).toBeInTheDocument();
    expect(screen.getByText('Qayta yozish kerak')).toHaveAttribute('data-status', 'late');

    // Qayta yozilgan karta — backend 409 beradi, tugmalar o'chirilgan.
    const rewritten = screen.getByRole('article', { name: 'Kundalik: Karimov Bekzod' });
    expect(within(rewritten).getByText(/Batafsil yozing/)).toBeInTheDocument();
    expect(within(rewritten).getByRole('button', { name: 'Tasdiqlash' })).toBeDisabled();

    await user.click(within(card).getByRole('button', { name: '4' }));
    await waitFor(() =>
      expect(within(card).getByRole('button', { name: '4' })).toHaveAttribute(
        'aria-pressed',
        'true',
      ),
    );
    expect(within(card).getByText('Tasdiqlangan')).toHaveAttribute('data-status', 'ok');
    expect(within(card).getByRole('button', { name: 'Tasdiqlash' })).toBeDisabled();
  });

  it('Qayta yozishga qaytarish — izohsiz lokal xato, izoh bilan → status rewrite', async () => {
    const user = userEvent.setup();
    renderAdminRoute('/admin/diaries');
    const card = await screen.findByRole('article', { name: 'Kundalik: Yusupova Nilufar' });

    await user.click(within(card).getByRole('button', { name: 'Qayta yozishga qaytarish' }));
    const field = within(card).getByLabelText('Izoh');
    expect(field).toBeInvalid();

    await user.type(field, 'Statistika jadvalini biriktiring');
    await user.click(within(card).getByRole('button', { name: 'Qayta yozishga qaytarish' }));
    await waitFor(() =>
      expect(within(card).getByText('Qayta yozish kerak')).toHaveAttribute('data-status', 'late'),
    );
    expect(within(card).getByText('Admin izohi:')).toBeInTheDocument();
    expect(within(card).getByText('Statistika jadvalini biriktiring')).toBeInTheDocument();
  });

  it('matnsiz PDF hisobot — matn o\'rniga "PDF hisobot" va fayl havolasi; kech yuborilgan belgisi', async () => {
    renderAdminRoute('/admin/diaries');
    const card = await screen.findByRole('article', { name: 'Kundalik: Rahimov Sardor' });
    expect(within(card).getByText('PDF hisobot')).toBeInTheDocument();
    expect(within(card).getByRole('button', { name: 'hisobot_09-10.pdf' })).toBeInTheDocument();
    expect(within(card).getByText('Kech yuborilgan')).toHaveAttribute('data-status', 'late');

    // O'z vaqtida yuborilgan kartada belgi yo'q.
    const onTime = screen.getByRole('article', { name: 'Kundalik: Aliyev Akmal' });
    expect(within(onTime).queryByText('Kech yuborilgan')).not.toBeInTheDocument();
    expect(within(onTime).queryByText('PDF hisobot')).not.toBeInTheDocument();
  });

  it("limit bilan so'raladi; javob limitga teng bo'lsa — \"Ko'proq ko'rsatish\" limitni oshiradi", async () => {
    const user = userEvent.setup();
    const limits: string[] = [];
    server.use(
      http.get('/api/admin/diaries', ({ request }) => {
        const limit = Number(new URL(request.url).searchParams.get('limit'));
        limits.push(String(limit));
        const count = limit === 100 ? 100 : 130;
        return HttpResponse.json(
          Array.from({ length: count }, (_, i) => ({
            id: `bulk-${i}`,
            studentId: `s-${i}`,
            studentName: `Talaba ${i}`,
            group: '412-22',
            date: '2026-10-11',
            submittedAt: '2026-10-11T12:42:00Z',
            submittedLate: false,
            status: 'approved',
            text: 'Matn',
            learned: null,
            files: [],
            score: 5,
            comment: null,
            reviewedAt: '2026-10-11T13:00:00Z',
          })),
        );
      }),
    );
    renderAdminRoute('/admin/diaries');
    await screen.findByRole('article', { name: 'Kundalik: Talaba 99' });
    expect(limits).toEqual(['100']);

    await user.click(screen.getByRole('button', { name: "Ko'proq ko'rsatish" }));
    await screen.findByRole('article', { name: 'Kundalik: Talaba 129' });
    expect(limits).toEqual(['100', '200']);
    // 130 < 200 — boshqa yozuv yo'q, tugma yashiriladi.
    expect(screen.queryByRole('button', { name: "Ko'proq ko'rsatish" })).not.toBeInTheDocument();
  });
});
