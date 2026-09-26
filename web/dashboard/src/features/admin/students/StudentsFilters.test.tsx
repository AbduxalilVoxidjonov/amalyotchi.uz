import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http } from 'msw';
import { useLocation } from 'react-router-dom';
import { server } from '@/mocks/server';
import { problemResponse } from '../shared/mockProblem';
import { renderWithProviders } from '../shared/renderWithProviders';
import { STUDENTS_ENDPOINT, STUDENTS_FILTERS_ENDPOINT } from './api';
import { StudentsPage } from './StudentsPage';

/** Joriy `?search` — URL holatini tekshirish uchun. */
function SearchProbe() {
  return <div data-testid="search">{useLocation().search}</div>;
}

function renderPage(url = '/admin/students') {
  return renderWithProviders(
    <>
      <StudentsPage />
      <SearchProbe />
    </>,
    [url],
  );
}

/** Ro'yxat so'rovlarining query parametrlari (`/students` — filters/profil emas). */
function captureListRequests() {
  const queries: URLSearchParams[] = [];
  const listener = ({ request }: { request: Request }) => {
    const url = new URL(request.url);
    if (url.pathname === STUDENTS_ENDPOINT) queries.push(url.searchParams);
  };
  server.events.on('request:start', listener);
  return {
    queries,
    last: () => queries[queries.length - 1]!,
    stop: () => server.events.removeListener('request:start', listener),
  };
}

const select = (name: string) => screen.getByRole('combobox', { name });
const optionLabels = (name: string) =>
  within(select(name))
    .getAllByRole('option')
    .map((o) => o.textContent);
const search = () => screen.getByTestId('search').textContent ?? '';

/** Variantlar yuklanib, select'lar yoqilguncha kutish. */
async function waitForFilters(firstRow = 'Aliyev Akmal') {
  await screen.findByText(firstRow);
  await waitFor(() => expect(select('Fakultet')).toBeEnabled());
}

