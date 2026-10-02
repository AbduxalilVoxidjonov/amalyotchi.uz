import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http } from 'msw';
import { server } from '@/mocks/server';
import { problemResponse } from '../shared/mockProblem';
import { renderWithProviders } from '../shared/renderWithProviders';
import { STUDENTS_ENDPOINT } from './api';
import { StudentsPage } from './StudentsPage';

const G1_LABEL = '412-22 · 3-kurs · Kompyuter injiniringi · Axborot texnologiyalari';

async function openForm(entry = '/admin/students', firstRow = 'Aliyev Akmal') {
  const user = userEvent.setup();
  renderWithProviders(<StudentsPage />, [entry]);
  await screen.findByText(firstRow);
  await user.click(screen.getByRole('button', { name: "Talaba qo'shish" }));
  const dialog = within(await screen.findByRole('dialog', { name: "Talaba qo'shish" }));
  return { user, dialog };
}

/** Guruh variantlari yuklanguncha kutib, guruhni tanlaydi. */
async function pickGroup(
  user: ReturnType<typeof userEvent.setup>,
  dialog: ReturnType<typeof within>,
  label = G1_LABEL,
) {
  await dialog.findByRole('option', { name: label });
  await user.selectOptions(dialog.getByLabelText('Guruh'), label);
}

function countPosts() {
  const counter = { n: 0 };
  server.events.on('request:start', ({ request }) => {
    if (request.method === 'POST' && new URL(request.url).pathname === STUDENTS_ENDPOINT)
      counter.n++;
  });
  return counter;
}

