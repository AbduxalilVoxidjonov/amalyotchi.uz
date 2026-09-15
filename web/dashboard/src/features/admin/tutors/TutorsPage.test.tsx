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

/** Forma modalidagi "Fakultetlar" checkbox'i (yorlig'i "KOD — Nom"), yuklanguncha kutadi. */
async function facultyBox(dialog: HTMLElement, label: string) {
  return within(dialog).findByRole('checkbox', { name: label });
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
    // Fakultet ustuni: kodlar ", " bilan, to'liq nomlar title'da; guruhlar alohida ustunda.
    const nodira = rowFor('Nodira Saidova');
    expect(nodira.getByText('AT, IM')).toHaveAttribute(
      'title',
      'Axborot texnologiyalari, Iqtisodiyot va moliya',
    );
    expect(nodira.getByText('412-22, 413-22')).toBeInTheDocument();
    const baxtiyor = rowFor('Baxtiyor Rasulov');
    expect(baxtiyor.getByText('IM')).toHaveAttribute('title', 'Iqtisodiyot va moliya');
    expect(baxtiyor.getByText('5 guruh')).toBeInTheDocument();
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

  it("fakultet filtri: ko'p fakultetli tyutor ikkinchi fakulteti bo'yicha ham topiladi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<TutorsPage />);
    await screen.findByText('Baxtiyor Rasulov');

    const filter = screen.getByLabelText("Fakultet bo'yicha filtr");
    await waitFor(() =>
      expect(
        within(filter).getByRole('option', { name: 'Iqtisodiyot va moliya' }),
      ).toBeInTheDocument(),
    );
    await user.selectOptions(filter, 'f2');

    await waitFor(() => expect(screen.queryByText('Sardor Karimov')).not.toBeInTheDocument());
    // Nodira — f1 + f2; Baxtiyor — faqat f2.
    expect(screen.getByText('Nodira Saidova')).toBeInTheDocument();
    expect(screen.getByText('Baxtiyor Rasulov')).toBeInTheDocument();
    expect(screen.getByText('1–2 / 2')).toBeInTheDocument();
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
    // Fakultetlar — checkbox ro'yxati ("KOD — Nom"), ikkitasi tanlanadi.
    const group = within(dialog).getByRole('group', { name: 'Fakultetlar' });
    await user.click(await facultyBox(dialog, 'QA — Qurilish va arxitektura'));
    await user.click(await facultyBox(dialog, 'FL — Filologiya'));
    expect(
      within(group).getByRole('checkbox', { name: 'QA — Qurilish va arxitektura' }),
    ).toBeChecked();
    expect(
      within(group).getByRole('checkbox', { name: 'AT — Axborot texnologiyalari' }),
    ).not.toBeChecked();
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(await screen.findByText('Malika Tosheva')).toBeInTheDocument();
    expect(rowFor('Malika Tosheva').getByText('+998 90 555-66-77')).toBeInTheDocument();
    // Nom tartibida: Filologiya → Qurilish va arxitektura.
    expect(rowFor('Malika Tosheva').getByText('FL, QA')).toHaveAttribute(
      'title',
      'Filologiya, Qurilish va arxitektura',
    );
    expect(rowFor('Malika Tosheva').getByText("guruh yo'q")).toBeInTheDocument();
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
    expect(within(dialog).getByText('Kamida bitta fakultet tanlang')).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it("yaratish: fakultet tanlanmasa (tanlab, so'ng olib tashlansa) — xato fieldset ostida, so'rov ketmaydi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<TutorsPage />);
    await screen.findByText('Nodira Saidova');

    await user.click(screen.getByRole('button', { name: 'Yangi tyutor' }));
    const dialog = await screen.findByRole('dialog', { name: 'Yangi tyutor' });
    await user.type(within(dialog).getByLabelText('FISH'), 'Malika Tosheva');
    await user.type(within(dialog).getByLabelText('HEMIS ID'), '100000000099');
    await user.type(within(dialog).getByLabelText('Parol'), 'parol12345');
    const box = await facultyBox(dialog, 'AT — Axborot texnologiyalari');
    await user.click(box);
    await user.click(box);
    expect(box).not.toBeChecked();
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    const group = within(dialog).getByRole('group', { name: 'Fakultetlar' });
    expect(within(group).getByRole('alert')).toHaveTextContent('Kamida bitta fakultet tanlang');
    expect(group).toHaveAttribute('aria-invalid', 'true');
    // Boshqa maydonlar to'g'ri — faqat fakultet xatosi.
    expect(within(dialog).getAllByRole('alert')).toHaveLength(1);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.queryByText('Malika Tosheva')).not.toBeInTheDocument();
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
    await user.click(await facultyBox(dialog, 'AT — Axborot texnologiyalari'));
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
    expect(await facultyBox(dialog, 'QA — Qurilish va arxitektura')).toBeChecked();
    expect(await facultyBox(dialog, 'AT — Axborot texnologiyalari')).not.toBeChecked();

    await user.clear(within(dialog).getByLabelText('FISH'));
    await user.type(within(dialog).getByLabelText('FISH'), 'Dilshod Ergashev-Yangi');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(await screen.findByText('Dilshod Ergashev-Yangi')).toBeInTheDocument();
  });

  it("tahrirlash: ko'lami bor fakultetni olib tashlash → 409 modal ichida; ko'lamsiz fakultetni olib tashlash mumkin", async () => {
    const user = userEvent.setup();
    renderWithProviders(<TutorsPage />);
    await screen.findByText('Nodira Saidova');

    await user.click(rowFor('Nodira Saidova').getByRole('button', { name: 'Tahrirlash' }));
    const dialog = await screen.findByRole('dialog', { name: 'Tyutorni tahrirlash' });
    const at = await facultyBox(dialog, 'AT — Axborot texnologiyalari');
    const im = await facultyBox(dialog, 'IM — Iqtisodiyot va moliya');
    expect(at).toBeChecked();
    expect(im).toBeChecked();

    // f1 da ko'lam bor (dir1) — olib tashlab bo'lmaydi.
    await user.click(at);
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));
    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      "Axborot texnologiyalari fakultetida tyutorga ko'lam biriktirilgan — avval uni ajrating.",
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    // f2 da ko'lam yo'q — olib tashlanadi, jadvalda faqat "AT" qoladi.
    await user.click(at);
    await user.click(im);
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() => expect(rowFor('Nodira Saidova').getByText('AT')).toBeInTheDocument());
    expect(rowFor('Nodira Saidova').queryByText('AT, IM')).not.toBeInTheDocument();
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