describe('StudentsPage — filtrlar', () => {
  it('variantlar backend javobidan chiqadi (fakultet, yo‘nalish, kurs)', async () => {
    renderPage();
    await waitForFilters();

    expect(optionLabels('Fakultet')).toEqual([
      'Fakultet: barchasi',
      'Axborot texnologiyalari',
      'Iqtisodiyot va moliya',
      'Qurilish va arxitektura',
    ]);
    expect(optionLabels("Yo'nalish")).toEqual([
      "Yo'nalish: barchasi",
      'Bank ishi',
      'Kompyuter injiniringi',
    ]);
    expect(optionLabels('Kurs')).toEqual(['Kurs: barchasi', '2-kurs', '3-kurs', '4-kurs']);
    expect(screen.getByText('Jami: 5 ta')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Filtrlarni tozalash' })).not.toBeInTheDocument();
  });

  it("fakultet tanlansa yo'nalishlar toraydi, so'rov va URL yangilanadi", async () => {
    const user = userEvent.setup();
    const requests = captureListRequests();
    renderPage();
    await waitForFilters();

    await user.selectOptions(select('Fakultet'), 'Axborot texnologiyalari');

    expect(optionLabels("Yo'nalish")).toEqual(["Yo'nalish: barchasi", 'Kompyuter injiniringi']);
    await waitFor(() => expect(requests.last().get('facultyId')).toBe('f1'));
    expect(await screen.findByText('Jami: 2 ta')).toBeInTheDocument();
    expect(screen.queryByText('Ismoilova Madina')).not.toBeInTheDocument();
    expect(search()).toBe('?faculty=f1');
    requests.stop();
  });

  it("yo'nalish va kurs parametrlari so'rovga yuboriladi (AND)", async () => {
    const user = userEvent.setup();
    const requests = captureListRequests();
    renderPage();
    await waitForFilters();

    await user.selectOptions(select("Yo'nalish"), 'Kompyuter injiniringi');
    await user.selectOptions(select('Kurs'), '3-kurs');

    await waitFor(() => {
      const q = requests.last();
      expect(q.get('directionId')).toBe('dir1');
      expect(q.get('course')).toBe('3');
      expect(q.get('page')).toBe('1');
      expect(q.has('facultyId')).toBe(false);
    });
    expect(await screen.findByText('Jami: 2 ta')).toBeInTheDocument();
    expect(search()).toBe('?direction=dir1&course=3');
    requests.stop();
  });

  it("fakultet o'zgarsa unga tegishli bo'lmagan yo'nalish tozalanadi", async () => {
    const user = userEvent.setup();
    renderPage();
    await waitForFilters();

    await user.selectOptions(select("Yo'nalish"), 'Bank ishi');
    await user.selectOptions(select('Fakultet'), 'Iqtisodiyot va moliya');
    // Tegishli — saqlanadi.
    expect(select("Yo'nalish")).toHaveValue('dir3');

    await user.selectOptions(select('Fakultet'), 'Axborot texnologiyalari');
    expect(select("Yo'nalish")).toHaveValue('');
    expect(search()).toBe('?faculty=f1');
  });

  it("holat URL'dan tiklanadi (sahifa yangilansa/orqaga qaytilsa)", async () => {
    const requests = captureListRequests();
    renderPage('/admin/students?faculty=f2&course=2&q=Madina');
    await waitForFilters('Ismoilova Madina');

    expect(select('Fakultet')).toHaveValue('f2');
    expect(select('Kurs')).toHaveValue('2');
    expect(screen.getByRole('searchbox', { name: 'Qidirish' })).toHaveValue('Madina');
    expect(screen.queryByText('Oripov Javohir')).not.toBeInTheDocument();
    const q = requests.queries[0]!;
    expect(q.get('facultyId')).toBe('f2');
    expect(q.get('course')).toBe('2');
    expect(q.get('q')).toBe('Madina');
    requests.stop();
  });

  it('"Filtrlarni tozalash" filtrlarni olib tashlaydi, qidiruv saqlanadi', async () => {
    const user = userEvent.setup();
    renderPage('/admin/students?faculty=f1&course=3&q=a');
    await waitForFilters();

    await user.click(screen.getByRole('button', { name: 'Filtrlarni tozalash' }));

    expect(select('Fakultet')).toHaveValue('');
    expect(select('Kurs')).toHaveValue('');
    expect(search()).toBe('?q=a');
    expect(screen.queryByRole('button', { name: 'Filtrlarni tozalash' })).not.toBeInTheDocument();
  });

  it('filtr bo‘yicha natija bo‘sh — bo‘sh holat va tozalash tugmasi', async () => {
    const user = userEvent.setup();
    renderPage('/admin/students?faculty=f3&course=2');

    expect(
      await screen.findByText("Tanlangan filtrlar bo'yicha talaba topilmadi."),
    ).toBeInTheDocument();
    expect(screen.getByText("Talabalar yo'q")).toBeInTheDocument();
    expect(screen.getByText('Jami: 0 ta')).toBeInTheDocument();

    const buttons = screen.getAllByRole('button', { name: 'Filtrlarni tozalash' });
    expect(buttons).toHaveLength(2);
    await user.click(buttons[1]!);
    expect(await screen.findByText('Aliyev Akmal')).toBeInTheDocument();
    expect(search()).toBe('');
  });

  it('filtr o‘zgarsa 1-sahifaga qaytadi', async () => {
    const user = userEvent.setup();
    const requests = captureListRequests();
    renderPage('/admin/students?page=2');
    await waitFor(() => expect(select('Fakultet')).toBeEnabled());
    await waitFor(() => expect(requests.last().get('page')).toBe('2'));

    await user.selectOptions(select('Kurs'), '2-kurs');

    await waitFor(() => expect(requests.last().get('page')).toBe('1'));
    expect(search()).toBe('?course=2');
    requests.stop();
  });

  it('filtr o‘zgarsa talabalar tanlovi tozalanadi', async () => {
    const user = userEvent.setup();
    renderPage();
    await waitForFilters();

    await user.click(screen.getByRole('checkbox', { name: 'Aliyev Akmal ni belgilash' }));
    expect(screen.getByText('1 ta tanlandi')).toBeInTheDocument();

    await user.selectOptions(select('Fakultet'), 'Axborot texnologiyalari');
    await waitFor(() => expect(screen.queryByText('1 ta tanlandi')).not.toBeInTheDocument());
  });

  it("variantlar yuklanayotganda select'lar o'chirilgan", async () => {
    renderPage();
    expect(select('Fakultet')).toBeDisabled();
    expect(select("Yo'nalish")).toBeDisabled();
    expect(select('Kurs')).toBeDisabled();
    await waitForFilters();
  });

  it("variantlar yuklanmasa filtr paneli yashiriladi, ro'yxat ishlaydi", async () => {
    server.use(
      http.get(STUDENTS_FILTERS_ENDPOINT, () => problemResponse(500, 'Xato', 'Server xatosi.')),
    );
    renderPage();

    expect(await screen.findByText('Aliyev Akmal')).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.queryByRole('combobox', { name: 'Fakultet' })).not.toBeInTheDocument(),
    );
    expect(screen.queryByRole('combobox', { name: 'Kurs' })).not.toBeInTheDocument();
    expect(screen.getByText('Jami: 5 ta')).toBeInTheDocument();
  });

  it("qidiruv (debounce'dan keyin) URL'ga yoziladi va sahifa 1 ga qaytadi", async () => {
    const user = userEvent.setup();
    renderPage('/admin/students?course=3&page=2');
    await waitFor(() => expect(select('Kurs')).toBeEnabled());

    await user.type(screen.getByRole('searchbox', { name: 'Qidirish' }), 'Diyor');

    await waitFor(() => expect(search()).toBe('?course=3&q=Diyor'));
    expect(await screen.findByText('Sobirov Diyor')).toBeInTheDocument();
  });
});
