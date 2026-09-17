import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http } from 'msw';
import { server } from '@/mocks/server';
import { problemResponse } from '../shared/mockProblem';
import { renderWithProviders } from '../shared/renderWithProviders';
import { STUDENTS_IMPORT_ENDPOINT } from './api';
import { StudentsPage } from './StudentsPage';

const XLSX_TYPE = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

function xlsx(name = 'talabalar.xlsx') {
  return new File(['mock'], name, { type: XLSX_TYPE });
}

describe('StudentsPage', () => {
  it("mock ro'yxatni ko'rsatadi (holatlar, ulanmagan korxona)", async () => {
    renderWithProviders(<StudentsPage />);
    expect(await screen.findByText('Aliyev Akmal')).toBeInTheDocument();
    expect(screen.getByText('Qizil bayroq')).toBeInTheDocument();
    expect(screen.getByText('Ulanmagan')).toBeInTheDocument();
    expect(screen.getByText('—')).toBeInTheDocument();
    expect(screen.getByText('1–5 / 5')).toBeInTheDocument();
  });

  it("toolbar'da Shablon va Excel import bor, HEMIS tugmasi yo'q", async () => {
    renderWithProviders(<StudentsPage />);
    expect(await screen.findByRole('button', { name: 'Shablon' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Excel import' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'HEMIS dan tortish' })).not.toBeInTheDocument();
    // Ishlamaydigan "Excel" (eksport) tugmasi ham yo'q — faqat shablon va import.
    expect(screen.queryByRole('button', { name: 'Excel' })).not.toBeInTheDocument();
  });

  it('talaba ismi profil sahifasiga havola', async () => {
    renderWithProviders(<StudentsPage />);
    expect(await screen.findByRole('link', { name: 'Aliyev Akmal' })).toHaveAttribute(
      'href',
      '/admin/students/s1',
    );
  });

  it('Excel import: fayl yuklanadi, hisobot (qo‘shildi/xatolar) ko‘rsatiladi', async () => {
    const user = userEvent.setup();
    renderWithProviders(<StudentsPage />);

    await user.click(await screen.findByRole('button', { name: 'Excel import' }));
    const dialog = await screen.findByRole('dialog');

    await user.upload(within(dialog).getByLabelText(/To.ldirilgan fayl/), xlsx());
    await user.click(within(dialog).getByRole('button', { name: 'Import qilish' }));

    const report = await within(dialog).findByRole('region', { name: 'Import natijasi' });
    expect(within(report).getByRole('status')).toHaveTextContent(
      'Qo‘shildi: 3 · Qabul qilinmadi: 2 · Jami: 5',
    );
    expect(
      within(report).getByText("HEMIS ID 5–20 ta raqamdan iborat bo'lishi kerak."),
    ).toBeInTheDocument();
    expect(within(report).getByText('999-99')).toBeInTheDocument();
  });

  it('Excel import: server xatosi modal ichida ko‘rinadi', async () => {
    server.use(
      http.post(STUDENTS_IMPORT_ENDPOINT, () =>
        problemResponse(400, "Noto'g'ri amal", "Faylni o'qib bo'lmadi — u haqiqiy .xlsx (Excel) fayli bo'lishi kerak."),
      ),
    );
    const user = userEvent.setup();
    renderWithProviders(<StudentsPage />);

    await user.click(await screen.findByRole('button', { name: 'Excel import' }));
    const dialog = await screen.findByRole('dialog');

    await user.upload(within(dialog).getByLabelText(/To.ldirilgan fayl/), xlsx('xato.xlsx'));
    await user.click(within(dialog).getByRole('button', { name: 'Import qilish' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent("Faylni o'qib bo'lmadi");
  });

  it('Shablon tugmasi faylni token bilan so‘raydi', async () => {
    const user = userEvent.setup();
    renderWithProviders(<StudentsPage />);

    await user.click(await screen.findByRole('button', { name: 'Shablon' }));

    // Xato bo'lmasa toolbar'da alert chiqmaydi (jsdom'da blob saqlash bosqichi o'tkazib yuboriladi).
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Shablon' })).not.toBeDisabled(),
    );
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it("№ ustuni sahifadagi tartib raqamlarini ko'rsatadi", async () => {
    renderWithProviders(<StudentsPage />);
    await screen.findByText('Aliyev Akmal');

    const table = screen.getByRole('table', { name: 'Talabalar' });
    for (const n of ['1', '2', '3', '4', '5']) {
      expect(within(table).getByText(n)).toBeInTheDocument();
    }
  });

  it("ikkita qator belgilansa tanlov paneli chiqadi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<StudentsPage />);

    await user.click(await screen.findByRole('checkbox', { name: 'Aliyev Akmal ni belgilash' }));
    await user.click(screen.getByRole('checkbox', { name: 'Sobirov Diyor ni belgilash' }));

    expect(screen.getByText('2 ta tanlandi')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Korxonaga biriktirish' })).toBeInTheDocument();
  });

  it("hammasini belgilash tugmasi sahifadagi barcha qatorlarni tanlaydi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<StudentsPage />);

    await screen.findByText('Aliyev Akmal');
    await user.click(screen.getByRole('checkbox', { name: 'Hammasini belgilash' }));

    expect(screen.getByText('5 ta tanlandi')).toBeInTheDocument();
  });

  it('Korxonaga biriktirish: korxona tanlanadi, hisobot ko‘rsatiladi', async () => {
    const user = userEvent.setup();
    renderWithProviders(<StudentsPage />);

    await user.click(await screen.findByRole('checkbox', { name: 'Aliyev Akmal ni belgilash' }));
    await user.click(screen.getByRole('checkbox', { name: 'Sobirov Diyor ni belgilash' }));
    await user.click(screen.getByRole('button', { name: 'Korxonaga biriktirish' }));

    const dialog = await screen.findByRole('dialog');
    await user.click(await within(dialog).findByRole('radio', { name: 'Tech Solutions MChJ' }));
    await user.click(within(dialog).getByRole('button', { name: 'Biriktirish' }));

    const report = await within(dialog).findByRole('region', { name: 'Biriktirish natijasi' });
    expect(within(report).getByRole('status')).toHaveTextContent(
      'Biriktirildi: 1 · Biriktirilmadi: 1 · Jami: 2',
    );
    expect(within(report).getByText('Sobirov Diyor')).toBeInTheDocument();
    expect(
      within(report).getByText('Allaqachon shu korxonaga biriktirilgan.'),
    ).toBeInTheDocument();
    // Muvaffaqiyatdan keyin tanlov tozalanadi.
    expect(screen.queryByText('2 ta tanlandi')).not.toBeInTheDocument();
  });
});
