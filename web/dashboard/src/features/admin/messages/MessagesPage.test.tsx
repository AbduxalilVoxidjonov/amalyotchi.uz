import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http } from 'msw';
import { server } from '@/mocks/server';
import { problemResponse } from '../shared/mockProblem';
import { renderHierarchyPage } from '../shared/renderHierarchyPage';
import { MESSAGES_ENDPOINT } from './api';
import { BOT_BLOCKED_HINT } from './components/RecipientsTable';
import { MessagesPage } from './MessagesPage';
import { mockRecipients } from './mocks';

function renderPage(url = '/admin/messages') {
  return renderHierarchyPage(<MessagesPage />, '/admin/messages', [url]);
}

const sorted = [...mockRecipients];

describe('MessagesPage · Ulanganlar', () => {
  it("ro'yxat, jami soni va bot bloklangan badge (tooltip bilan)", async () => {
    renderPage('/admin/messages?size=50');
    expect(await screen.findByText(sorted[0]!.fullName)).toBeInTheDocument();
    expect(screen.getByText(`Jami: ${mockRecipients.length} ta`)).toBeInTheDocument();
    const blocked = screen.getAllByText('Bot bloklangan');
    expect(blocked).toHaveLength(2);
    expect(blocked[0]!.closest('[title]')).toHaveAttribute('title', BOT_BLOCKED_HINT);
    expect(screen.getByRole('tab', { name: 'Ulanganlar' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
  });

  it('tanlov sahifalar va filtr o‘zgarishida saqlanadi; "Tanlovni tozalash"', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.click(
      await screen.findByRole('checkbox', { name: `${sorted[0]!.fullName} ni tanlash` }),
    );
    expect(screen.getByRole('button', { name: 'Tanlanganlarga yozish (1)' })).toBeEnabled();

    await user.click(screen.getByRole('button', { name: 'Keyingi' }));
    await user.click(
      await screen.findByRole('checkbox', { name: `${sorted[20]!.fullName} ni tanlash` }),
    );
    expect(screen.getByRole('button', { name: 'Tanlanganlarga yozish (2)' })).toBeInTheDocument();

    // Filtr o'zgarsa ham tanlov tozalanmaydi.
    await user.selectOptions(await screen.findByRole('combobox', { name: 'Kurs' }), '3');
    await waitFor(() => expect(screen.getByText(/^Jami: \d+ ta$/)).not.toHaveTextContent('44'));
    expect(screen.getByRole('button', { name: 'Tanlanganlarga yozish (2)' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Tanlovni tozalash' }));
    expect(screen.getByRole('button', { name: 'Tanlanganlarga yozish (0)' })).toBeDisabled();
  });

  it('"Sahifadagilarni tanlash" — joriy sahifadagi barcha qatorlar', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText(sorted[0]!.fullName);
    await user.click(screen.getByRole('checkbox', { name: 'Sahifadagilarni tanlash' }));
    expect(screen.getByRole('button', { name: 'Tanlanganlarga yozish (20)' })).toBeInTheDocument();
  });

  it("filtrlar bog'liq: fakultet o'zgarsa guruh tozalanadi, guruhlar ro'yxati toraytiriladi", async () => {
    const user = userEvent.setup();
    renderPage();
    const group = await screen.findByRole('combobox', { name: 'Guruh' });
    await waitFor(() =>
      expect(within(group).getByRole('option', { name: '221-23' })).toBeInTheDocument(),
    );
    await user.selectOptions(group, '221-23');
    await waitFor(() => expect(screen.getByText(/^Jami: \d+ ta$/)).not.toHaveTextContent('44'));
    expect(screen.getByRole('combobox', { name: 'Guruh' })).toHaveValue('g4');

    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Fakultet' })).toBeEnabled());
    await user.selectOptions(screen.getByRole('combobox', { name: 'Fakultet' }), 'f1');
    expect(screen.getByRole('combobox', { name: 'Guruh' })).toHaveValue('');
    await waitFor(() =>
      expect(
        within(screen.getByRole('combobox', { name: 'Guruh' })).queryByRole('option', {
          name: '221-23',
        }),
      ).not.toBeInTheDocument(),
    );
  });

  it('bitta talabaga yozish: tasdiqsiz yuboriladi va tafsilot sahifasiga o‘tadi', async () => {
    const user = userEvent.setup();
    renderPage();
    const name = sorted[0]!.fullName;
    await user.click(await screen.findByRole('button', { name: `${name} ga xabar yozish` }));

    const dialog = await screen.findByRole('dialog', { name: 'Xabar yozish' });
    expect(within(dialog).getByTestId('compose-target')).toHaveTextContent(name);
    const send = within(dialog).getByRole('button', { name: 'Yuborish' });
    expect(send).toBeDisabled();
    // Ilova tugmasi sukut bo'yicha yoqiq.
    expect(
      within(dialog).getByRole('checkbox', { name: "Ilovani ochish tugmasini qo'shish" }),
    ).toBeChecked();

    const textarea = within(dialog).getByRole('textbox', { name: 'Xabar matni' });
    expect(textarea).toHaveFocus();
    await user.type(textarea, '<b>Salom</b>{enter}Ertaga');
    expect(within(dialog).getByText('19/4000')).toBeInTheDocument();
    // Oldindan ko'rish — oddiy matn (HTML talqin qilinmaydi).
    expect(within(dialog).getByTestId('compose-preview').textContent).toBe('<b>Salom</b>\nErtaga');
    expect(within(dialog).getByTestId('compose-preview').querySelector('b')).toBeNull();

    await user.click(send);
    expect(await screen.findByTestId('location')).toHaveTextContent('/admin/messages/m3');
  });

  it('ommaviy xabar (filtrsiz) — barcha ulanganlar, tasdiq bosqichi', async () => {
    const user = userEvent.setup();
    let body: unknown = null;
    server.events.on('request:start', async ({ request }) => {
      if (request.method === 'POST' && request.url.endsWith(MESSAGES_ENDPOINT)) {
        body = await request.clone().json();
      }
    });
    renderPage();
    await screen.findByText(sorted[0]!.fullName);
    await user.click(screen.getByRole('button', { name: 'Ommaviy xabar' }));

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByTestId('compose-target')).toHaveTextContent(
      `Barcha ulanganlar — ${mockRecipients.length} ta`,
    );
    await user.type(within(dialog).getByRole('textbox', { name: 'Xabar matni' }), 'Diqqat');
    await user.click(
      within(dialog).getByRole('checkbox', { name: "Ilovani ochish tugmasini qo'shish" }),
    );
    await user.click(within(dialog).getByRole('button', { name: 'Yuborish' }));

    const confirm = await screen.findByRole('dialog', { name: 'Yuborishni tasdiqlang' });
    expect(confirm).toHaveTextContent(
      `${mockRecipients.length} ta talabaga yuboriladi. Davom etasizmi?`,
    );
    const yes = within(confirm).getByRole('button', { name: 'Ha, yuborish' });
    expect(yes).toHaveFocus();
    await user.click(yes);

    expect(await screen.findByTestId('location')).toHaveTextContent('/admin/messages/m3');
    expect(body).toEqual({ text: 'Diqqat', attachAppButton: false, audience: { kind: 'all' } });
    server.events.removeAllListeners();
  });

  it('filtr bo‘yicha ommaviy xabar — filtr tavsifi va taxminiy son', async () => {
    const user = userEvent.setup();
    renderPage('/admin/messages?course=2');
    await waitFor(() => expect(screen.getByText(/^Jami: \d+ ta$/)).toBeInTheDocument());
    const total = mockRecipients.filter((r) => r.course === 2).length;
    await waitFor(() => expect(screen.getByText(`Jami: ${total} ta`)).toBeInTheDocument());
    await user.click(screen.getByRole('button', { name: 'Ommaviy xabar' }));
    expect(await screen.findByTestId('compose-target')).toHaveTextContent(
      `Filtr bo'yicha: 2-kurs — taxminan ${total} ta talaba`,
    );
  });

  it('server xatosi (400) oyna ichida ko‘rsatiladi', async () => {
    server.use(
      http.post(MESSAGES_ENDPOINT, () =>
        problemResponse(
          400,
          "Ma'lumotlar noto'g'ri",
          "Tanlangan auditoriyada Telegram ulangan talaba yo'q.",
        ),
      ),
    );
    const user = userEvent.setup();
    renderPage();
    await user.click(
      await screen.findByRole('button', { name: `${sorted[1]!.fullName} ga xabar yozish` }),
    );
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByRole('textbox', { name: 'Xabar matni' }), 'Salom');
    await user.click(within(dialog).getByRole('button', { name: 'Yuborish' }));
    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      "Tanlangan auditoriyada Telegram ulangan talaba yo'q.",
    );
    expect(screen.queryByTestId('location')).not.toBeInTheDocument();
  });
});

describe('MessagesPage · Yuborilganlar', () => {
  it("yuborilgan xabarlar ro'yxati: auditoriya, progress, holat", async () => {
    renderPage('/admin/messages?tab=sent');
    const table = await screen.findByRole('table', { name: 'Yuborilgan xabarlar' });
    expect(await within(table).findByText(/412-22 guruh talabalari, diqqat!/)).toBeInTheDocument();
    expect(within(table).getAllByText('Yakunlangan')).toHaveLength(2);
    expect(within(table).getByText(/Barcha ulanganlar · Admin Adminov/)).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Yuborilganlar' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
  });

  it('varaqlar almashganda tanlov saqlanadi', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.click(
      await screen.findByRole('checkbox', { name: `${sorted[2]!.fullName} ni tanlash` }),
    );
    await user.click(screen.getByRole('tab', { name: 'Yuborilganlar' }));
    await screen.findByRole('table', { name: 'Yuborilgan xabarlar' });
    await user.click(screen.getByRole('tab', { name: 'Ulanganlar' }));
    expect(
      await screen.findByRole('button', { name: 'Tanlanganlarga yozish (1)' }),
    ).toBeInTheDocument();
  });
});
