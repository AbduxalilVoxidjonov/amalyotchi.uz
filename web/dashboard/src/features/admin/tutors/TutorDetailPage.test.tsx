import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { resetFacultiesMock } from '../faculties/mocks';
import { problemResponse } from '../shared/mockProblem';
import { renderHierarchyPage } from '../shared/renderHierarchyPage';
import { TUTORS_ENDPOINT, tutorsApi } from './api';
import { NO_ACADEMIC_YEAR_SCOPE_ID, resetTutorsMock } from './mocks';
import { TutorDetailPage } from './TutorDetailPage';

function renderPage(tutorId = 't1') {
  return renderHierarchyPage(<TutorDetailPage />, '/admin/tutors/:tutorId', [
    `/admin/tutors/${tutorId}`,
  ]);
}

function scopesTable() {
  return within(screen.getByRole('table', { name: "Biriktirilgan ko'lam" }));
}

function groupsTable() {
  return within(screen.getByRole('table', { name: 'Qamrab olingan guruhlar' }));
}

async function openScopePicker(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('button', { name: "Guruhlarni biriktirish" }));
  const dialog = await screen.findByRole('dialog', { name: "Guruhlarni biriktirish" });
  // Daraxt yuklanguncha.
  await within(dialog).findByRole('checkbox', { name: 'Axborot texnologiyalari (Fakultet)' });
  return dialog;
}

