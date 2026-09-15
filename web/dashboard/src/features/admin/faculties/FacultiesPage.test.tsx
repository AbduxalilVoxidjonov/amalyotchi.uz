import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { renderWithProviders } from '../shared/renderWithProviders';
import { FACULTIES_ENDPOINT } from './api';
import { FacultiesPage } from './FacultiesPage';
import { resetFacultiesMock } from './mocks';

/** Nom bo'yicha qator elementini topish. */
function rowElementFor(name: string): HTMLElement {
  const cell = screen.getByText(name);
  const row = cell.closest('[role="row"]');
  if (!row) throw new Error(`"${name}" uchun qator topilmadi`);
  return row as HTMLElement;
}

/** Nom bo'yicha qatorni topib, shu qator ichida qidirish uchun. */
function rowFor(name: string) {
  return within(rowElementFor(name));
}

describe('FacultiesPage', () => {
  afterEach(() => resetFacultiesMock());

  it("mock ro'yxatni ko'rsatadi va qidiradi (debounce)", async () => {
    const user = userEvent.setup();
    renderWithProviders(<FacultiesPage />);
    expect(await screen.findByText('Axborot texnologiyalari')).toBeInTheDocument();
    expect(screen.getByText("E'tibor")).toBeInTheDocument();
    expect(screen.getByText('1–4 / 4')).toBeInTheDocument();

    await user.type(screen.getByLabelText('Qidirish'), 'filolog');
    await waitFor(() =>
      expect(screen.queryByText('Axborot texnologiyalari')).not.toBeInTheDocument(),
    );
    expect(screen.getByText('Filologiya')).toBeInTheDocument();
    expect(screen.getByText('1–1 / 1')).toBeInTheDocument();
  });

  it('fakultet nomi — kafedralarga kiradigan havola', async () => {
    renderWithProviders(<FacultiesPage />);
    await screen.findByText('Axborot texnologiyalari');
    expect(screen.getByRole('link', { name: 'Axborot texnologiyalari' })).toHaveAttribute(
      'href',
      '/admin/faculties/f1',
    );
  });

  it("bo'sh javob → EmptyState", async () => {
    server.use(
      http.get(FACULTIES_ENDPOINT, () =>
        HttpResponse.json({ items: [], page: 1, pageSize: 20, total: 0 }),
      ),
    );
    renderWithProviders(<FacultiesPage />);
    expect(await screen.findByText("Fakultetlar yo'q")).toBeInTheDocument();
    expect(screen.getByText('0–0 / 0')).toBeInTheDocument();
  });

  it("yaratish: modal ochiladi → to'ldiriladi → ro'yxatda paydo bo'ladi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<FacultiesPage />);
    await screen.findByText('Axborot texnologiyalari');

    await user.click(screen.getByRole('button', { name: 'Yangi fakultet' }));
    const dialog = await screen.findByRole('dialog', { name: 'Yangi fakultet' });
    await user.type(within(dialog).getByLabelText('Nomi'), 'Sanoat muhandisligi');
    await user.type(within(dialog).getByLabelText('Kodi'), 'sm');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(await screen.findByText('Sanoat muhandisligi')).toBeInTheDocument();
    // Kod avtomatik UPPERCASE ko'rsatiladi.
    expect(rowFor('Sanoat muhandisligi').getByText('Faol')).toBeInTheDocument();
  });

  it("validatsiya: bo'sh maydonlar bilan submit → xabarlar, so'rov yuborilmaydi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<FacultiesPage />);
    await screen.findByText('Axborot texnologiyalari');

    await user.click(screen.getByRole('button', { name: 'Yangi fakultet' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    expect(await within(dialog).findByText('Fakultet nomini kiriting.')).toBeInTheDocument();
    expect(within(dialog).getByText('Fakultet kodini kiriting.')).toBeInTheDocument();
    // Modal hali ochiq — so'rov yuborilmagan.
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('409: kod takrori → server xabari dialog ichida', async () => {
    const user = userEvent.setup();
    renderWithProviders(<FacultiesPage />);
    await screen.findByText('Axborot texnologiyalari');

    await user.click(screen.getByRole('button', { name: 'Yangi fakultet' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Nomi'), 'Yana bir fakultet');
    await user.type(within(dialog).getByLabelText('Kodi'), 'AT');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      "'AT' kodli fakultet allaqachon mavjud.",
    );
  });

  it("tahrirlash: forma boshlang'ich qiymatlar bilan to'ladi → o'zgarish saqlanadi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<FacultiesPage />);
    await screen.findByText('Qurilish va arxitektura');

    await user.click(rowFor('Qurilish va arxitektura').getByRole('button', { name: 'Tahrirlash' }));
    const dialog = await screen.findByRole('dialog', { name: 'Fakultetni tahrirlash' });
    expect(within(dialog).getByLabelText('Nomi')).toHaveValue('Qurilish va arxitektura');
    expect(within(dialog).getByLabelText('Kodi')).toHaveValue('QA');

    await user.clear(within(dialog).getByLabelText('Nomi'));
    await user.type(within(dialog).getByLabelText('Nomi'), 'Qurilish muhandisligi');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(await screen.findByText('Qurilish muhandisligi')).toBeInTheDocument();
  });

  it("holat almashtirish: 'Faol emas qilish' → badge 'Faol emas', qator xiralashadi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<FacultiesPage />);
    await screen.findByText('Axborot texnologiyalari');

    await user.click(
      rowFor('Axborot texnologiyalari').getByRole('button', { name: 'Faol emas qilish' }),
    );

    await waitFor(() =>
      expect(rowFor('Axborot texnologiyalari').getByText('Faol emas')).toBeInTheDocument(),
    );
    expect(rowElementFor('Axborot texnologiyalari')).toHaveAttribute('data-row-dim', 'true');
    expect(
      rowFor('Axborot texnologiyalari').getByRole('button', { name: 'Faollashtirish' }),
    ).toBeInTheDocument();
  });

  it("o'chirish: tasdiqlash → qator yo'qoladi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<FacultiesPage />);
    await screen.findByText('Filologiya');

    await user.click(rowFor('Filologiya').getByRole('button', { name: "O'chirish" }));
    const dialog = await screen.findByRole('dialog', { name: "Fakultetni o'chirish" });
    expect(dialog).toHaveTextContent('«Filologiya»');
    await user.click(within(dialog).getByRole('button', { name: "O'chirish" }));

    await waitFor(() => expect(screen.queryByText('Filologiya')).not.toBeInTheDocument());
  });

  it("o'chirish: bog'liq yozuvlar bo'lsa 409 xabari dialog ichida, qator qoladi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<FacultiesPage />);
    await screen.findByText('Iqtisodiyot va moliya');

    await user.click(rowFor('Iqtisodiyot va moliya').getByRole('button', { name: "O'chirish" }));
    const dialog = await screen.findByRole('dialog', { name: "Fakultetni o'chirish" });
    await user.click(within(dialog).getByRole('button', { name: "O'chirish" }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'Fakultetga guruhlar, tyutorlar yoki talabalar biriktirilgan',
    );
    expect(screen.getByText('Iqtisodiyot va moliya')).toBeInTheDocument();
  });
});
