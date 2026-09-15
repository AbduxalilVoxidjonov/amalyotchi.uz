import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { resetDepartmentsMock } from '../departments/mocks';
import { resetDirectionsMock } from '../directions/mocks';
import { resetFacultiesMock } from '../mocks';
import { renderHierarchyPage } from '../../shared/renderHierarchyPage';
import { DirectionGroupsPage } from './DirectionGroupsPage';
import { resetGroupsMock } from './mocks';

function rowFor(name: string) {
  const cell = screen.getByText(name);
  const row = cell.closest('[role="row"]');
  if (!row) throw new Error(`"${name}" uchun qator topilmadi`);
  return within(row as HTMLElement);
}

function renderPage(facultyId = 'f1', departmentId = 'd1', directionId = 'dir1') {
  return renderHierarchyPage(
    <DirectionGroupsPage />,
    '/admin/faculties/:facultyId/departments/:departmentId/directions/:directionId',
    [`/admin/faculties/${facultyId}/departments/${departmentId}/directions/${directionId}`],
  );
}

describe('DirectionGroupsPage', () => {
  afterEach(() => {
    resetGroupsMock();
    resetDirectionsMock();
    resetDepartmentsMock();
    resetFacultiesMock();
  });

  it("ro'yxat va to'liq breadcrumb (Fakultet → Kafedra → Yo'nalish → guruh) ko'rsatiladi", async () => {
    renderPage();
    expect(await screen.findByText('412-22')).toBeInTheDocument();
    expect(screen.getByText('413-22')).toBeInTheDocument();
    expect(screen.getAllByText('Nodira Saidova')).toHaveLength(2);

    const breadcrumb = screen.getByRole('navigation', { name: "Yo'l" });
    expect(within(breadcrumb).getByRole('link', { name: 'Fakultetlar' })).toBeInTheDocument();
    expect(
      within(breadcrumb).getByRole('link', { name: 'Axborot texnologiyalari' }),
    ).toHaveAttribute('href', '/admin/faculties/f1');
    expect(
      within(breadcrumb).getByRole('link', { name: 'Kompyuter injiniringi kafedrasi' }),
    ).toHaveAttribute('href', '/admin/faculties/f1/departments/d1');
    expect(
      within(breadcrumb).getByText('Kompyuter injiniringi', { selector: '[aria-current="page"]' }),
    ).toBeInTheDocument();
  });

  it('tyutorsiz guruh — chiziqcha', async () => {
    renderPage('f1', 'd1', 'dir2');
    expect(await screen.findByText('421-23')).toBeInTheDocument();
    expect(screen.getByText('—')).toBeInTheDocument();
  });

  it("yaratish: modal ochiladi → to'ldiriladi → ro'yxatda paydo bo'ladi", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('412-22');

    await user.click(screen.getByRole('button', { name: 'Yangi guruh' }));
    const dialog = await screen.findByRole('dialog', { name: 'Yangi guruh' });
    await user.type(within(dialog).getByLabelText('Guruh nomi'), '414-22');
    await user.selectOptions(within(dialog).getByLabelText('Kurs'), '3-kurs');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(await screen.findByText('414-22')).toBeInTheDocument();
  });

  it("validatsiya: kurs tanlanmasa — xabar, so'rov yuborilmaydi", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('412-22');

    await user.click(screen.getByRole('button', { name: 'Yangi guruh' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Guruh nomi'), '415-22');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    expect(
      await within(dialog).findByText("Kurs 1–6 oralig'ida bo'lishi kerak."),
    ).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it("tahrirlash: forma boshlang'ich qiymatlar bilan to'ladi → o'zgarish saqlanadi", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('413-22');

    await user.click(rowFor('413-22').getByRole('button', { name: 'Tahrirlash' }));
    const dialog = await screen.findByRole('dialog', { name: 'Guruhni tahrirlash' });
    expect(within(dialog).getByLabelText('Guruh nomi')).toHaveValue('413-22');
    expect(within(dialog).getByLabelText('Kurs')).toHaveValue('3');

    await user.clear(within(dialog).getByLabelText('Guruh nomi'));
    await user.type(within(dialog).getByLabelText('Guruh nomi'), '413-22b');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(await screen.findByText('413-22b')).toBeInTheDocument();
  });

  it('409: guruh nomi takrori → server xabari dialog ichida', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('412-22');

    await user.click(screen.getByRole('button', { name: 'Yangi guruh' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Guruh nomi'), '412-22');
    await user.selectOptions(within(dialog).getByLabelText('Kurs'), '3-kurs');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      "'412-22' guruhi bu yo'nalishda allaqachon mavjud.",
    );
  });

  it('holat almashtirish ishlaydi', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('412-22');

    await user.click(rowFor('412-22').getByRole('button', { name: 'Faol emas qilish' }));
    await waitFor(() => expect(rowFor('412-22').getByText('Faol emas')).toBeInTheDocument());
  });

  it("o'chirish: talabalari bor guruh → 409, qator qoladi", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('412-22');

    await user.click(rowFor('412-22').getByRole('button', { name: "O'chirish" }));
    const dialog = await screen.findByRole('dialog', { name: "Guruhni o'chirish" });
    await user.click(within(dialog).getByRole('button', { name: "O'chirish" }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'Guruhda talabalar yoki biriktirilgan tyutor bor',
    );
    expect(screen.getByText('412-22')).toBeInTheDocument();
  });

  it("o'chirish: talabasiz guruh → tasdiqlash → qator yo'qoladi", async () => {
    const user = userEvent.setup();
    renderPage('f1', 'd1', 'dir2');
    await screen.findByText('421-23');

    await user.click(rowFor('421-23').getByRole('button', { name: "O'chirish" }));
    const dialog = await screen.findByRole('dialog', { name: "Guruhni o'chirish" });
    await user.click(within(dialog).getByRole('button', { name: "O'chirish" }));

    await waitFor(() => expect(screen.queryByText('421-23')).not.toBeInTheDocument());
  });

  it("404: yo'nalish topilmasa — 'topilmadi' holati", async () => {
    renderPage('f1', 'd1', 'unknown');
    expect(await screen.findByText("Yo'nalish topilmadi.")).toBeInTheDocument();
    expect(screen.getByRole('link', { name: "Yo'nalishlarga qaytish" })).toHaveAttribute(
      'href',
      '/admin/faculties/f1/departments/d1',
    );
  });
});
