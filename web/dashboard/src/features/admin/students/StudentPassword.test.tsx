import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MOCK_REJECTED_STUDENT_PASSWORD } from '@/features/shared/student-password/mocks';
import { server } from '@/mocks/server';
import { renderHierarchyPage } from '../shared/renderHierarchyPage';
import { StudentDetailPage } from './StudentDetailPage';

function renderPage(studentId: string) {
  return renderHierarchyPage(<StudentDetailPage />, '/admin/students/:studentId', [
    `/admin/students/${studentId}`,
  ]);
}

async function profile(name: string) {
  return within(await screen.findByRole('article', { name: `Talaba: ${name}` }));
}

afterEach(() => vi.unstubAllEnvs());

describe("Admin talaba profili · Parol o'rnatish", () => {
  it("holat belgisi hasPassword dan: 'Parol o'rnatilgan' / 'Parol yo'q'", async () => {
    renderPage('s1');
    const p = await profile('Aliyev Akmal');
    expect(p.getByText("Parol o'rnatilgan")).toBeInTheDocument();
    expect(p.getByRole('button', { name: "Parol o'rnatish" })).toBeInTheDocument();
  });

  it("parol yo'q talaba — belgisi 'Parol yo'q'", async () => {
    renderPage('s2');
    const p = await profile('Sobirov Diyor');
    expect(p.getByText("Parol yo'q")).toBeInTheDocument();
  });

  it('validatsiya: qisqa parol va mos kelmagan tasdiq mijozda ushlanadi', async () => {
    const user = userEvent.setup();
    let posted = 0;
    server.events.on('request:start', ({ request }) => {
      if (request.method === 'POST' && request.url.endsWith('/password')) posted++;
    });
    renderPage('s2');
    await user.click(
      (await profile('Sobirov Diyor')).getByRole('button', { name: "Parol o'rnatish" }),
    );
    const dialog = await screen.findByRole('dialog', { name: "Parol o'rnatish" });

    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));
    expect(within(dialog).getByText('Parolni kiriting.')).toBeInTheDocument();

    await user.type(within(dialog).getByLabelText('Yangi parol'), '1234567');
    await user.type(within(dialog).getByLabelText('Parolni tasdiqlang'), '1234567');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));
    expect(
      within(dialog).getByText("Parol kamida 8 ta belgidan iborat bo'lishi kerak."),
    ).toBeInTheDocument();

    await user.clear(within(dialog).getByLabelText('Yangi parol'));
    await user.type(within(dialog).getByLabelText('Yangi parol'), 'yangiparol1');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));
    expect(within(dialog).getByText('Parollar mos kelmadi.')).toBeInTheDocument();
    expect(posted).toBe(0);
    server.events.removeAllListeners();
  });

  it("generatsiya → ikkala maydon to'ladi, ko'rinadi, nusxalanadi → saqlash → muvaffaqiyat oynasi, belgi yangilanadi", async () => {
    vi.stubEnv('VITE_TWA_URL', 'https://app.amaliyotchi.test/');
    const user = userEvent.setup();
    const writeText = vi.spyOn(navigator.clipboard, 'writeText').mockResolvedValue(undefined);
    let body: unknown = null;
    server.events.on('request:start', async ({ request }) => {
      if (request.method === 'POST' && request.url.endsWith('/api/admin/students/s2/password'))
        body = await request.clone().json();
    });
    renderPage('s2');
    await user.click(
      (await profile('Sobirov Diyor')).getByRole('button', { name: "Parol o'rnatish" }),
    );
    const dialog = await screen.findByRole('dialog', { name: "Parol o'rnatish" });

    const password = within(dialog).getByLabelText('Yangi parol');
    expect(password).toHaveAttribute('type', 'password');
    expect(within(dialog).getByRole('button', { name: 'Nusxalash' })).toBeDisabled();

    await user.click(within(dialog).getByRole('button', { name: 'Parol yaratish' }));
    const generated = (password as HTMLInputElement).value;
    expect(generated).toMatch(/^[A-Za-z2-9]{10}$/);
    expect(within(dialog).getByLabelText('Parolni tasdiqlang')).toHaveValue(generated);
    expect(password).toHaveAttribute('type', 'text');

    await user.click(within(dialog).getByRole('button', { name: 'Nusxalash' }));
    expect(await within(dialog).findByText('Nusxalandi')).toBeInTheDocument();
    expect(writeText).toHaveBeenLastCalledWith(generated);

    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));
    const done = await screen.findByRole('dialog', { name: "Parol o'rnatildi" });
    expect(body).toEqual({ password: generated });
    const text = `HEMIS ID: 341061 · Parol: ${generated} · Kirish: https://app.amaliyotchi.test`;
    expect(within(done).getByTestId('student-credentials')).toHaveTextContent(text);
    expect(
      within(done).getByText("Talaba birinchi kirishda parolni o'zgartiradi."),
    ).toBeInTheDocument();

    await user.click(within(done).getByRole('button', { name: 'Nusxalash' }));
    expect(writeText).toHaveBeenLastCalledWith(text);

    await user.click(within(done).getByRole('button', { name: 'Tayyor' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(
      await (await profile('Sobirov Diyor')).findByText("Parol o'rnatilgan"),
    ).toBeInTheDocument();
    server.events.removeAllListeners();
  });

  it("VITE_TWA_URL bo'lmasa Kirish qatori ko'rsatilmaydi", async () => {
    vi.stubEnv('VITE_TWA_URL', '');
    const user = userEvent.setup();
    renderPage('s2');
    await user.click(
      (await profile('Sobirov Diyor')).getByRole('button', { name: "Parol o'rnatish" }),
    );
    const dialog = await screen.findByRole('dialog', { name: "Parol o'rnatish" });
    await user.type(within(dialog).getByLabelText('Yangi parol'), 'Qwerty2345');
    await user.type(within(dialog).getByLabelText('Parolni tasdiqlang'), 'Qwerty2345');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    const done = await screen.findByRole('dialog', { name: "Parol o'rnatildi" });
    const credentials = within(done).getByTestId('student-credentials');
    expect(credentials).toHaveTextContent('HEMIS ID: 341061 · Parol: Qwerty2345');
    expect(credentials).not.toHaveTextContent('Kirish');
  });

  it("400 ValidationProblem: errors.password maydon ostida ko'rsatiladi, modal ochiq qoladi", async () => {
    const user = userEvent.setup();
    renderPage('s2');
    await user.click(
      (await profile('Sobirov Diyor')).getByRole('button', { name: "Parol o'rnatish" }),
    );
    const dialog = await screen.findByRole('dialog', { name: "Parol o'rnatish" });
    await user.type(within(dialog).getByLabelText('Yangi parol'), MOCK_REJECTED_STUDENT_PASSWORD);
    await user.type(
      within(dialog).getByLabelText('Parolni tasdiqlang'),
      MOCK_REJECTED_STUDENT_PASSWORD,
    );
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    expect(
      await within(dialog).findByText('Parol juda oddiy. Boshqasini tanlang.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('dialog', { name: "Parol o'rnatildi" })).not.toBeInTheDocument();
  });

  it('404 — umumiy xato matni modal ichida', async () => {
    server.use(
      http.post('/api/admin/students/:id/password', () =>
        HttpResponse.json(
          { status: 404, title: 'Topilmadi', detail: 'Talaba topilmadi.' },
          { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    const user = userEvent.setup();
    renderPage('s2');
    await user.click(
      (await profile('Sobirov Diyor')).getByRole('button', { name: "Parol o'rnatish" }),
    );
    const dialog = await screen.findByRole('dialog', { name: "Parol o'rnatish" });
    await user.type(within(dialog).getByLabelText('Yangi parol'), 'Qwerty2345');
    await user.type(within(dialog).getByLabelText('Parolni tasdiqlang'), 'Qwerty2345');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Talaba topilmadi.');
  });
});
