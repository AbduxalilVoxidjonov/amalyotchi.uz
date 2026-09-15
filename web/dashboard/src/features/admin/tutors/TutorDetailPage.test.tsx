import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { resetFacultiesMock } from '../faculties/mocks';
import { problemResponse } from '../shared/mockProblem';
import { renderHierarchyPage } from '../shared/renderHierarchyPage';
import { TUTORS_ENDPOINT } from './api';
import { resetTutorsMock } from './mocks';
import { TutorDetailPage } from './TutorDetailPage';

function renderPage(tutorId = 't1') {
  return renderHierarchyPage(<TutorDetailPage />, '/admin/tutors/:tutorId', [
    `/admin/tutors/${tutorId}`,
  ]);
}

function groupsTable() {
  return within(screen.getByRole('table', { name: 'Biriktirilgan guruhlar' }));
}

describe('TutorDetailPage', () => {
  afterEach(() => {
    resetTutorsMock();
    resetFacultiesMock();
  });

  it("sarlavha kartasi, breadcrumb va biriktirilgan guruhlar ko'rsatiladi", async () => {
    renderPage();
    expect(await screen.findByRole('heading', { name: 'Nodira Saidova' })).toBeInTheDocument();

    const breadcrumb = screen.getByRole('navigation', { name: "Yo'l" });
    expect(within(breadcrumb).getByRole('link', { name: 'Tyutorlar' })).toHaveAttribute(
      'href',
      '/admin/tutors',
    );
    expect(
      within(breadcrumb).getByText('Nodira Saidova', { selector: '[aria-current="page"]' }),
    ).toBeInTheDocument();

    const facts = within(screen.getByRole('region', { name: "Tyutor ma'lumotlari" }));
    expect(facts.getByText('+998 90 111-22-33')).toBeInTheDocument();
    expect(facts.getByText('AT · Axborot texnologiyalari')).toBeInTheDocument();
    expect(facts.getByText('Faol')).toBeInTheDocument();
    expect(facts.getByText('20.08.2026 14:00')).toBeInTheDocument();

    expect(groupsTable().getByText('412-22')).toBeInTheDocument();
    expect(groupsTable().getByText('413-22')).toBeInTheDocument();
    expect(groupsTable().getAllByText('Kompyuter injiniringi')).toHaveLength(2);
    expect(groupsTable().getByText('2 guruh · 38 talaba')).toBeInTheDocument();
  });

  it("guruhi yo'q tyutor — bo'sh holat", async () => {
    server.use(
      http.get(`${TUTORS_ENDPOINT}/:id`, () =>
        HttpResponse.json({
          id: 'tx',
          fullName: 'Guruhsiz Tyutor',
          hemisId: '100000000077',
          phone: null,
          facultyId: 'f1',
          facultyCode: 'AT',
          facultyName: 'Axborot texnologiyalari',
          isActive: true,
          lastLoginAt: null,
          createdAt: '2026-09-01T00:00:00+00:00',
          groups: [],
        }),
      ),
    );
    renderPage('tx');
    expect(await screen.findByText('Guruhlar biriktirilmagan')).toBeInTheDocument();
    expect(groupsTable().getByText('0 guruh · 0 talaba')).toBeInTheDocument();
  });

  it('guruh tanlash: band guruh disabled (+ tyutor ismi), yangi guruh tanlanib saqlanadi', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByRole('heading', { name: 'Nodira Saidova' });

    await user.click(screen.getByRole('button', { name: 'Guruhlarni tahrirlash' }));
    const dialog = await screen.findByRole('dialog', { name: 'Guruhlarni tahrirlash' });

    // Kafedra › yo'nalish bo'limlari va joriy tanlov.
    expect(
      await within(dialog).findByRole('group', {
        name: 'Kompyuter injiniringi kafedrasi › Kompyuter injiniringi',
      }),
    ).toBeInTheDocument();
    expect(within(dialog).getByRole('checkbox', { name: /412-22/ })).toBeChecked();
    expect(within(dialog).getByRole('checkbox', { name: /413-22/ })).toBeChecked();
    expect(within(dialog).getByText('Tanlangan: 2')).toBeInTheDocument();

    // Boshqa tyutorga biriktirilgan — disabled + "— Sardor Karimov".
    const taken = within(dialog).getByRole('checkbox', { name: /431-22/ });
    expect(taken).toBeDisabled();
    expect(taken).not.toBeChecked();
    expect(within(dialog).getByText('— Sardor Karimov')).toBeInTheDocument();
    // Faol emas guruh ro'yxatda yo'q; boshqa fakultet guruhlari ham yo'q.
    expect(within(dialog).queryByRole('checkbox', { name: /441-22/ })).not.toBeInTheDocument();
    expect(within(dialog).queryByRole('checkbox', { name: /221-23/ })).not.toBeInTheDocument();

    // Qidiruv.
    await user.type(within(dialog).getByLabelText('Guruh qidirish'), '422');
    expect(within(dialog).queryByRole('checkbox', { name: /412-22/ })).not.toBeInTheDocument();
    await user.click(within(dialog).getByRole('checkbox', { name: /422-23/ }));
    await user.clear(within(dialog).getByLabelText('Guruh qidirish'));
    // Tanlov qidiruvdan keyin ham saqlanadi.
    expect(within(dialog).getByRole('checkbox', { name: /422-23/ })).toBeChecked();
    await user.click(within(dialog).getByRole('checkbox', { name: /413-22/ }));
    expect(within(dialog).getByText('Tanlangan: 2')).toBeInTheDocument();

    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

    expect(await groupsTable().findByText('422-23')).toBeInTheDocument();
    expect(groupsTable().getByText('412-22')).toBeInTheDocument();
    expect(groupsTable().queryByText('413-22')).not.toBeInTheDocument();
    expect(groupsTable().getByText('2 guruh · 36 talaba')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Guruhlar saqlandi.');
  });

  it('guruh tanlash: 409 (guruh boshqa tyutorga biriktirilgan) → xabar modal ichida', async () => {
    const user = userEvent.setup();
    server.use(
      http.put(`${TUTORS_ENDPOINT}/:id/groups`, () =>
        problemResponse(409, 'Ziddiyat', '421-23 guruhi Sardor Karimov tyutoriga biriktirilgan.'),
      ),
    );
    renderPage();
    await screen.findByRole('heading', { name: 'Nodira Saidova' });

    await user.click(screen.getByRole('button', { name: 'Guruhlarni tahrirlash' }));
    const dialog = await screen.findByRole('dialog', { name: 'Guruhlarni tahrirlash' });
    await user.click(await within(dialog).findByRole('checkbox', { name: /421-23/ }));
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      '421-23 guruhi Sardor Karimov tyutoriga biriktirilgan.',
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it("parolni tiklash: tasdiq mos kelmasa xato, mos kelsa 204 → 'Parol yangilandi.'", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByRole('heading', { name: 'Nodira Saidova' });

    await user.click(screen.getByRole('button', { name: 'Parolni tiklash' }));
    const dialog = await screen.findByRole('dialog', { name: 'Parolni tiklash' });
    await user.type(within(dialog).getByLabelText('Yangi parol'), 'yangiparol1');
    await user.type(within(dialog).getByLabelText('Parolni tasdiqlang'), 'yangiparol2');
    await user.click(within(dialog).getByRole('button', { name: "O'rnatish" }));
    expect(within(dialog).getByText('Parollar mos kelmadi.')).toBeInTheDocument();

    await user.clear(within(dialog).getByLabelText('Parolni tasdiqlang'));
    await user.type(within(dialog).getByLabelText('Parolni tasdiqlang'), 'yangiparol1');
    await user.click(within(dialog).getByRole('button', { name: "O'rnatish" }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.getByRole('status')).toHaveTextContent('Parol yangilandi.');
  });

  it('parolni tiklash: qisqa parol mijozda ushlanadi', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByRole('heading', { name: 'Nodira Saidova' });

    await user.click(screen.getByRole('button', { name: 'Parolni tiklash' }));
    const dialog = await screen.findByRole('dialog', { name: 'Parolni tiklash' });
    await user.type(within(dialog).getByLabelText('Yangi parol'), '1234567');
    await user.type(within(dialog).getByLabelText('Parolni tasdiqlang'), '1234567');
    await user.click(within(dialog).getByRole('button', { name: "O'rnatish" }));
    expect(
      within(dialog).getByText("Parol kamida 8 ta belgidan iborat bo'lishi kerak."),
    ).toBeInTheDocument();
  });

  it("holat: 'Faol emas qilish' tasdiq bilan → badge 'Faol emas' → 'Faollashtirish'", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByRole('heading', { name: 'Nodira Saidova' });

    await user.click(screen.getByRole('button', { name: 'Faol emas qilish' }));
    const dialog = await screen.findByRole('dialog', { name: 'Tyutorni faol emas qilish' });
    await user.click(within(dialog).getByRole('button', { name: 'Faol emas qilish' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    const facts = within(screen.getByRole('region', { name: "Tyutor ma'lumotlari" }));
    expect(await facts.findByText('Faol emas')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Faollashtirish' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Tyutor faol emas qilindi.');
  });

  it("tahrirlash: telefon o'zgartiriladi → kartada yangilanadi", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByRole('heading', { name: 'Nodira Saidova' });

    await user.click(screen.getByRole('button', { name: 'Tahrirlash' }));
    const dialog = await screen.findByRole('dialog', { name: 'Tyutorni tahrirlash' });
    expect(within(dialog).getByLabelText('FISH')).toHaveValue('Nodira Saidova');
    await waitFor(() => expect(within(dialog).getByLabelText('Fakultet')).toHaveValue('f1'));
    await user.clear(within(dialog).getByLabelText('Telefon'));
    await user.type(within(dialog).getByLabelText('Telefon'), '+998 90 999-88-77');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(await screen.findByText('+998 90 999-88-77')).toBeInTheDocument();
  });

  it("404: tyutor topilmasa — 'topilmadi' holati + ro'yxatga qaytish", async () => {
    renderPage('unknown');
    expect(await screen.findByText('Tyutor topilmadi.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Tyutorlarga qaytish' })).toHaveAttribute(
      'href',
      '/admin/tutors',
    );
  });
});
