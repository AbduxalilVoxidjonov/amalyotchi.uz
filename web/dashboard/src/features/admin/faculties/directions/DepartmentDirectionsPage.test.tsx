import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { resetDepartmentsMock } from '../departments/mocks';
import { resetFacultiesMock } from '../mocks';
import { renderHierarchyPage } from '../../shared/renderHierarchyPage';
import { DepartmentDirectionsPage } from './DepartmentDirectionsPage';
import { resetDirectionsMock } from './mocks';

function rowFor(name: string) {
  const cell = screen.getByText(name);
  const row = cell.closest('[role="row"]');
  if (!row) throw new Error(`"${name}" uchun qator topilmadi`);
  return within(row as HTMLElement);
}

function renderPage(facultyId = 'f1', departmentId = 'd1') {
  return renderHierarchyPage(
    <DepartmentDirectionsPage />,
    '/admin/faculties/:facultyId/departments/:departmentId',
    [`/admin/faculties/${facultyId}/departments/${departmentId}`],
  );
}

describe('DepartmentDirectionsPage', () => {
  afterEach(() => {
    resetDirectionsMock();
    resetDepartmentsMock();
    resetFacultiesMock();
  });

  it("ro'yxat va to'liq breadcrumb (Fakultet → Kafedra → Yo'nalish) ko'rsatiladi", async () => {
    renderPage();
    expect(await screen.findByText('Kompyuter injiniringi')).toBeInTheDocument();
    expect(screen.getByText('Dasturiy injiniring')).toBeInTheDocument();

    const breadcrumb = screen.getByRole('navigation', { name: "Yo'l" });
    expect(within(breadcrumb).getByRole('link', { name: 'Fakultetlar' })).toBeInTheDocument();
    expect(
      within(breadcrumb).getByRole('link', { name: 'Axborot texnologiyalari' }),
    ).toHaveAttribute('href', '/admin/faculties/f1');
    expect(
      within(breadcrumb).getByText('Kompyuter injiniringi kafedrasi', {
        selector: '[aria-current="page"]',
      }),
    ).toBeInTheDocument();
  });

  it("qatorga bosib ichkariga o'tish — URL o'zgaradi", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Dasturiy injiniring');

    await user.click(screen.getByRole('link', { name: 'Dasturiy injiniring' }));
    expect(await screen.findByTestId('location')).toHaveTextContent(
      '/admin/faculties/f1/departments/d1/directions/dir2',
    );
  });

  it("yaratish: modal ochiladi → to'ldiriladi → ro'yxatda paydo bo'ladi", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Kompyuter injiniringi');

    await user.click(screen.getByRole('button', { name: "Yangi yo'nalish" }));
    const dialog = await screen.findByRole('dialog', { name: "Yangi yo'nalish" });
    await user.type(within(dialog).getByLabelText('Nomi'), 'Axborot tizimlari');
    await user.type(within(dialog).getByLabelText('Kodi'), 'at-b');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(await screen.findByText('Axborot tizimlari')).toBeInTheDocument();
    expect(rowFor('Axborot tizimlari').getByText('AT-B')).toBeInTheDocument();
  });

  it('409: kod takrori → server xabari dialog ichida', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Kompyuter injiniringi');

    await user.click(screen.getByRole('button', { name: "Yangi yo'nalish" }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Nomi'), 'Yana bir yonalish');
    await user.type(within(dialog).getByLabelText('Kodi'), 'KI-B');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      "'KI-B' kodli yo'nalish bu kafedrada allaqachon mavjud.",
    );
  });

  it("o'chirish: guruhlari bor yo'nalish → 409, qator qoladi", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Kompyuter injiniringi');

    await user.click(rowFor('Kompyuter injiniringi').getByRole('button', { name: "O'chirish" }));
    const dialog = await screen.findByRole('dialog', { name: "Yo'nalishni o'chirish" });
    await user.click(within(dialog).getByRole('button', { name: "O'chirish" }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent("Yo'nalishda guruhlar bor");
    expect(screen.getByText('Kompyuter injiniringi')).toBeInTheDocument();
  });

  it('holat almashtirish ishlaydi', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Kompyuter injiniringi');

    await user.click(
      rowFor('Kompyuter injiniringi').getByRole('button', { name: 'Faol emas qilish' }),
    );
    await waitFor(() =>
      expect(rowFor('Kompyuter injiniringi').getByText('Faol emas')).toBeInTheDocument(),
    );
  });

  it("o'chirish: guruhi yo'q yo'nalish → tasdiqlash → qator yo'qoladi", async () => {
    const user = userEvent.setup();
    renderPage('f2', 'd3');
    await screen.findByText('Moliyaviy menejment');

    await user.click(rowFor('Moliyaviy menejment').getByRole('button', { name: "O'chirish" }));
    const dialog = await screen.findByRole('dialog', { name: "Yo'nalishni o'chirish" });
    await user.click(within(dialog).getByRole('button', { name: "O'chirish" }));

    await waitFor(() => expect(screen.queryByText('Moliyaviy menejment')).not.toBeInTheDocument());
  });

  it("404: kafedra topilmasa — 'topilmadi' holati", async () => {
    renderPage('f1', 'unknown');
    expect(await screen.findByText('Kafedra topilmadi.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Kafedralarga qaytish' })).toHaveAttribute(
      'href',
      '/admin/faculties/f1',
    );
  });
});
