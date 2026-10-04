import { QueryClient } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { RouterProvider } from 'react-router-dom';
import { AppProviders } from '@/app/providers';
import { createTestRouter } from '@/app/router';
import { issueSession, mockUsers } from '@/mocks/data';
import { server } from '@/mocks/server';
import { useAuthStore } from '@/shared/auth/store';
import { ADMINS_ENDPOINT } from './api';
import { mockAdmins, mockTemporaryPassword } from './mocks';

/** 0 — bosh admin (Admin Adminov), 1 — oddiy admin (Nodira Saidova). */
function renderAs(index: 0 | 1, path = '/admin/admins') {
  useAuthStore.getState().setSession(issueSession(mockUsers[index]!));
  const router = createTestRouter([path]);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </AppProviders>,
  );
  return router;
}

async function findTable() {
  return screen.findByRole('table', { name: 'Adminlar' });
}

/** Jadvaldagi admin qatori (FISH matni bo'yicha). */
function rowOf(table: HTMLElement, name: string): HTMLElement {
  return within(table).getByText(name).closest('[role="row"]') as HTMLElement;
}

describe('AdminsPage (/admin/admins, v3.28)', () => {
  it("bosh admin: sidebar'da 'Adminlar', ro'yxat, bosh admin belgisi va (siz)", async () => {
    renderAs(0);
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    expect(within(nav).getByRole('link', { name: 'Adminlar' })).toHaveAttribute(
      'aria-current',
      'page',
    );
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Adminlar');

    const table = await findTable();
    await within(table).findByText('Admin Adminov');
    const superRow = rowOf(table, 'Admin Adminov');
    expect(within(superRow).getByText('Bosh admin')).toBeInTheDocument();
    expect(within(superRow).getByText('(siz)')).toBeInTheDocument();
    expect(within(table).getByText('Nodira Saidova')).toBeInTheDocument();
    expect(within(rowOf(table, 'Rasulov Baxtiyor')).getByText('Bloklangan')).toBeInTheDocument();
  });

  it("oddiy admin: sidebar'da 'Adminlar' yo'q, marshrut → /admin", async () => {
    const router = renderAs(1);
    await waitFor(() => expect(router.state.location.pathname).toBe('/admin'));
    const nav = await screen.findByRole('navigation', { name: 'Asosiy' });
    expect(within(nav).queryByRole('link', { name: 'Adminlar' })).not.toBeInTheDocument();
  });

  it("bloklash: bosh admin va o'zi uchun o'chiq; boshqa admin — tasdiq bilan bloklanadi", async () => {
    const user = userEvent.setup();
    renderAs(0);
    const table = await findTable();
    await within(table).findByText('Nodira Saidova');

    const selfBlock = within(rowOf(table, 'Admin Adminov')).getByRole('button', {
      name: 'Admin Adminov — bloklash',
    });
    expect(selfBlock).toBeDisabled();
    expect(selfBlock).toHaveAttribute('title', "Bosh adminni bloklab bo'lmaydi");

    const block = within(rowOf(table, 'Nodira Saidova')).getByRole('button', {
      name: 'Nodira Saidova — bloklash',
    });
    expect(block).toBeEnabled();
    await user.click(block);
    const dialog = await screen.findByRole('dialog', { name: 'Adminni bloklash' });
    await user.click(within(dialog).getByRole('button', { name: 'Bloklash' }));

    await waitFor(() =>
      expect(within(rowOf(table, 'Nodira Saidova')).getByText('Bloklangan')).toBeInTheDocument(),
    );
    expect(screen.getByRole('status')).toHaveTextContent('Nodira Saidova bloklandi.');
    expect(mockAdmins().find((a) => a.fullName === 'Nodira Saidova')?.isActive).toBe(false);
  });

  it("o'zi (bosh admin bo'lmagan holat) — bloklash o'chiq, sababi title'da", async () => {
    const me = mockUsers[0]!;
    server.use(
      http.get(ADMINS_ENDPOINT, () =>
        HttpResponse.json({
          items: [
            {
              id: me.id,
              fullName: me.fullName,
              login: me.hemisId,
              phoneNumber: null,
              isActive: true,
              isSuperAdmin: false,
              lastLoginAt: null,
              createdAt: '2026-08-20T10:00:00+05:00',
            },
          ],
        }),
      ),
    );
    renderAs(0);
    const table = await findTable();
    await within(table).findByText(me.fullName);
    const btn = within(rowOf(table, me.fullName)).getByRole('button', {
      name: `${me.fullName} — bloklash`,
    });
    expect(btn).toBeDisabled();
    expect(btn).toHaveAttribute('title', "O'zingizni bloklab bo'lmaydi");
  });

  it("yaratish: parolsiz → vaqtinchalik parol bir marta ko'rsatiladi (nusxalash bilan)", async () => {
    const user = userEvent.setup();
    renderAs(0);
    await findTable();
    await user.click(screen.getByRole('button', { name: 'Yangi admin' }));
    const dialog = await screen.findByRole('dialog', { name: 'Yangi admin' });
    await user.type(within(dialog).getByLabelText('FISH'), 'Karimova Dilnoza');
    await user.type(within(dialog).getByLabelText('Login'), 'dilnoza.k');
    await user.type(within(dialog).getByLabelText('Telefon'), '90 111 22 33');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    const secret = await screen.findByRole('dialog', { name: 'Admin yaratildi' });
    expect(within(secret).getByTestId('temporary-password')).toHaveTextContent(
      mockTemporaryPassword(1),
    );
    expect(within(secret).getByText(/login: dilnoza\.k/)).toBeInTheDocument();
    expect(within(secret).getByRole('button', { name: 'Nusxalash' })).toBeInTheDocument();

    await user.click(within(secret).getByRole('button', { name: 'Tayyor' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    // Parol qayta ko'rinmaydi; yangi admin jadvalda.
    expect(screen.queryByText(mockTemporaryPassword(1))).not.toBeInTheDocument();
    const table = await findTable();
    expect(await within(table).findByText('Karimova Dilnoza')).toBeInTheDocument();
  });

  it('yaratish: band login (409) — login maydoni ostida; validatsiya serverga bormaydi', async () => {
    const user = userEvent.setup();
    renderAs(0);
    await findTable();
    await user.click(screen.getByRole('button', { name: 'Yangi admin' }));
    const dialog = await screen.findByRole('dialog', { name: 'Yangi admin' });

    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));
    expect(await within(dialog).findByText('FISH ni kiriting.')).toBeInTheDocument();
    expect(within(dialog).getByText('Loginni kiriting.')).toBeInTheDocument();

    await user.type(within(dialog).getByLabelText('FISH'), 'Yangi Admin');
    await user.type(within(dialog).getByLabelText('Login'), '100000000002');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));
    expect(await within(dialog).findByText('Bu login allaqachon band.')).toBeInTheDocument();
  });

  it('server 400 maydon xatosi (PascalCase) — tegishli maydon ostida', async () => {
    server.use(
      http.post(ADMINS_ENDPOINT, () =>
        HttpResponse.json(
          {
            status: 400,
            title: "Ma'lumotlar noto'g'ri",
            errors: { Login: ["Login faqat lotin harflari, raqam va '.' dan iborat."] },
          },
          { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    const user = userEvent.setup();
    renderAs(0);
    await findTable();
    await user.click(screen.getByRole('button', { name: 'Yangi admin' }));
    const dialog = await screen.findByRole('dialog', { name: 'Yangi admin' });
    await user.type(within(dialog).getByLabelText('FISH'), 'Yangi Admin');
    await user.type(within(dialog).getByLabelText('Login'), 'yangi#admin');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));
    expect(
      await within(dialog).findByText("Login faqat lotin harflari, raqam va '.' dan iborat."),
    ).toBeInTheDocument();
  });

  it('tahrirlash: FISH va telefon yangilanadi', async () => {
    const user = userEvent.setup();
    renderAs(0);
    const table = await findTable();
    await within(table).findByText('Nodira Saidova');
    await user.click(
      within(rowOf(table, 'Nodira Saidova')).getByRole('button', {
        name: 'Nodira Saidova — tahrirlash',
      }),
    );
    const dialog = await screen.findByRole('dialog', { name: 'Adminni tahrirlash' });
    expect(within(dialog).queryByLabelText('Login')).not.toBeInTheDocument();
    const name = within(dialog).getByLabelText('FISH');
    await user.clear(name);
    await user.type(name, 'Saidova Nodira');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));
    expect(await within(table).findByText('Saidova Nodira')).toBeInTheDocument();
  });

  it('parolni tiklash: tasdiq → yangi vaqtinchalik parol oynasi', async () => {
    const user = userEvent.setup();
    renderAs(0);
    const table = await findTable();
    await within(table).findByText('Nodira Saidova');
    await user.click(
      within(rowOf(table, 'Nodira Saidova')).getByRole('button', {
        name: 'Nodira Saidova — parolni tiklash',
      }),
    );
    const confirm = await screen.findByRole('dialog', { name: 'Parolni tiklash' });
    await user.click(within(confirm).getByRole('button', { name: 'Tiklash' }));
    const secret = await screen.findByRole('dialog', { name: 'Parol tiklandi' });
    expect(within(secret).getByTestId('temporary-password')).toHaveTextContent(
      mockTemporaryPassword(1),
    );
  });

  it('server 403 (bosh admin emas) — xato holati', async () => {
    server.use(
      http.get(ADMINS_ENDPOINT, () =>
        HttpResponse.json(
          {
            status: 403,
            title: "Ruxsat yo'q",
            detail: 'Adminlarni faqat bosh admin boshqara oladi.',
          },
          { status: 403, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    renderAs(0);
    expect(
      await screen.findByText('Adminlarni faqat bosh admin boshqara oladi.'),
    ).toBeInTheDocument();
  });
});
