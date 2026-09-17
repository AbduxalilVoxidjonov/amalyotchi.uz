import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '../shared/renderWithProviders';
import { CompaniesPage } from './CompaniesPage';
import { resetCompaniesMock } from './mocks';

/** Nom bo'yicha qatorni topib, shu qator ichida qidirish uchun. */
function rowFor(name: string) {
  const cell = screen.getByText(name);
  const row = cell.closest('[role="row"]');
  if (!row) throw new Error(`"${name}" uchun qator topilmadi`);
  return within(row as HTMLElement);
}

/** "Yangi korxona" formasini to'g'ri qiymatlar bilan to'ldiradi (STIR chaqiruvchidan). */
async function fillForm(
  user: ReturnType<typeof userEvent.setup>,
  dialog: HTMLElement,
  values: { name: string; tin: string },
) {
  await user.type(within(dialog).getByLabelText('Nomi'), values.name);
  await user.type(within(dialog).getByLabelText('STIR'), values.tin);
  await user.type(within(dialog).getByLabelText('Faoliyat turi'), 'Dasturiy ta’minot');
  await user.type(within(dialog).getByLabelText('Manzil'), 'Toshkent, Yunusobod 4');
  await user.type(within(dialog).getByLabelText('Kenglik (lat)'), '41.3500');
  await user.type(within(dialog).getByLabelText('Uzunlik (lng)'), '69.2800');
  await user.type(within(dialog).getByLabelText('Radius (m)'), '180');
  await user.type(within(dialog).getByLabelText('Rahbar FISH'), 'Sobirov Jahongir');
  await user.type(within(dialog).getByLabelText('Rahbar telefoni'), '+998 90 777-88-99');
}

