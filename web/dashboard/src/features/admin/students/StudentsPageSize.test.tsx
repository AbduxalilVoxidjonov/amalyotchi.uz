import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useLocation } from 'react-router-dom';
import { server } from '@/mocks/server';
import { FacultiesPage } from '../faculties/FacultiesPage';
import { renderWithProviders } from '../shared/renderWithProviders';
import { TutorsPage } from '../tutors/TutorsPage';
import { STUDENTS_ENDPOINT } from './api';
import { clampPageSize } from './pageSize';
import { StudentsPage } from './StudentsPage';

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

const sizeInput = () => screen.getByRole<HTMLInputElement>('spinbutton', { name: 'Sahifada:' });
const search = () => screen.getByTestId('search').textContent ?? '';

describe('clampPageSize', () => {
  it.each([
    ['0', 1],
    ['-5', 1],
    ['501', 500],
    ['9999', 500],
    ['2.7', 2],
    ['0.4', 1],
    ['50', 50],
    [' 120 ', 120],
  ])('%s → %d', (raw, expected) => {
    expect(clampPageSize(raw)).toBe(expected);
  });

  it("bo'sh / son emas → null (joriy qiymat saqlanadi)", () => {
    expect(clampPageSize('')).toBeNull();
    expect(clampPageSize('  ')).toBeNull();
    expect(clampPageSize('abc')).toBeNull();
  });
});

describe('StudentsPage — sahifa hajmi', () => {
  it("sukut 20: so'rovda pageSize=20, URL toza, maydonda 20", async () => {
    const requests = captureListRequests();
    renderPage();
    await screen.findByText('Aliyev Akmal');

    expect(requests.last().get('pageSize')).toBe('20');
    expect(sizeInput()).toHaveValue(20);
    expect(sizeInput()).toHaveAttribute('min', '1');
    expect(sizeInput()).toHaveAttribute('max', '500');
    expect(search()).toBe('');
    requests.stop();
  });

  it("son kiritib Enter → so'rovda pageSize, URL'da size, sahifa 1", async () => {
    const user = userEvent.setup();
    const requests = captureListRequests();
    renderPage('/admin/students?page=3&q=ali');
    await waitFor(() => expect(requests.last().get('page')).toBe('3'));

    await user.clear(sizeInput());
    await user.type(sizeInput(), '150');
    // Har harfda so'rov yuborilmaydi — faqat Enter/blur'da.
    expect(requests.queries.some((q) => q.get('pageSize') === '1')).toBe(false);
    expect(search()).toBe('?page=3&q=ali');

    await user.keyboard('{Enter}');

    await waitFor(() => expect(requests.last().get('pageSize')).toBe('150'));
    expect(requests.last().get('page')).toBe('1');
    expect(search()).toBe('?q=ali&size=150');
    expect(sizeInput()).toHaveValue(150);
    requests.stop();
  });

  it("blur'da ham qo'llanadi", async () => {
    const user = userEvent.setup();
    const requests = captureListRequests();
    renderPage();
    await screen.findByText('Aliyev Akmal');

    await user.clear(sizeInput());
    await user.type(sizeInput(), '50');
    await user.tab();

    await waitFor(() => expect(requests.last().get('pageSize')).toBe('50'));
    expect(search()).toBe('?size=50');
    requests.stop();
  });

  it.each([
    ['0', 1, '?size=1'],
    ['9000', 500, '?size=500'],
    ['2.5', 2, '?size=2'],
  ])("noto'g'ri qiymat %s → %d ga qisiladi", async (typed, expected, url) => {
    const user = userEvent.setup();
    const requests = captureListRequests();
    renderPage();
    await screen.findByText('Aliyev Akmal');

    await user.clear(sizeInput());
    await user.type(sizeInput(), `${typed}{Enter}`);

    expect(sizeInput()).toHaveValue(expected);
    await waitFor(() => expect(requests.last().get('pageSize')).toBe(String(expected)));
    expect(search()).toBe(url);
    requests.stop();
  });

  it("bo'sh qiymat → joriy hajm qaytadi, so'rov yo'q", async () => {
    const user = userEvent.setup();
    const requests = captureListRequests();
    renderPage('/admin/students?size=50');
    await screen.findByText('Aliyev Akmal');
    const before = requests.queries.length;

    await user.clear(sizeInput());
    await user.keyboard('{Enter}');

    expect(sizeInput()).toHaveValue(50);
    expect(search()).toBe('?size=50');
    expect(requests.queries).toHaveLength(before);
    requests.stop();
  });

  it("20 ga qaytarilsa URL'dan size o'chadi", async () => {
    const user = userEvent.setup();
    renderPage('/admin/students?size=100');
    await screen.findByText('Aliyev Akmal');

    await user.clear(sizeInput());
    await user.type(sizeInput(), '20{Enter}');

    await waitFor(() => expect(search()).toBe(''));
  });

  it("URL'dan tiklanadi (?size=2&page=2): so'rov, maydon va footer", async () => {
    const requests = captureListRequests();
    renderPage('/admin/students?size=2&page=2');

    // 5 ta mock talaba, 2 tadan → 2-sahifa: 3–4 / 5; tartib raqami ham hajmga mos.
    expect(await screen.findByText('3–4 / 5')).toBeInTheDocument();
    expect(requests.last().get('pageSize')).toBe('2');
    expect(requests.last().get('page')).toBe('2');
    expect(sizeInput()).toHaveValue(2);
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Keyingi' })).toBeEnabled();
    requests.stop();
  });

  it("URL'dagi noto'g'ri size qisiladi (?size=9999 → 500)", async () => {
    const requests = captureListRequests();
    renderPage('/admin/students?size=9999');
    await screen.findByText('Aliyev Akmal');

    expect(requests.last().get('pageSize')).toBe('500');
    expect(sizeInput()).toHaveValue(500);
    requests.stop();
  });

  it('hajm o‘zgarsa joriy sahifadagi tanlov tozalanadi', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.click(await screen.findByRole('checkbox', { name: 'Aliyev Akmal ni belgilash' }));
    expect(screen.getByText('1 ta tanlandi')).toBeInTheDocument();

    await user.clear(sizeInput());
    await user.type(sizeInput(), '50{Enter}');

    await waitFor(() => expect(screen.queryByText('1 ta tanlandi')).not.toBeInTheDocument());
  });
});

describe("boshqa admin jadvallarida sahifa hajmi boshqaruvi yo'q", () => {
  it.each([
    ['Tyutorlar', <TutorsPage key="t" />],
    ['Fakultetlar', <FacultiesPage key="f" />],
  ])('%s', async (_name, ui) => {
    renderWithProviders(ui);
    await screen.findByRole('button', { name: 'Keyingi' });
    await screen.findByText(/^\d+–\d+ \/ \d+$/);
    expect(screen.queryByRole('spinbutton', { name: 'Sahifada:' })).not.toBeInTheDocument();
  });
});
