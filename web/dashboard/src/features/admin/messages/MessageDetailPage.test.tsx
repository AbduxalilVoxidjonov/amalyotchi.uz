import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { renderHierarchyPage } from '../shared/renderHierarchyPage';
import { MessageDetailPage } from './MessageDetailPage';
import { mockRecipients } from './mocks';
import { isMessageActive } from './types';

function renderDetail(id: string) {
  return renderHierarchyPage(<MessageDetailPage />, '/admin/messages/:messageId', [
    `/admin/messages/${id}`,
  ]);
}

function stat(label: string) {
  const grid = screen.getByRole('group', { name: 'Yetkazish statistikasi' });
  return within(grid).getByText(label).parentElement!;
}

describe('MessageDetailPage', () => {
  it("to'liq matn, statistika va yetkazishlar", async () => {
    renderDetail('m1');
    expect(await screen.findByText(/Amaliyot davri 1-oktabrdan boshlanadi/)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Barcha ulanganlar' })).toBeInTheDocument();
    expect(stat('Jami')).toHaveTextContent(String(mockRecipients.length));
    expect(stat('Xato')).toHaveTextContent('2');
    expect(stat('Bloklangan')).toHaveTextContent('2');
    expect(stat('Navbatda')).toHaveTextContent('0');
    expect(screen.getByText('Yuborish yakunlangan')).toBeInTheDocument();
    expect(await screen.findByRole('table', { name: 'Yetkazishlar' })).toBeInTheDocument();
  });

  it('holat filtri: faqat xatolar (xato matni bilan)', async () => {
    const user = userEvent.setup();
    renderDetail('m1');
    const table = await screen.findByRole('table', { name: 'Yetkazishlar' });
    const badge = { selector: 'span' };
    await within(table).findAllByText('Yetkazildi', badge);
    await user.selectOptions(screen.getByRole('combobox', { name: 'Holat' }), 'failed');
    await waitFor(() => expect(within(table).queryAllByText('Yetkazildi', badge)).toHaveLength(0));
    expect(within(table).getAllByText('Too Many Requests: retry after 5')).toHaveLength(2);
  });

  it('"Xatolarni qayta yuborish" — xatolar navbatga qaytadi', async () => {
    const user = userEvent.setup();
    renderDetail('m1');
    const retry = await screen.findByRole('button', { name: 'Xatolarni qayta yuborish' });
    await user.click(retry);
    await waitFor(() => expect(stat('Navbatda')).toHaveTextContent('2'));
    expect(stat('Xato')).toHaveTextContent('0');
    expect(screen.getAllByText('Navbatda').length).toBeGreaterThan(1); // status badge ham
    expect(
      screen.queryByRole('button', { name: 'Xatolarni qayta yuborish' }),
    ).not.toBeInTheDocument();
  });

  it("xatosiz xabarda qayta yuborish tugmasi yo'q", async () => {
    renderDetail('m2');
    await screen.findByText(/412-22 guruh talabalari/);
    expect(
      screen.queryByRole('button', { name: 'Xatolarni qayta yuborish' }),
    ).not.toBeInTheDocument();
  });

  it('404 — "Xabar topilmadi."', async () => {
    renderDetail('nope');
    expect(await screen.findByText('Xabar topilmadi.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Yuborilganlarga qaytish' })).toHaveAttribute(
      'href',
      '/admin/messages?tab=sent',
    );
  });

  it("yuborilgandan keyingi bir martalik xabar (flash) ko'rsatiladi va yopiladi", async () => {
    const user = userEvent.setup();
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter
          initialEntries={[
            { pathname: '/admin/messages/m1', state: { flash: 'Xabar navbatga qo‘yildi' } },
          ]}
        >
          <Routes>
            <Route path="/admin/messages/:messageId" element={<MessageDetailPage />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );
    const flash = await screen.findByText('Xabar navbatga qo‘yildi');
    await user.click(screen.getByRole('button', { name: 'Xabarni yopish' }));
    expect(flash).not.toBeInTheDocument();
  });

  it('isMessageActive — faqat navbatda/yuborilmoqda holatida avtomatik yangilanadi', () => {
    expect(isMessageActive('queued')).toBe(true);
    expect(isMessageActive('sending')).toBe(true);
    expect(isMessageActive('completed')).toBe(false);
    expect(isMessageActive(undefined)).toBe(false);
  });
});
