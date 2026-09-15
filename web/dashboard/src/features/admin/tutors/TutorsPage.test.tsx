import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { resetFacultiesMock } from '../faculties/mocks';
import { renderHierarchyPage } from '../shared/renderHierarchyPage';
import { renderWithProviders } from '../shared/renderWithProviders';
import { resetTutorsMock } from './mocks';
import { TutorsPage } from './TutorsPage';

/** Ism bo'yicha qatorni topib, shu qator ichida qidirish uchun. */
function rowFor(name: string) {
  const cell = screen.getByText(name);
  const row = cell.closest('[role="row"]');
  if (!row) throw new Error(`"${name}" uchun qator topilmadi`);
  return within(row as HTMLElement);
}

describe('TutorsPage', () => {
  afterEach(() => {
    resetTutorsMock();
    resetFacultiesMock();
  });

  it("v2 ro'yxatni ko'rsatadi (telefon, doira, holat)", async () => {
    renderWithProviders(<TutorsPage />);
    expect(await screen.findByText('Baxtiyor Rasulov')).toBeInTheDocument();
    expect(screen.getByText('Kechikmoqda')).toBeInTheDocument();
    expect(screen.getByText('+998 90 111-22-33')).toBeInTheDocument();
    expect(screen.getByText('AT · 412-22, 413-22')).toBeInTheDocument();
    expect(screen.getByText('IM · 5 guruh')).toBeInTheDocument();
    expect(screen.getByText('1–5 / 5')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Yangi tyutor' })).toBeInTheDocument();
  });

  it("tyutor ismi — detail sahifasiga havola; bosilganda URL o'zgaradi", async () => {
    const user = userEvent.setup();
    renderHierarchyPage(<TutorsPage />, '/admin/tutors', ['/admin/tutors']);
    await screen.findByText('Nodira Saidova');

    expect(screen.getByRole('link', { name: 'Nodira Saidova' })).toHaveAttribute(
      'href',
      '/admin/tutors/t1',
    );
    await user.click(screen.getByRole('link', { name: 'Nodira Saidova' }));
    expect(await screen.findByTestId('location')).toHaveTextContent('/admin/tutors/t1');
  });

  it('fakultet filtri: faqat tanlangan fakultet tyutorlari qoladi', async () => {
    const user = userEvent.setup();
    renderWithProviders(<TutorsPage />);
    await screen.findByText('Baxtiyor Rasulov');

    const filter = screen.getByLabelText("Fakultet bo'yicha filtr");
    await waitFor(() =>
      expect(within(filter).getByRole('option', { name: 'Filologiya' })).toBeInTheDocument(),
    );
    await user.selectOptions(filter, 'f1');

    await waitFor(() => expect(screen.queryByText('Baxtiyor Rasulov')).not.toBeInTheDocument());
    expect(screen.getByText('Nodira Saidova')).toBeInTheDocument();
    expect(screen.getByText('Sardor Karimov')).toBeInTheDocument();
    expect(screen.getByText('1–2 / 2')).toBeInTheDocument();

    await user.selectOptions(filter, '');
    expect(await screen.findByText('Baxtiyor Rasulov')).toBeInTheDocument();
  });

  it("yaratish: modal → to'ldiriladi → ro'yxatda paydo bo'ladi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<TutorsPage />);
    await screen.findByText('Nodira Saidova');

    await user.click(screen.getByRole('button', { name: 'Yangi tyutor' }));
    const dialog = await screen.findByRole('dialog', { name: 'Yangi tyutor' });
    await user.type(within(dialog).getByLabelText('FISH'), 'Malika Tosheva');
    await user.type(within(dialog).getByLabelText('HEMIS ID'), '100000000099');
    await user.type(within(dialog).getByLabelText('Telefon'), '+998 90 555-66-77');
    await user.type(within(dialog).getByLabelText('Parol'), 'parol12345');
    const faculty = within(dialog).getByLabelText('Fakultet');
    await waitFor(() => expect(faculty).toBeEnabled());
    await user.selectOptions(faculty, 'f3');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(await screen.findByText('Malika Tosheva')).toBeInTheDocument();
    expect(rowFor('Malika Tosheva').getByText('+998 90 555-66-77')).toBeInTheDocument();
    expect(rowFor('Malika Tosheva').getByText("QA · guruh yo'q")).toBeInTheDocument();
    expect(screen.getByText('1–6 / 6')).toBeInTheDocument();
  });

  it("yaratish: mijoz validatsiyasi — bo'sh forma yuborilmaydi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<TutorsPage />);
    await screen.findByText('Nodira Saidova');

    await user.click(screen.getByRole('button', { name: 'Yangi tyutor' }));
    const dialog = await screen.findByRole('dialog', { name: 'Yangi tyutor' });
    await user.type(within(dialog).getByLabelText('HEMIS ID'), 'abc');
    await user.type(within(dialog).getByLabelText('Parol'), '1234567');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    expect(within(dialog).getByText('FISH ni kiriting.')).toBeInTheDocument();
    expect(
      within(dialog).getByText("HEMIS ID 5–20 ta raqamdan iborat bo'lishi kerak."),
    ).toBeInTheDocument();
    expect(
      within(dialog).getByText("Parol kamida 8 ta belgidan iborat bo'lishi kerak."),
    ).toBeInTheDocument();
    expect(within(dialog).getByText('Fakultetni tanlang.')).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('409: HEMIS ID takrori → server xabari dialog ichida', async () => {
    const user = userEvent.setup();
    renderWithProviders(<TutorsPage />);
    await screen.findByText('Nodira Saidova');

    await user.click(screen.getByRole('button', { name: 'Yangi tyutor' }));
    const dialog = await screen.findByRole('dialog', { name: 'Yangi tyutor' });
    await user.type(within(dialog).getByLabelText('FISH'), 'Takror Tyutor');
    await user.type(within(dialog).getByLabelText('HEMIS ID'), '100000000002');
    await user.type(within(dialog).getByLabelText('Parol'), 'parol12345');
    const faculty = within(dialog).getByLabelText('Fakultet');
    await waitFor(() => expect(faculty).toBeEnabled());
    await user.selectOptions(faculty, 'f1');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'Bu HEMIS ID bilan foydalanuvchi mavjud.',
    );
    expect(screen.queryByText('Takror Tyutor')).not.toBeInTheDocument();
  });

  it("tahrirlash: forma boshlang'ich qiymatlar bilan to'ladi → o'zgarish saqlanadi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<TutorsPage />);
    await screen.findByText('Dilshod Ergashev');

    await user.click(rowFor('Dilshod Ergashev').getByRole('button', { name: 'Tahrirlash' }));
    const dialog = await screen.findByRole('dialog', { name: 'Tyutorni tahrirlash' });
    expect(within(dialog).getByLabelText('FISH')).toHaveValue('Dilshod Ergashev');
    expect(within(dialog).getByLabelText('Telefon')).toHaveValue('+998 93 700-18-45');
    expect(within(dialog).queryByLabelText('HEMIS ID')).not.toBeInTheDocument();
    expect(within(dialog).queryByLabelText('Parol')).not.toBeInTheDocument();
    await waitFor(() => expect(within(dialog).getByLabelText('Fakultet')).toHaveValue('f3'));

    await user.clear(within(dialog).getByLabelText('FISH'));
    await user.type(within(dialog).getByLabelText('FISH'), 'Dilshod Ergashev-Yangi');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(await screen.findByText('Dilshod Ergashev-Yangi')).toBeInTheDocument();
  });

  it("tahrirlash: ko'lami bor tyutor fakultetini o'zgartirish → 409 modal ichida", async () => {
    const user = userEvent.setup();
    renderWithProviders(<TutorsPage />);
    await screen.findByText('Nodira Saidova');

    await user.click(rowFor('Nodira Saidova').getByRole('button', { name: 'Tahrirlash' }));
    const dialog = await screen.findByRole('dialog', { name: 'Tyutorni tahrirlash' });
    const faculty = within(dialog).getByLabelText('Fakultet');
    await waitFor(() => expect(faculty).toHaveValue('f1'));
    await user.selectOptions(faculty, 'f2');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      "Tyutorga ko'lam biriktirilgan — avval uni ajrating.",
    );
  });

  it("holat almashtirish: 'Faol emas qilish' → badge 'Faol emas', so'ng 'Faollashtirish'", async () => {
    const user = userEvent.setup();
    renderWithProviders(<TutorsPage />);
    await screen.findByText('Sardor Karimov');

    await user.click(rowFor('Sardor Karimov').getByRole('button', { name: 'Faol emas qilish' }));
    await waitFor(() =>
      expect(rowFor('Sardor Karimov').getByText('Faol emas')).toBeInTheDocument(),
    );
    expect(
      rowFor('Sardor Karimov').getByRole('button', { name: 'Faollashtirish' }),
    ).toBeInTheDocument();
  });
});
