import { http, HttpResponse } from 'msw';
import { screen } from '@testing-library/react';
import { server } from '@/mocks/server';
import { mockPlace } from '@/features/place/mocks';
import { renderApp } from '@/test/render-app';

describe('PlacePage (isJoyim)', () => {
  it("korxona ma'lumotlari, shartnoma va geofence", async () => {
    renderApp('/joyim');
    expect(await screen.findByText("Korxona ma'lumotlari")).toBeInTheDocument();
    expect(screen.getByText('Tasdiqlangan')).toBeInTheDocument();
    expect(screen.getByText('304 512 889')).toBeInTheDocument();
    expect(screen.getByText('Islomov B. · +998 90 123 45 67')).toBeInTheDocument();
    expect(screen.getByText('01.10–15.11.2026')).toBeInTheDocument();
    expect(screen.getByText('shartnoma_aliyev.pdf')).toBeInTheDocument();
    expect(screen.getByText('2 bet · 1,8 MB · 24.09.2026 da yuklangan')).toBeInTheDocument();
    expect(screen.getByText('08.10.2026 da tyutor N. Saidova tasdiqladi')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Xarita (nuqta + 150 m doira)' })).toHaveTextContent(
      '41.3111, 69.2797',
    );
  });

  it("revisionNeeded → 'Qayta topshirish' + tyutor izohi; shartnoma yo'q", async () => {
    server.use(
      http.get('/api/student/place', () =>
        HttpResponse.json({
          ...mockPlace,
          status: 'revisionNeeded',
          comment: 'Shartnomada muhr yo‘q',
          mentorName: null,
          mentorPhone: null,
          contract: null,
        }),
      ),
    );
    renderApp('/joyim');
    expect(await screen.findByText('Qayta topshirish')).toBeInTheDocument();
    expect(screen.getByText('Tyutor izohi: Shartnomada muhr yo‘q')).toBeInTheDocument();
    expect(screen.getByText('Shartnoma hali yuklanmagan')).toBeInTheDocument();
    expect(screen.getByText('—')).toBeInTheDocument(); // mentor yo'q
  });

  it('404 → "biriktirilmagan" bo\'sh holati', async () => {
    server.use(
      http.get('/api/student/place', () =>
        HttpResponse.json(
          { status: 404, title: 'Topilmadi', detail: 'Joy yo‘q' },
          { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    renderApp('/joyim');
    expect(await screen.findByText('Amaliyot joyi hali biriktirilmagan')).toBeInTheDocument();
  });
});
