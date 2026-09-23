import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderHierarchyPage } from '../shared/renderHierarchyPage';
import { resetPracticePeriodsMock } from './mocks';
import { PracticePeriodDetailPage } from './PracticePeriodDetailPage';

function renderPage(periodId = 'p1') {
  return renderHierarchyPage(<PracticePeriodDetailPage />, '/admin/practice-periods/:periodId', [
    `/admin/practice-periods/${periodId}`,
  ]);
}

function groupsTable() {
  return within(screen.getByRole('table', { name: 'Biriktirilgan guruhlar' }));
}

describe('PracticePeriodDetailPage', () => {
  afterEach(() => resetPracticePeriodsMock());

  it("sarlavha, status, ma'lumot kartasi va biriktirilgan guruhlar", async () => {
    renderPage();
    expect(
      await screen.findByRole('heading', { name: 'Ishlab chiqarish amaliyoti 2026' }),
    ).toBeInTheDocument();
    expect(screen.getByText('Faol')).toHaveAttribute('data-status', 'ok');
    expect(screen.getByRole('link', { name: 'Amaliyot davrlari' })).toHaveAttribute(
      'href',
      '/admin/practice-periods',
    );

    const info = within(screen.getByRole('region', { name: "Davr ma'lumotlari" }));
    expect(info.getByText('31.08.2026 — 14.10.2026')).toBeInTheDocument();
    expect(info.getByText('09:00–17:00')).toBeInTheDocument();
    expect(info.getByLabelText('Dushanba: ish kuni')).toBeInTheDocument();
    expect(info.getByLabelText('Yakshanba: dam olish')).toBeInTheDocument();
    expect(info.getByText('Kundalik hisobot majburiy')).toBeInTheDocument();

    const t = groupsTable();
    expect(t.getByText('412-22')).toBeInTheDocument();
    expect(t.getByText('221-23')).toBeInTheDocument();
    expect(t.getAllByText('Kompyuter injiniringi kafedrasi')).toHaveLength(2);
    expect(t.getByRole('link', { name: 'Bank ishi' })).toHaveAttribute(
      'href',
      '/admin/faculties/f2/departments/d3/directions/dir3',
    );
  });

  it('ajratish: guruh jadvaldan chiqadi; davomati bor guruh — 409', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('413-22');

    await user.click(groupsTable().getByRole('button', { name: '413-22 guruhini ajratish' }));
    let dialog = await screen.findByRole('dialog', { name: 'Guruhni ajratish' });
    await user.click(within(dialog).getByRole('button', { name: 'Ajratish' }));
    await waitFor(() => expect(screen.queryByText('413-22')).not.toBeInTheDocument());

    await user.click(groupsTable().getByRole('button', { name: '412-22 guruhini ajratish' }));
    dialog = await screen.findByRole('dialog', { name: 'Guruhni ajratish' });
    await user.click(within(dialog).getByRole('button', { name: 'Ajratish' }));
    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      "412-22 guruhi talabalarining shu davrda davomat yozuvlari bor — ajratib bo'lmaydi.",
    );
    expect(groupsTable().getByText('412-22')).toBeInTheDocument();
  });

  it("guruh qo'shish: mavjudlari oldindan belgilangan, yangisi PUT bilan qo'shiladi", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('412-22');

    await user.click(screen.getByRole('button', { name: "Guruh qo'shish" }));
    const dialog = within(await screen.findByRole('dialog', { name: "Guruh qo'shish" }));
    const panel = within(dialog.getByRole('region', { name: 'Tanlangan guruhlar' }));
    expect(panel.getByRole('heading', { name: /Tanlangan guruhlar \(3\)/ })).toBeInTheDocument();
    expect(dialog.getByRole('button', { name: 'Saqlash' })).toBeDisabled();

    await waitFor(() => expect(dialog.getByLabelText('Fakultet')).toBeEnabled());
    await user.selectOptions(dialog.getByLabelText('Fakultet'), 'Axborot texnologiyalari');
    await dialog.findByRole('option', { name: 'Kompyuter injiniringi kafedrasi' });
    await user.selectOptions(dialog.getByLabelText('Kafedra'), 'Kompyuter injiniringi kafedrasi');
    await dialog.findByRole('option', { name: 'Kompyuter injiniringi' });
    await user.selectOptions(dialog.getByLabelText("Yo'nalish"), 'Kompyuter injiniringi');
    // O'z davri (p1) guruhlari band emas va belgilangan.
    const g1 = await dialog.findByRole('checkbox', { name: /412-22/ });
    expect(g1).toBeChecked();
    expect(g1).toBeEnabled();

    await user.selectOptions(dialog.getByLabelText("Yo'nalish"), 'Dasturiy injiniring');
    await user.click(await dialog.findByRole('checkbox', { name: /421-23/ }));
    expect(panel.getByRole('heading', { name: /Tanlangan guruhlar \(4\)/ })).toBeInTheDocument();
    await user.click(dialog.getByRole('button', { name: 'Saqlash' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(await groupsTable().findByText('421-23')).toBeInTheDocument();
  });

  it('yopish: tasdiqlashdan keyin status "Yopilgan", tahrirlash amallari yashiriladi', async () => {
    const user = userEvent.setup();
    renderPage('p3');
    await screen.findByRole('heading', { name: 'Qishki amaliyot 2027' });
    expect(screen.getByText('Rejada')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Yopish' }));
    const dialog = await screen.findByRole('dialog', { name: 'Davrni yopish' });
    await user.click(within(dialog).getByRole('button', { name: 'Ha, yopish' }));

    expect(await screen.findByText('Yopilgan')).toHaveAttribute('data-status', 'neu');
    expect(screen.queryByRole('button', { name: 'Tahrirlash' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Yopish' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: "Guruh qo'shish" })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /guruhini ajratish/ })).not.toBeInTheDocument();
  });

  it('tahrirlash: faol davrda boshlanish sanasi o‘chiq; nom saqlanadi', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByRole('heading', { name: 'Ishlab chiqarish amaliyoti 2026' });

    await user.click(screen.getByRole('button', { name: 'Tahrirlash' }));
    const dialog = within(await screen.findByRole('dialog', { name: 'Davrni tahrirlash' }));
    expect(dialog.getByLabelText('Boshlanish sanasi')).toBeDisabled();
    expect(dialog.getByLabelText('Tugash sanasi')).toBeEnabled();

    await user.clear(dialog.getByLabelText('Nomi'));
    await user.type(dialog.getByLabelText('Nomi'), 'Ishlab chiqarish amaliyoti (kuz)');
    await user.click(dialog.getByRole('button', { name: 'Saqlash' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(
      await screen.findByRole('heading', { name: 'Ishlab chiqarish amaliyoti (kuz)' }),
    ).toBeInTheDocument();
  });

  it("o'chirish: davomati bor davr — 409; bo'sh davr o'chirilib ro'yxatga qaytadi", async () => {
    const user = userEvent.setup();
    const { unmount } = renderPage('p1');
    await screen.findByRole('heading', { name: 'Ishlab chiqarish amaliyoti 2026' });
    await user.click(screen.getByRole('button', { name: "O'chirish" }));
    let dialog = await screen.findByRole('dialog', { name: "Davrni o'chirish" });
    await user.click(within(dialog).getByRole('button', { name: "O'chirish" }));
    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      "Davrda davomat yozuvlari bor — uni o'chirib bo'lmaydi.",
    );
    unmount();

    renderPage('p3');
    await screen.findByRole('heading', { name: 'Qishki amaliyot 2027' });
    await user.click(screen.getByRole('button', { name: "O'chirish" }));
    dialog = await screen.findByRole('dialog', { name: "Davrni o'chirish" });
    await user.click(within(dialog).getByRole('button', { name: "O'chirish" }));
    expect(await screen.findByTestId('location')).toHaveTextContent('/admin/practice-periods');
  });

  it('404: davr topilmadi', async () => {
    renderPage('nope');
    expect(await screen.findByText('Amaliyot davri topilmadi.')).toBeInTheDocument();
  });
});