describe("Admin talabalar · Talaba qo'shish", () => {
  it("Excel import yonida 'Talaba qo'shish' tugmasi; formada Telegram izohi", async () => {
    const { dialog } = await openForm();
    expect(screen.getByRole('button', { name: 'Excel import' })).toBeInTheDocument();
    expect(
      dialog.getByText(
        "Talaba Telegram orqali kiradi. Brauzerdan kirishi uchun talaba sahifasida parol o'rnatishingiz mumkin.",
      ),
    ).toBeInTheDocument();
    // Parol maydoni yo'q.
    expect(dialog.queryByLabelText(/parol/i)).not.toBeInTheDocument();
  });

  it("majburiy maydonlar: xatolar maydon ostida, fokus birinchi xatoga, so'rov yuborilmaydi", async () => {
    const posts = countPosts();
    const { user, dialog } = await openForm();
    await user.click(dialog.getByRole('button', { name: 'Saqlash' }));

    expect(dialog.getByText("FISH bo'sh.")).toBeInTheDocument();
    expect(dialog.getByText("HEMIS ID bo'sh.")).toBeInTheDocument();
    expect(dialog.getByText("Guruh bo'sh.")).toBeInTheDocument();
    const fullName = dialog.getByLabelText('FISH');
    expect(fullName).toHaveAttribute('aria-invalid', 'true');
    await waitFor(() => expect(fullName).toHaveFocus());
    expect(posts.n).toBe(0);
  });

  it('HEMIS ID harf bilan — xato; noto‘g‘ri telefon — xato', async () => {
    const posts = countPosts();
    const { user, dialog } = await openForm();
    await user.type(dialog.getByLabelText('FISH'), 'Yangi Talaba');
    await user.type(dialog.getByLabelText('HEMIS ID'), '12ab45');
    await user.type(dialog.getByLabelText('Telefon'), '12345');
    await pickGroup(user, dialog);
    await user.click(dialog.getByRole('button', { name: 'Saqlash' }));

    expect(
      dialog.getByText("HEMIS ID 5–20 ta raqamdan iborat bo'lishi kerak."),
    ).toBeInTheDocument();
    expect(dialog.getByText("Telefon raqami noto'g'ri. Namuna: +998901234567")).toBeInTheDocument();
    await waitFor(() => expect(dialog.getByLabelText('HEMIS ID')).toHaveFocus());
    expect(posts.n).toBe(0);
  });

  it("muvaffaqiyat: modal yopiladi, flash + profil havolasi, talaba ro'yxatda paydo bo'ladi", async () => {
    let body: unknown;
    server.events.on('request:start', async ({ request }) => {
      if (request.method === 'POST' && new URL(request.url).pathname === STUDENTS_ENDPOINT)
        body = await request.clone().json();
    });
    const { user, dialog } = await openForm();
    await user.type(dialog.getByLabelText('FISH'), '  Yangi Talaba  ');
    await user.type(dialog.getByLabelText('HEMIS ID'), '399001');
    await user.type(dialog.getByLabelText('Telefon'), '90 111 22 33');
    await pickGroup(user, dialog);
    await user.click(dialog.getByRole('button', { name: 'Saqlash' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(body).toEqual({
      fullName: 'Yangi Talaba',
      hemisId: '399001',
      groupId: 'g1',
      phoneNumber: '+998901112233',
    });
    const flash = await screen.findByText(/«Yangi Talaba» talabalar ro'yxatiga qo'shildi/);
    expect(within(flash).getByRole('link', { name: 'Talaba sahifasini ochish' })).toHaveAttribute(
      'href',
      '/admin/students/s-new-1',
    );
    expect(await screen.findByRole('link', { name: 'Yangi Talaba' })).toBeInTheDocument();
    expect(screen.getByText('1–6 / 6')).toBeInTheDocument();
  });

  it("'Saqlash va yana qo'shish': forma tozalanadi, guruh saqlanadi, modal ochiq qoladi", async () => {
    const { user, dialog } = await openForm();
    await user.type(dialog.getByLabelText('FISH'), 'Birinchi Talaba');
    await user.type(dialog.getByLabelText('HEMIS ID'), '399002');
    await pickGroup(user, dialog);
    await user.click(dialog.getByRole('button', { name: "Saqlash va yana qo'shish" }));

    expect(await dialog.findByText(/«Birinchi Talaba» qo'shildi/)).toBeInTheDocument();
    expect(dialog.getByLabelText('FISH')).toHaveValue('');
    expect(dialog.getByLabelText('HEMIS ID')).toHaveValue('');
    expect(dialog.getByLabelText('Guruh')).toHaveValue('g1');
    await waitFor(() => expect(dialog.getByLabelText('FISH')).toHaveFocus());
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('409 (HEMIS ID band) — xabar HEMIS ID maydoni ostida', async () => {
    const { user, dialog } = await openForm();
    await user.type(dialog.getByLabelText('FISH'), 'Takror Talaba');
    await user.type(dialog.getByLabelText('HEMIS ID'), '341030');
    await pickGroup(user, dialog);
    await user.click(dialog.getByRole('button', { name: 'Saqlash' }));

    expect(
      await dialog.findByText('Bu HEMIS ID bilan talaba allaqachon mavjud.'),
    ).toBeInTheDocument();
    const hemis = dialog.getByLabelText('HEMIS ID');
    expect(hemis).toHaveAttribute('aria-invalid', 'true');
    await waitFor(() => expect(hemis).toHaveFocus());
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('409 (telefon band) — xabar Telefon maydoni ostida', async () => {
    const { user, dialog } = await openForm();
    await user.type(dialog.getByLabelText('FISH'), 'Telefonli Talaba');
    await user.type(dialog.getByLabelText('HEMIS ID'), '399004');
    await user.type(dialog.getByLabelText('Telefon'), '+998 90 123-45-67');
    await pickGroup(user, dialog);
    await user.click(dialog.getByRole('button', { name: 'Saqlash' }));

    expect(
      await dialog.findByText('Bu telefon raqami bilan foydalanuvchi bor.'),
    ).toBeInTheDocument();
    expect(dialog.getByLabelText('Telefon')).toHaveAttribute('aria-invalid', 'true');
  });

  it('400 errors (camelCase) — har biri tegishli maydon ostida', async () => {
    server.use(
      http.post(STUDENTS_ENDPOINT, () =>
        problemResponse(400, "Ma'lumotlar noto'g'ri", "Maydonlar noto'g'ri.", {
          errors: {
            fullName: ['FISH serverda rad etildi.'],
            groupId: ["Bunday faol guruh yo'q."],
            phoneNumber: ['Telefon serverda rad etildi.'],
          },
        }),
      ),
    );
    const { user, dialog } = await openForm();
    await user.type(dialog.getByLabelText('FISH'), 'Server Talaba');
    await user.type(dialog.getByLabelText('HEMIS ID'), '399003');
    await user.type(dialog.getByLabelText('Telefon'), '+998901112244');
    await pickGroup(user, dialog);
    await user.click(dialog.getByRole('button', { name: 'Saqlash' }));

    expect(await dialog.findByText('FISH serverda rad etildi.')).toBeInTheDocument();
    expect(dialog.getByText("Bunday faol guruh yo'q.")).toBeInTheDocument();
    expect(dialog.getByText('Telefon serverda rad etildi.')).toBeInTheDocument();
    expect(dialog.getByLabelText('Guruh')).toHaveAttribute('aria-invalid', 'true');
    await waitFor(() => expect(dialog.getByLabelText('FISH')).toHaveFocus());
  });

  it("sahifa filtrlari guruh tanlovida oldindan qo'yiladi", async () => {
    const { dialog } = await openForm('/admin/students?faculty=f2', 'Ismoilova Madina');
    expect(dialog.getByLabelText('Fakultet')).toHaveValue('f2');
    expect(
      await dialog.findByRole('option', {
        name: '221-23 · 2-kurs · Bank ishi · Iqtisodiyot va moliya',
      }),
    ).toBeInTheDocument();
    expect(dialog.queryByRole('option', { name: G1_LABEL })).not.toBeInTheDocument();
  });
});
