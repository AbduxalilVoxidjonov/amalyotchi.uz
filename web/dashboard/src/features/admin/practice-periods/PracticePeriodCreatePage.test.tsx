import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderHierarchyPage } from '../shared/renderHierarchyPage';
import { resetPracticePeriodsMock } from './mocks';
import { PracticePeriodCreatePage } from './PracticePeriodCreatePage';

type User = ReturnType<typeof userEvent.setup>;

function renderPage() {
  return renderHierarchyPage(<PracticePeriodCreatePage />, '/admin/practice-periods/new', [
    '/admin/practice-periods/new',
  ]);
}

function setDates(start: string, end: string) {
  fireEvent.change(screen.getByLabelText('Boshlanish sanasi'), { target: { value: start } });
  fireEvent.change(screen.getByLabelText('Tugash sanasi'), { target: { value: end } });
}

/** Kaskad: Fakultet → Kafedra → Yo'nalish (har bosqich serverdan kelishini kutadi). */
async function pickDirection(user: User, faculty: string, department: string, direction: string) {
  await waitFor(() => expect(screen.getByLabelText('Fakultet')).toBeEnabled());
  await user.selectOptions(screen.getByLabelText('Fakultet'), faculty);
  await waitFor(() => expect(screen.getByRole('option', { name: department })).toBeInTheDocument());
  await user.selectOptions(screen.getByLabelText('Kafedra'), department);
  await waitFor(() => expect(screen.getByRole('option', { name: direction })).toBeInTheDocument());
  await user.selectOptions(screen.getByLabelText("Yo'nalish"), direction);
  await screen.findByRole('group', { name: `${direction} guruhlari` });
}

function selectedPanel() {
  return within(screen.getByRole('region', { name: 'Tanlangan guruhlar' }));
}

