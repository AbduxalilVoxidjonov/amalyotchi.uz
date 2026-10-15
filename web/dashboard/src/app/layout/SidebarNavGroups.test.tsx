import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { adminNavGroups } from '../nav';
import { SidebarNavGroups } from './SidebarNavGroups';
import { NAV_GROUPS_STORAGE_KEY } from './useNavGroupsOpen';

function setup(
  path = '/admin',
  props: { onNavigate?: () => void; onSearchShortcut?: () => void } = {},
) {
  const groups = adminNavGroups({ '/admin/students': 5, '/admin/face': 2 }, { isSuperAdmin: true });
  const router = createMemoryRouter(
    [{ path: '*', element: <SidebarNavGroups groups={groups} {...props} /> }],
    { initialEntries: [path] },
  );
  const { unmount } = render(<RouterProvider router={router} />);
  const nav = screen.getByRole('navigation', { name: 'Asosiy' });
  const search = screen.getByRole('searchbox', { name: 'Sahifalarni qidirish' });
  const groupBtn = (name: RegExp) => within(nav).getByRole('button', { name });
  return { router, nav, search, groupBtn, unmount };
}

describe('SidebarNavGroups', () => {
  it("bo'lim tugmasi: aria-expanded + aria-controls; bosilganda ochiladi/yopiladi", async () => {
    const { nav, groupBtn } = setup('/admin');
    const btn = groupBtn(/Talabalar/);
    expect(btn).toHaveAttribute('aria-expanded', 'false');
    const list = document.getElementById(btn.getAttribute('aria-controls')!);
    expect(list).not.toBeVisible();
    // Yopiq — sarlavhada faqat kutilayotgan ish (yuz 2); talabalar jami (5) kirmaydi.
    expect(btn).toHaveTextContent(/^Talabalar2$/);

    await userEvent.click(btn);
    expect(btn).toHaveAttribute('aria-expanded', 'true');
    expect(list).toBeVisible();
    expect(btn).toHaveTextContent(/^Talabalar$/);
    expect(within(nav).getByRole('link', { name: /Yuz tasdiqlash/ })).toHaveTextContent(
      'Yuz tasdiqlash2',
    );

    // Klaviatura: Enter/Space bilan ham boshqariladi (native button).
    btn.focus();
    await userEvent.keyboard('{Enter}');
    expect(btn).toHaveAttribute('aria-expanded', 'false');
    await userEvent.keyboard(' ');
    expect(btn).toHaveAttribute('aria-expanded', 'true');
  });

  it("aktiv sahifa bo'limi ochiq (saqlangan 'yopiq' holatdan qat'i nazar)", () => {
    window.localStorage.setItem(NAV_GROUPS_STORAGE_KEY, JSON.stringify({ system: false }));
    const { groupBtn } = setup('/admin/audit');
    expect(groupBtn(/Tizim/)).toHaveAttribute('aria-expanded', 'true');
    expect(groupBtn(/Asosiy/)).toHaveAttribute('aria-expanded', 'true');
    expect(groupBtn(/Amaliyot/)).toHaveAttribute('aria-expanded', 'false');
  });

  it('ochiq/yopiq holat localStorage’da saqlanadi va qayta tiklanadi', async () => {
    const first = setup('/admin');
    await userEvent.click(first.groupBtn(/Amaliyot/));
    await userEvent.click(first.groupBtn(/Asosiy/));
    expect(JSON.parse(window.localStorage.getItem(NAV_GROUPS_STORAGE_KEY)!)).toMatchObject({
      practice: true,
      main: false,
    });
    // Asosiy — aktiv bo'lim; qayta yuklanganda baribir ochiq. Amaliyot — saqlangan holat.
    first.unmount();
    const second = setup('/admin');
    expect(second.groupBtn(/Amaliyot/)).toHaveAttribute('aria-expanded', 'true');
    expect(second.groupBtn(/Asosiy/)).toHaveAttribute('aria-expanded', 'true');
  });

  it("buzilgan / mavjud bo'lmagan storage — xatosiz, sukut holati", () => {
    window.localStorage.setItem(NAV_GROUPS_STORAGE_KEY, '{buzilgan');
    const getItem = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    const { groupBtn } = setup('/admin');
    expect(groupBtn(/Asosiy/)).toHaveAttribute('aria-expanded', 'true');
    expect(groupBtn(/Talabalar/)).toHaveAttribute('aria-expanded', 'false');
    getItem.mockRestore();
    setItem.mockRestore();
  });

  it("qidiruv: mos bo'limlar ochiq, boshqalari yashirin; Esc — tozalaydi", async () => {
    const { nav, search } = setup('/admin');
    await userEvent.type(search, 'BAHO');
    expect(within(nav).queryAllByRole('button')).toHaveLength(0);
    expect(
      within(nav)
        .getAllByRole('link')
        .map((l) => l.textContent),
    ).toEqual(['Baholash']);
    expect(screen.getByText('1 ta natija')).toHaveAttribute('aria-live', 'polite');

    await userEvent.clear(search);
    await userEvent.type(search, 'zzzz');
    expect(within(nav).getByText('Hech narsa topilmadi')).toBeInTheDocument();

    await userEvent.keyboard('{Escape}');
    expect(search).toHaveValue('');
    expect(within(nav).getAllByRole('button')).toHaveLength(6);
  });

  it('qidiruv: apostrof variantlari va Enter → birinchi natija', async () => {
    const onNavigate = vi.fn();
    const { router, search } = setup('/admin', { onNavigate });
    await userEvent.type(search, 'sozlama');
    await userEvent.keyboard('{Enter}');
    expect(router.state.location.pathname).toBe('/admin/settings');
    expect(onNavigate).toHaveBeenCalledTimes(1);
    expect(search).toHaveValue('');

    await userEvent.type(search, 'yoʻnalish');
    await userEvent.keyboard('{Enter}');
    expect(router.state.location.pathname).toBe('/admin/faculties');
  });

  it('ArrowDown — birinchi natija havolasiga fokus', async () => {
    const { nav, search } = setup('/admin');
    await userEvent.type(search, 'xarita');
    await userEvent.keyboard('{ArrowDown}');
    expect(within(nav).getByRole('link', { name: 'Xarita' })).toHaveFocus();
  });

  it("'/' va Ctrl/Cmd+K — qidiruvga fokus; matn maydonida '/' oddiy belgi", async () => {
    const onSearchShortcut = vi.fn();
    const { search } = setup('/admin', { onSearchShortcut });
    expect(search).not.toHaveFocus();
    fireEvent.keyDown(document.body, { key: '/' });
    expect(search).toHaveFocus();
    expect(onSearchShortcut).toHaveBeenCalledTimes(1);

    search.blur();
    fireEvent.keyDown(document.body, { key: 'k', ctrlKey: true });
    expect(search).toHaveFocus();
    search.blur();
    fireEvent.keyDown(document.body, { key: 'K', metaKey: true });
    expect(search).toHaveFocus();

    // Fokus boshqa input'da — '/' qidiruvga o'tkazmaydi.
    const other = document.createElement('input');
    document.body.appendChild(other);
    other.focus();
    fireEvent.keyDown(other, { key: '/' });
    expect(other).toHaveFocus();
    other.remove();
  });
});
