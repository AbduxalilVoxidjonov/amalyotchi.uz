import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderHierarchyPage } from '../../shared/renderHierarchyPage';
import { resetFacultiesMock } from '../mocks';
import { FacultyDepartmentsPage } from './FacultyDepartmentsPage';
import { resetDepartmentsMock } from './mocks';

function rowFor(name: string) {
  const cell = screen.getByText(name);
  const row = cell.closest('[role="row"]');
  if (!row) throw new Error(`"${name}" uchun qator topilmadi`);
  return within(row as HTMLElement);
}

function renderPage(facultyId = 'f1') {
  return renderHierarchyPage(<FacultyDepartmentsPage />, '/admin/faculties/:facultyId', [
    `/admin/faculties/${facultyId}`,
  ]);
}

describe('FacultyDepartmentsPage', () => {
  afterEach(() => {
    resetDepartmentsMock();
    resetFacultiesMock();
  });

  it("ro'yxat va breadcrumb ko'rsatiladi", async () => {
    renderPage();
    expect(await screen.findByText('Kompyuter injiniringi kafedrasi')).toBeInTheDocument();
    expect(screen.getByText('Axborot xavfsizligi kafedrasi')).toBeInTheDocument();

    const breadcrumb = screen.getByRole('navigation', { name: "Yo'l" });
    expect(within(breadcrumb).getByRole('link', { name: 'Fakultetlar' })).toBeInTheDocument();
    expect(
      within(breadcrumb).getByText('Axborot texnologiyalari', {
        selector: '[aria-current="page"]',
      }),
    ).toBeInTheDocument();
  });

  it("nom havolasi bosilsa ichkariga o'tiladi — URL o'zgaradi", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Kompyuter injiniringi kafedrasi');

    await user.click(screen.getByRole('link', { name: 'Kompyuter injiniringi kafedrasi' }));
    expect(await screen.findByTestId('location')).toHaveTextContent(
      '/admin/faculties/f1/departments/d1',
    );
  });

  it("qatorning nom bo'lmagan katagi bosilsa — yo'nalishlar sahifasiga o'tiladi", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Kompyuter injiniringi kafedrasi');

    // 2-katak — kafedra kodi (havola emas).
    await user.click(rowFor('Kompyuter injiniringi kafedrasi').getAllByRole('cell')[1]!);
    expect(await screen.findByTestId('location')).toHaveTextContent(
      '/admin/faculties/f1/departments/d1',
    );
  });

  it("qatordagi amal tugmasi bosilsa — navigatsiya bo'lmaydi", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Kompyuter injiniringi kafedrasi');

    await user.click(
      rowFor('Kompyuter injiniringi kafedrasi').getByRole('button', { name: 'Faol emas qilish' }),
    );
    await waitFor(() =>
      expect(rowFor('Kompyuter injiniringi kafedrasi').getByText('Faol emas')).toBeInTheDocument(),
    );
    expect(screen.queryByTestId('location')).not.toBeInTheDocument();
  });

  it("yaratish: modal ochiladi → to'ldiriladi → ro'yxatda paydo bo'ladi", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Kompyuter injiniringi kafedrasi');

    await user.click(screen.getByRole('button', { name: 'Yangi kafedra' }));
    const dialog = await screen.findByRole('dialog', { name: 'Yangi kafedra' });
    await user.type(within(dialog).getByLabelText('Nomi'), 'Dasturiy injiniring kafedrasi');
    await user.type(within(dialog).getByLabelText('Kodi'), 'di');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(await screen.findByText('Dasturiy injiniring kafedrasi')).toBeInTheDocument();
    expect(rowFor('Dasturiy injiniring kafedrasi').getByText('DI')).toBeInTheDocument();
  });

  it("tahrirlash: forma boshlang'ich qiymatlar bilan to'ladi → o'zgarish saqlanadi", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Axborot xavfsizligi kafedrasi');

    await user.click(
      rowFor('Axborot xavfsizligi kafedrasi').getByRole('button', { name: 'Tahrirlash' }),
    );
    const dialog = await screen.findByRole('dialog', { name: 'Kafedrani tahrirlash' });
    expect(within(dialog).getByLabelText('Nomi')).toHaveValue('Axborot xavfsizligi kafedrasi');
    expect(within(dialog).getByLabelText('Kodi')).toHaveValue('AX');

    await user.clear(within(dialog).getByLabelText('Nomi'));
    await user.type(within(dialog).getByLabelText('Nomi'), 'Kiberxavfsizlik kafedrasi');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(await screen.findByText('Kiberxavfsizlik kafedrasi')).toBeInTheDocument();
  });

  it('409: kod takrori → server xabari dialog ichida', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Kompyuter injiniringi kafedrasi');

    await user.click(screen.getByRole('button', { name: 'Yangi kafedra' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Nomi'), 'Yana bir kafedra');
    await user.type(within(dialog).getByLabelText('Kodi'), 'KI');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      "'KI' kodli kafedra bu fakultetda allaqachon mavjud.",
    );
  });

  it("holat almashtirish: 'Faol emas qilish' → badge 'Faol emas', qator xiralashadi", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Kompyuter injiniringi kafedrasi');

    await user.click(
      rowFor('Kompyuter injiniringi kafedrasi').getByRole('button', { name: 'Faol emas qilish' }),
    );

    await waitFor(() =>
      expect(rowFor('Kompyuter injiniringi kafedrasi').getByText('Faol emas')).toBeInTheDocument(),
    );
    expect(
      rowFor('Kompyuter injiniringi kafedrasi').getByRole('button', { name: 'Faollashtirish' }),
    ).toBeInTheDocument();
  });

  it("o'chirish: bog'liq yo'nalishlar bo'lsa 409 xabari, qator qoladi", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Kompyuter injiniringi kafedrasi');

    await user.click(
      rowFor('Kompyuter injiniringi kafedrasi').getByRole('button', { name: "O'chirish" }),
    );
    const dialog = await screen.findByRole('dialog', { name: "Kafedrani o'chirish" });
    await user.click(within(dialog).getByRole('button', { name: "O'chirish" }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      "Kafedrada yo'nalishlar bor",
    );
    expect(screen.getByText('Kompyuter injiniringi kafedrasi')).toBeInTheDocument();
  });

  it("o'chirish: bo'sh kafedra → tasdiqlash → qator yo'qoladi", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Axborot xavfsizligi kafedrasi');

    await user.click(
      rowFor('Axborot xavfsizligi kafedrasi').getByRole('button', { name: "O'chirish" }),
    );
    const dialog = await screen.findByRole('dialog', { name: "Kafedrani o'chirish" });
    await user.click(within(dialog).getByRole('button', { name: "O'chirish" }));

    await waitFor(() =>
      expect(screen.queryByText('Axborot xavfsizligi kafedrasi')).not.toBeInTheDocument(),
    );
  });

  it("404: fakultet topilmasa — 'topilmadi' holati + fakultetlarga qaytish", async () => {
    renderPage('unknown');
    expect(await screen.findByText('Fakultet topilmadi.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Fakultetlarga qaytish' })).toHaveAttribute(
      'href',
      '/admin/faculties',
    );
  });
});