describe('PracticePeriodCreatePage', () => {
  afterEach(() => resetPracticePeriodsMock());

  it('kaskad: yuqori daraja o‘zgarsa pastkilari tozalanadi', async () => {
    const user = userEvent.setup();
    renderPage();
    await pickDirection(
      user,
      'Axborot texnologiyalari',
      'Kompyuter injiniringi kafedrasi',
      'Kompyuter injiniringi',
    );
    expect(screen.getByRole('checkbox', { name: /412-22/ })).toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText('Fakultet'), 'Iqtisodiyot va moliya');
    expect(screen.getByLabelText('Kafedra')).toHaveValue('');
    expect(screen.getByLabelText("Yo'nalish")).toHaveValue('');
    expect(screen.getByLabelText("Yo'nalish")).toBeDisabled();
    expect(screen.queryByRole('checkbox', { name: /412-22/ })).not.toBeInTheDocument();
    // Faol bo'lmagan kafedra (d4) ro'yxatda yo'q.
    await screen.findByRole('option', { name: 'Moliya kafedrasi' });
    expect(screen.queryByRole('option', { name: 'Iqtisodiyot kafedrasi' })).not.toBeInTheDocument();
  });

  it('tanlov turli yo‘nalish va fakultetlar bo‘ylab saqlanadi; chip × va tozalash', async () => {
    const user = userEvent.setup();
    renderPage();
    setDates('2026-11-02', '2026-12-15');
    expect(screen.getByText('≈ 44 kalendar kun')).toBeInTheDocument();

    await pickDirection(
      user,
      'Axborot texnologiyalari',
      'Kompyuter injiniringi kafedrasi',
      'Kompyuter injiniringi',
    );
    // p1 bilan kesishmaydi — tanlash mumkin, lekin info ko'rsatiladi.
    const g2 = screen.getByRole('checkbox', { name: /413-22/ });
    expect(g2).toBeEnabled();
    expect(g2).toHaveAccessibleDescription(/Boshqa davrda: Ishlab chiqarish amaliyoti 2026/);
    await user.click(g2);

    await user.selectOptions(screen.getByLabelText("Yo'nalish"), 'Dasturiy injiniring');
    await user.click(await screen.findByRole('checkbox', { name: /421-23/ }));

    await pickDirection(user, 'Iqtisodiyot va moliya', 'Moliya kafedrasi', 'Bank ishi');
    await user.click(screen.getByRole('checkbox', { name: /221-23/ }));

    const panel = selectedPanel();
    expect(panel.getByRole('heading', { name: /Tanlangan guruhlar \(3\)/ })).toBeInTheDocument();
    expect(panel.getByText('Axborot texnologiyalari › Kompyuter injiniringi')).toBeInTheDocument();
    expect(panel.getByText('Axborot texnologiyalari › Dasturiy injiniring')).toBeInTheDocument();
    expect(panel.getByText('Iqtisodiyot va moliya › Bank ishi')).toBeInTheDocument();

    await user.click(panel.getByRole('button', { name: '413-22 guruhini olib tashlash' }));
    expect(panel.getByRole('heading', { name: /Tanlangan guruhlar \(2\)/ })).toBeInTheDocument();

    await user.click(panel.getByRole('button', { name: 'Hammasini tozalash' }));
    expect(panel.getByRole('heading', { name: /Tanlangan guruhlar \(0\)/ })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: /221-23/ })).not.toBeChecked();
  });

  it('band guruh o‘chiq, "Hammasini tanlash" faqat bo‘shlarini oladi (indeterminate)', async () => {
    const user = userEvent.setup();
    renderPage();
    setDates('2026-09-01', '2026-10-30');
    await pickDirection(
      user,
      'Axborot texnologiyalari',
      'Kompyuter injiniringi kafedrasi',
      'Kompyuter injiniringi',
    );

    const g1 = screen.getByRole('checkbox', { name: /412-22/ });
    expect(g1).toBeDisabled();
    expect(
      screen.getAllByText('Band: Ishlab chiqarish amaliyoti 2026 (31.08 — 14.10)'),
    ).toHaveLength(2);
    // Hamma guruh band — "Hammasini tanlash" ham o'chiq.
    expect(screen.getByRole('checkbox', { name: 'Hammasini tanlash' })).toBeDisabled();

    // Oraliq p1 dan keyinga surilsa — tanlash mumkin; bittasi tanlansa indeterminate.
    setDates('2026-10-15', '2026-11-30');
    expect(g1).toBeEnabled();
    await user.click(g1);
    const all = screen.getByRole('checkbox', { name: 'Hammasini tanlash' });
    expect(all).toBePartiallyChecked();
    await user.click(all);
    expect(all).toBeChecked();
    expect(selectedPanel().getByRole('heading', { name: /\(2\)/ })).toBeInTheDocument();
  });

  it('forma to‘g‘ri va kamida 1 guruh bo‘lmaguncha "Davr yaratish" o‘chiq; tugash < boshlanish xatosi', async () => {
    const user = userEvent.setup();
    renderPage();
    const submit = screen.getByRole('button', { name: 'Davr yaratish' });
    expect(submit).toBeDisabled();

    await user.type(screen.getByLabelText('Nomi'), 'Kuzgi amaliyot');
    setDates('2026-11-10', '2026-11-01');
    expect(
      screen.getByText("Tugash sanasi boshlanish sanasidan oldin bo'lishi mumkin emas."),
    ).toBeInTheDocument();
    expect(submit).toBeDisabled();

    setDates('2026-11-01', '2026-11-30');
    expect(screen.getByText('Kamida bitta guruh tanlang.')).toBeInTheDocument();
    expect(submit).toBeDisabled();
    expect(screen.getByRole('link', { name: 'Sozlamalarni ochish' })).toHaveAttribute(
      'href',
      '/admin/settings',
    );
  });

  it('409 (ustma-ust): server detail forma tepasida ko‘rinadi', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.type(screen.getByLabelText('Nomi'), 'Qishki qo‘shimcha');
    setDates('2027-01-15', '2027-02-01');
    await pickDirection(
      user,
      'Axborot texnologiyalari',
      'Kompyuter injiniringi kafedrasi',
      'Dasturiy injiniring',
    );
    await user.click(screen.getByRole('checkbox', { name: /421-23/ }));
    await user.click(screen.getByRole('button', { name: 'Davr yaratish' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent("Davrni yaratib bo'lmadi");
    expect(alert).toHaveTextContent(
      'Quyidagi guruhlar shu sanalarda boshqa davrga biriktirilgan: 421-23 (Qishki amaliyot 2027)',
    );
    expect(screen.queryByTestId('location')).not.toBeInTheDocument();
  });

  it('muvaffaqiyat: davr sahifasiga o‘tadi', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.type(screen.getByLabelText('Nomi'), 'Kuzgi amaliyot 2026');
    setDates('2026-11-02', '2026-12-15');
    await pickDirection(user, 'Iqtisodiyot va moliya', 'Moliya kafedrasi', 'Bank ishi');
    await user.click(screen.getByRole('checkbox', { name: /221-23/ }));
    await user.click(screen.getByRole('button', { name: 'Davr yaratish' }));

    expect(await screen.findByTestId('location')).toHaveTextContent(
      /^\/admin\/practice-periods\/p\d+$/,
    );
  });
});