describe('CompaniesPage', () => {
  afterEach(() => {
    resetCompaniesMock();
  });

  it("v3 ro'yxatni ko'rsatadi (STIR formati, radius, belgi)", async () => {
    renderWithProviders(<CompaniesPage />);
    expect(await screen.findByText('Tech Solutions MChJ')).toBeInTheDocument();
    expect(screen.getByText('305 881 204')).toBeInTheDocument();
    expect(screen.getByText('450 m')).toBeInTheDocument();
    expect(screen.getByText('Katta radius')).toBeInTheDocument();
    expect(screen.getByText("Shubhali to'planish")).toBeInTheDocument();
  });

  it("toolbar: eski 'Excel' va 'Shubhali to'planishlar' tugmalari yo'q", async () => {
    renderWithProviders(<CompaniesPage />);
    await screen.findByText('Tech Solutions MChJ');

    expect(screen.queryByRole('button', { name: 'Excel' })).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: "Shubhali to'planishlar" }),
    ).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Yangi korxona' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Shablon' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Excel import' })).toBeInTheDocument();
  });

  it('korxona nomi detail sahifasiga havola qiladi', async () => {
    renderWithProviders(<CompaniesPage />);
    expect(await screen.findByRole('link', { name: 'Tech Solutions MChJ' })).toHaveAttribute(
      'href',
      '/admin/companies/c1',
    );
  });

  it("STIR chegarasidan oshgan korxona ogohlantirish bilan ko'rsatiladi", async () => {
    renderWithProviders(<CompaniesPage />);
    await screen.findByText('Mega Servis MChJ');
    const table = within(screen.getByRole('table', { name: 'Korxonalar' }));
    expect(table.getByText('21/10')).toHaveAttribute('data-status', 'bad');
    expect(table.getByText("Talaba ko'p")).toHaveAttribute('data-status', 'bad');
  });

  it("yaratish: modal → to'ldiriladi → ro'yxatda paydo bo'ladi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<CompaniesPage />);
    await screen.findByText('Tech Solutions MChJ');

    await user.click(screen.getByRole('button', { name: 'Yangi korxona' }));
    const dialog = await screen.findByRole('dialog', { name: 'Yangi korxona' });
    await fillForm(user, dialog, { name: 'Yangi Tex MChJ', tin: '309112233' });
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(await screen.findByText('Yangi Tex MChJ')).toBeInTheDocument();
    expect(rowFor('Yangi Tex MChJ').getByText('309 112 233')).toBeInTheDocument();
    expect(rowFor('Yangi Tex MChJ').getByText('180 m')).toBeInTheDocument();
    expect(screen.getByText('1–6 / 6')).toBeInTheDocument();
  });

  it("yaratish: mijoz validatsiyasi — bo'sh forma yuborilmaydi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<CompaniesPage />);
    await screen.findByText('Tech Solutions MChJ');

    await user.click(screen.getByRole('button', { name: 'Yangi korxona' }));
    const dialog = await screen.findByRole('dialog', { name: 'Yangi korxona' });
    await user.type(within(dialog).getByLabelText('STIR'), '12345');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    expect(within(dialog).getByText('Korxona nomini kiriting.')).toBeInTheDocument();
    expect(
      within(dialog).getByText("STIR 9 ta raqamdan iborat bo'lishi kerak. Namuna: 123456789"),
    ).toBeInTheDocument();
    expect(within(dialog).getByText('Manzilni kiriting.')).toBeInTheDocument();
    expect(within(dialog).getByText('Rahbar telefonini kiriting.')).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('409: STIR takrori → server xabari dialog ichida', async () => {
    const user = userEvent.setup();
    renderWithProviders(<CompaniesPage />);
    await screen.findByText('Tech Solutions MChJ');

    await user.click(screen.getByRole('button', { name: 'Yangi korxona' }));
    const dialog = await screen.findByRole('dialog', { name: 'Yangi korxona' });
    // `c3` — Qurilish Trest 12 ning STIR'i.
    await fillForm(user, dialog, { name: 'Takror Korxona', tin: '305881204' });
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'STIR 305881204 bilan korxona allaqachon mavjud.',
    );
    expect(screen.queryByText('Takror Korxona')).not.toBeInTheDocument();
  });

  it("tahrirlash: forma detail qiymatlari bilan to'ladi → o'zgarish saqlanadi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<CompaniesPage />);
    await screen.findByText('Tech Solutions MChJ');

    await user.click(rowFor('Tech Solutions MChJ').getByRole('button', { name: 'Tahrirlash' }));
    const dialog = await screen.findByRole('dialog', { name: 'Korxonani tahrirlash' });
    await waitFor(() =>
      expect(within(dialog).getByLabelText('Nomi')).toHaveValue('Tech Solutions MChJ'),
    );
    expect(within(dialog).getByLabelText('STIR')).toHaveValue('304512889');
    expect(within(dialog).getByLabelText('Kenglik (lat)')).toHaveValue('41.3111');
    expect(within(dialog).getByLabelText('Rahbar telefoni')).toHaveValue('+998 90 123-45-67');

    await user.clear(within(dialog).getByLabelText('Nomi'));
    await user.type(within(dialog).getByLabelText('Nomi'), 'Tech Solutions Plus');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(await screen.findByText('Tech Solutions Plus')).toBeInTheDocument();
  });

  it("holat almashtirish: 'Faolsizlantirish' → 'Faollashtirish' ga aylanadi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<CompaniesPage />);
    await screen.findByText('Agrobank ATB');

    await user.click(rowFor('Agrobank ATB').getByRole('button', { name: 'Faolsizlantirish' }));
    await waitFor(() =>
      expect(
        rowFor('Agrobank ATB').getByRole('button', { name: 'Faollashtirish' }),
      ).toBeInTheDocument(),
    );
  });

  it("o'chirish: faol korxona uchun 409 xabari tasdiq dialogida", async () => {
    const user = userEvent.setup();
    renderWithProviders(<CompaniesPage />);
    await screen.findByText('Qurilish Trest 12');

    await user.click(rowFor('Qurilish Trest 12').getByRole('button', { name: "O'chirish" }));
    const dialog = await screen.findByRole('dialog', { name: "Korxonani o'chirish" });
    expect(within(dialog).getByText(/«Qurilish Trest 12» korxonasini/)).toBeInTheDocument();

    await user.click(within(dialog).getByRole('button', { name: "O'chirish" }));
    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      "Avval korxonani faolsizlantiring — keyin o'chirish mumkin.",
    );
    expect(screen.getByText('Qurilish Trest 12')).toBeInTheDocument();
  });

  it("o'chirish: talabasi bor faol emas korxona uchun 409 xabari", async () => {
    const user = userEvent.setup();
    renderWithProviders(<CompaniesPage />);
    await screen.findByText("Ipak Yo'li Logistika");

    await user.click(rowFor("Ipak Yo'li Logistika").getByRole('button', { name: "O'chirish" }));
    const dialog = await screen.findByRole('dialog', { name: "Korxonani o'chirish" });
    await user.click(within(dialog).getByRole('button', { name: "O'chirish" }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      "Korxonaga 5 ta talaba biriktirilgan — uni o'chirib bo'lmaydi.",
    );
  });

  it("o'chirish: faolsizlantirilgan talabasiz korxona ro'yxatdan chiqadi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<CompaniesPage />);
    await screen.findByText('Tech Solutions MChJ');

    // Yangi korxona — talabasi yo'q; avval faolsizlantiriladi, so'ng o'chiriladi.
    await user.click(screen.getByRole('button', { name: 'Yangi korxona' }));
    const form = await screen.findByRole('dialog', { name: 'Yangi korxona' });
    await fillForm(user, form, { name: "O'chiriladigan MChJ", tin: '309998877' });
    await user.click(within(form).getByRole('button', { name: 'Saqlash' }));
    await screen.findByText("O'chiriladigan MChJ");

    await user.click(
      rowFor("O'chiriladigan MChJ").getByRole('button', { name: 'Faolsizlantirish' }),
    );
    await waitFor(() =>
      expect(
        rowFor("O'chiriladigan MChJ").getByRole('button', { name: 'Faollashtirish' }),
      ).toBeInTheDocument(),
    );

    await user.click(rowFor("O'chiriladigan MChJ").getByRole('button', { name: "O'chirish" }));
    const dialog = await screen.findByRole('dialog', { name: "Korxonani o'chirish" });
    await user.click(within(dialog).getByRole('button', { name: "O'chirish" }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() => expect(screen.queryByText("O'chiriladigan MChJ")).not.toBeInTheDocument());
  });

  it('Excel import: shablon havolasi va hisobot (qabul qilinmagan qatorlar)', async () => {
    const user = userEvent.setup();
    renderWithProviders(<CompaniesPage />);
    await screen.findByText('Tech Solutions MChJ');

    await user.click(screen.getByRole('button', { name: 'Excel import' }));
    const dialog = await screen.findByRole('dialog', { name: "Korxonalarni Excel'dan yuklash" });
    expect(within(dialog).getByText(/Nomi\*, STIR\*, Faoliyat turi\*/)).toBeInTheDocument();

    const file = new File(['mock'], 'korxonalar.xlsx', {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    });
    await user.upload(within(dialog).getByLabelText(/To.ldirilgan fayl/), file);
    await user.click(within(dialog).getByRole('button', { name: 'Import qilish' }));

    const report = await within(dialog).findByRole('status');
    expect(report).toHaveTextContent('Qo‘shildi: 4');
    expect(report).toHaveTextContent('Qabul qilinmadi: 2');
    expect(
      within(dialog).getByRole('table', { name: /Qabul qilinmagan qatorlar/ }),
    ).toBeInTheDocument();
  });
});