describe('TutorDetailPage', () => {
  afterEach(() => {
    resetTutorsMock();
    resetFacultiesMock();
  });

  it("sarlavha kartasi, breadcrumb, ko'lam jadvali va qamrab olingan guruhlar ko'rsatiladi", async () => {
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

    // Ko'lam jadvali: daraja badge + nom + yo'l + guruh/talaba.
    const scopes = scopesTable();
    expect(scopes.getByText("Yo'nalish")).toBeInTheDocument();
    expect(scopes.getByText('Kompyuter injiniringi')).toBeInTheDocument();
    expect(
      scopes.getByText('Axborot texnologiyalari › Kompyuter injiniringi kafedrasi'),
    ).toBeInTheDocument();
    expect(scopes.getByText('38')).toBeInTheDocument();
    expect(scopes.getByText("1 ko'lam · 2 guruh · 38 talaba")).toBeInTheDocument();

    // Qamrab olingan guruhlar (ko'lamdan yoyilgan).
    expect(screen.getByText('Qamrab olingan guruhlar (2)')).toBeInTheDocument();
    expect(groupsTable().getByText('412-22')).toBeInTheDocument();
    expect(groupsTable().getByText('413-22')).toBeInTheDocument();
    expect(groupsTable().getAllByText('Kompyuter injiniringi')).toHaveLength(2);
  });

  it("ko'lami yo'q tyutor — bo'sh holat", async () => {
    server.use(
      http.get(`${TUTORS_ENDPOINT}/:id`, () =>
        HttpResponse.json({
          id: 'tx',
          fullName: "Ko'lamsiz Tyutor",
          hemisId: '100000000077',
          phone: null,
          facultyId: 'f1',
          facultyCode: 'AT',
          facultyName: 'Axborot texnologiyalari',
          isActive: true,
          lastLoginAt: null,
          createdAt: '2026-09-01T00:00:00+00:00',
          scopes: [],
          groups: [],
        }),
      ),
    );
    renderPage('tx');
    expect(await screen.findByText("Ko'lam biriktirilmagan")).toBeInTheDocument();
    expect(scopesTable().getByText("0 ko'lam · 0 guruh · 0 talaba")).toBeInTheDocument();
    expect(screen.getByText('Qamrab olingan guruhlar (0)')).toBeInTheDocument();
  });

  it("ko'lam tanlash: ota orqali qamrash, boshqa tyutor tugunlari (o'zi/avlod/ajdod) disabled, qidiruv, saqlash", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByRole('heading', { name: 'Nodira Saidova' });
    const dialog = await openScopePicker(user);
    const box = (name: string) => within(dialog).getByRole('checkbox', { name });

    // Boshlang'ich tanlov — `detail.scopes` (dir1); avlodlari "ota orqali" qamrab olingan.
    expect(box("Kompyuter injiniringi (Yo'nalish)")).toBeChecked();
    expect(box("Kompyuter injiniringi (Yo'nalish)")).toBeEnabled();
    expect(box('412-22 (Guruh)')).toBeChecked();
    expect(box('412-22 (Guruh)')).toBeDisabled();
    expect(box('412-22 (Guruh)')).toHaveAccessibleDescription('— ota orqali');
    expect(within(dialog).getAllByText('— ota orqali')).toHaveLength(2);
    expect(within(dialog).getByText("Tanlangan: 1 ta ko'lam · ~2 guruh")).toBeInTheDocument();

    // Boshqa tyutor kafedrasi: o'zi disabled "— FISH", avlodlari "— FISH orqali".
    const takenDept = box("Sun'iy intellekt kafedrasi (Kafedra)");
    expect(takenDept).toBeDisabled();
    expect(takenDept).not.toBeChecked();
    expect(takenDept).toHaveAccessibleDescription('— Sardor Karimov');
    expect(box("Sun'iy intellekt (Yo'nalish)")).toBeDisabled();
    expect(box("Sun'iy intellekt (Yo'nalish)")).toHaveAccessibleDescription(
      '— Sardor Karimov orqali',
    );
    expect(box('451-23 (Guruh)')).toBeDisabled();
    // Boshqa tyutor guruhi: o'zi disabled, ajdodlari (yo'nalish, kafedra, fakultet) ham — kesishuv.
    expect(box('431-22 (Guruh)')).toBeDisabled();
    expect(box('431-22 (Guruh)')).toHaveAccessibleDescription('— Sardor Karimov');
    expect(box("Axborot xavfsizligi (Yo'nalish)")).toBeDisabled();
    expect(box("Axborot xavfsizligi (Yo'nalish)")).toHaveAccessibleDescription(
      'ichida Sardor Karimov biriktirilgan',
    );
    expect(box('Axborot xavfsizligi kafedrasi (Kafedra)')).toBeDisabled();
    expect(box('Axborot texnologiyalari (Fakultet)')).toBeDisabled();
    expect(box('Axborot texnologiyalari (Fakultet)')).toHaveAccessibleDescription(
      'ichida Sardor Karimov biriktirilgan',
    );
    // Band tugun yonidagi erkin guruh tanlanadi; faol emas guruh daraxtda yo'q.
    expect(box('432-22 (Guruh)')).toBeEnabled();
    expect(
      within(dialog).queryByRole('checkbox', { name: '441-22 (Guruh)' }),
    ).not.toBeInTheDocument();

    // Kafedra tanlansa — ichidagi yo'nalish/guruhlar "ota orqali" (avvalgi dir1 tanlovi normalizatsiya qilinadi).
    await user.click(box('Kompyuter injiniringi kafedrasi (Kafedra)'));
    expect(box("Kompyuter injiniringi (Yo'nalish)")).toBeChecked();
    expect(box("Kompyuter injiniringi (Yo'nalish)")).toBeDisabled();
    expect(box('422-23 (Guruh)')).toBeChecked();
    expect(box('422-23 (Guruh)')).toBeDisabled();
    expect(within(dialog).getAllByText('— ota orqali')).toHaveLength(6);
    expect(within(dialog).getByText("Tanlangan: 1 ta ko'lam · ~4 guruh")).toBeInTheDocument();

    // Yopish/ochish.
    await user.click(
      within(dialog).getByRole('button', { name: 'Kompyuter injiniringi kafedrasi — yopish' }),
    );
    expect(
      within(dialog).queryByRole('checkbox', { name: "Kompyuter injiniringi (Yo'nalish)" }),
    ).not.toBeInTheDocument();
    await user.click(
      within(dialog).getByRole('button', { name: 'Kompyuter injiniringi kafedrasi — ochish' }),
    );
    expect(box("Kompyuter injiniringi (Yo'nalish)")).toBeInTheDocument();

    // Qidiruv: mos tugun va ajdodlari qoladi.
    await user.type(within(dialog).getByLabelText('Tugun qidirish'), '432');
    expect(
      within(dialog).queryByRole('checkbox', { name: '412-22 (Guruh)' }),
    ).not.toBeInTheDocument();
    expect(box('Axborot xavfsizligi kafedrasi (Kafedra)')).toBeInTheDocument();
    await user.click(box('432-22 (Guruh)'));
    await user.clear(within(dialog).getByLabelText('Tugun qidirish'));
    // Tanlov qidiruvdan keyin ham saqlanadi.
    expect(box('432-22 (Guruh)')).toBeChecked();
    expect(within(dialog).getByText("Tanlangan: 2 ta ko'lam · ~5 guruh")).toBeInTheDocument();

    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

    // Jadval yangilandi: faqat aniq tanlangan tugunlar (kafedra + guruh), guruhlar yoyilgan.
    const scopes = scopesTable();
    expect(await scopes.findByText('Kompyuter injiniringi kafedrasi')).toBeInTheDocument();
    expect(scopes.getByText('Kafedra')).toBeInTheDocument();
    expect(scopes.getByText('432-22')).toBeInTheDocument();
    expect(scopes.getByText('Guruh')).toBeInTheDocument();
    expect(
      scopes.getByText(
        'Axborot texnologiyalari › Axborot xavfsizligi kafedrasi › Axborot xavfsizligi',
      ),
    ).toBeInTheDocument();
    expect(scopes.queryByText("Yo'nalish")).not.toBeInTheDocument();
    expect(scopes.getByText("2 ko'lam · 5 guruh · 73 talaba")).toBeInTheDocument();
    expect(screen.getByText('Qamrab olingan guruhlar (5)')).toBeInTheDocument();
    expect(groupsTable().getByText('422-23')).toBeInTheDocument();
    expect(groupsTable().getByText('432-22')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent("Ko'lam saqlandi.");
  });

  it("ko'lam tanlash: 409 (tugun boshqa tyutorga biriktirilgan) → xabar modal ichida", async () => {
    const user = userEvent.setup();
    server.use(
      http.put(`${TUTORS_ENDPOINT}/:id/scopes`, () =>
        problemResponse(
          409,
          'Ziddiyat',
          "Dasturiy injiniring (yo'nalish) Sardor Karimov tyutoriga biriktirilgan.",
        ),
      ),
    );
    renderPage();
    await screen.findByRole('heading', { name: 'Nodira Saidova' });
    const dialog = await openScopePicker(user);

    await user.click(
      within(dialog).getByRole('checkbox', { name: "Dasturiy injiniring (Yo'nalish)" }),
    );
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      "Dasturiy injiniring (yo'nalish) Sardor Karimov tyutoriga biriktirilgan.",
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

describe('tutors mock: PUT /scopes', () => {
  afterEach(() => resetTutorsMock());

  it('kesishuv 409: boshqa tyutor tugunining ajdodi yoki avlodi tanlansa', async () => {
    // d2 ichida Sardor Karimovning g-431-22 guruhi bor.
    await expect(
      tutorsApi.setScopes('t1', [{ level: 'department', id: 'd2' }]),
    ).rejects.toMatchObject({
      status: 409,
      message: '431-22 (guruh) Sardor Karimov tyutoriga biriktirilgan.',
    });
    // d5 kafedrasi Sardor Karimovniki — ichidagi guruh ham band.
    await expect(
      tutorsApi.setScopes('t1', [{ level: 'group', id: 'g-451-23' }]),
    ).rejects.toMatchObject({
      status: 409,
      message: "Sun'iy intellekt kafedrasi (kafedra) Sardor Karimov tyutoriga biriktirilgan.",
    });
  });

  it('normalizatsiya: ota va avlod birga yuborilsa faqat ota saqlanadi; boshqa fakultet tuguni 400', async () => {
    const detail = await tutorsApi.setScopes('t1', [
      { level: 'group', id: 'g-412-22' },
      { level: 'department', id: 'd1' },
      { level: 'direction', id: 'dir2' },
    ]);
    expect(detail.scopes.map((s) => `${s.level}:${s.departmentId ?? s.groupId}`)).toEqual([
      'department:d1',
    ]);
    expect(detail.groups.map((g) => g.groupName)).toEqual(['412-22', '413-22', '421-23', '422-23']);

    await expect(
      tutorsApi.setScopes('t1', [{ level: 'group', id: 'g-221-23' }]),
    ).rejects.toMatchObject({ status: 400 });
    await expect(
      tutorsApi.setScopes('t1', [{ level: 'group', id: NO_ACADEMIC_YEAR_SCOPE_ID }]),
    ).rejects.toMatchObject({ status: 409, message: "Faol o'quv yili yo'q." });
  });
});
