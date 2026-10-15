import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderHierarchyPage } from '../shared/renderHierarchyPage';
import { renderWithProviders } from '../shared/renderWithProviders';
import { StudentDetailPage } from './StudentDetailPage';
import { StudentsPage } from './StudentsPage';

const XLSX_TYPE = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

/** v3.34 — "admin talabani kiritdi → talaba ilovadan kiradi" oqimi. */
describe('Talabaning ilovaga kirishi (v3.34)', () => {
  it("ro'yxatda 'Ilova' ustuni va 'Ilovaga kira olmaydi' filtri", async () => {
    const user = userEvent.setup();
    renderWithProviders(<StudentsPage />);
    expect(await screen.findByText('Aliyev Akmal')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Ilova' })).toBeInTheDocument();
    expect(screen.getAllByText('Kira oladi').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Kira olmaydi').length).toBeGreaterThan(0);

    await user.selectOptions(
      screen.getByLabelText('Ilovaga kirish'),
      "Ilovaga kira olmaydi (parol yo'q)",
    );
    await waitFor(() => expect(screen.queryByText('Aliyev Akmal')).not.toBeInTheDocument());
    expect(screen.getByText('Sobirov Diyor')).toBeInTheDocument();
    expect(screen.queryByText('Kira oladi')).not.toBeInTheDocument();
  });

  it('Excel import: parollar yaratiladi — jadval, nusxalash va CSV', async () => {
    const user = userEvent.setup();
    renderWithProviders(<StudentsPage />);
    await user.click(await screen.findByRole('button', { name: 'Excel import' }));
    const dialog = await screen.findByRole('dialog');
    expect(
      within(dialog).getByRole('checkbox', { name: /vaqtinchalik parol yaratish/ }),
    ).toBeChecked();

    await user.upload(
      within(dialog).getByLabelText(/To.ldirilgan fayl/),
      new File(['mock'], 'talabalar.xlsx', { type: XLSX_TYPE }),
    );
    await user.click(within(dialog).getByRole('button', { name: 'Import qilish' }));

    const table = await within(dialog).findByRole('table', { name: /Ilovaga kirish/ });
    expect(within(table).getByText('Ab3cdEfgh4')).toBeInTheDocument();
    expect(within(table).getByText('390001')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Jadvalni nusxalash' })).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'CSV yuklab olish' })).toBeInTheDocument();
    expect(within(dialog).getByText(/parollari faqat hozir ko.rsatiladi/)).toBeInTheDocument();
  });

  it("Excel import: belgi olib tashlansa parollar so'ralmaydi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<StudentsPage />);
    await user.click(await screen.findByRole('button', { name: 'Excel import' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('checkbox', { name: /vaqtinchalik parol yaratish/ }));
    await user.upload(
      within(dialog).getByLabelText(/To.ldirilgan fayl/),
      new File(['mock'], 'talabalar.xlsx', { type: XLSX_TYPE }),
    );
    await user.click(within(dialog).getByRole('button', { name: 'Import qilish' }));
    await within(dialog).findByRole('region', { name: 'Import natijasi' });
    expect(within(dialog).queryByRole('table', { name: /Ilovaga kirish/ })).not.toBeInTheDocument();
  });

  it("profil 'Ilova' kartasi: HEMIS ID ni o'zgartirish — format xatosi, band, muvaffaqiyat", async () => {
    const user = userEvent.setup();
    renderHierarchyPage(<StudentDetailPage />, '/admin/students/:studentId', [
      '/admin/students/s2',
    ]);
    const card = within(await screen.findByRole('region', { name: 'Ilova' }));
    expect(card.getByText('341061')).toBeInTheDocument();
    expect(card.getByRole('region', { name: 'Yuz etaloni' })).toBeInTheDocument();

    await user.click(card.getByRole('button', { name: "HEMIS ID ni o'zgartirish" }));
    const dialog = within(await screen.findByRole('dialog', { name: "HEMIS ID ni o'zgartirish" }));
    const input = dialog.getByLabelText('HEMIS ID');

    await user.clear(input);
    await user.type(input, '12ab');
    await user.click(dialog.getByRole('button', { name: 'Saqlash' }));
    expect(
      dialog.getByText("HEMIS ID 5–20 ta raqamdan iborat bo'lishi kerak."),
    ).toBeInTheDocument();

    await user.clear(input);
    await user.type(input, '341030');
    await user.click(dialog.getByRole('button', { name: 'Saqlash' }));
    expect(
      await dialog.findByText('Bu HEMIS ID bilan talaba allaqachon mavjud.'),
    ).toBeInTheDocument();

    await user.clear(input);
    await user.type(input, '399777');
    await user.click(dialog.getByRole('button', { name: 'Saqlash' }));
    expect(
      await screen.findByText(
        "HEMIS ID o'zgartirildi: 399777. Talaba ilovaga endi shu login bilan kiradi.",
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
