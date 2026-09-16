import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { resetSettingsMock } from './mocks';
import { renderWithProviders } from '../shared/renderWithProviders';
import { SettingsPage } from './SettingsPage';

describe('SettingsPage', () => {
  afterEach(() => resetSettingsMock());

  it("sozlamalar (xom qiymat + birlik), bayramlar va shablonlarni ko'rsatadi", async () => {
    renderWithProviders(<SettingsPage />);
    expect(await screen.findByText('Global qoidalar')).toBeInTheDocument();
    expect(screen.getByLabelText('Standart geofence radiusi')).toHaveValue(200);
    expect(screen.getByLabelText('Kundalik hisobot majburiy')).toHaveValue('true');
    expect(screen.getByLabelText('Ish kunlari')).toHaveValue('1,2,3,4,5,6');
    expect(screen.getAllByText('daqiqa').length).toBeGreaterThan(0);
    expect(screen.getByText("21.03 · Navro'z bayrami")).toBeInTheDocument();
    expect(screen.getByText("20.03.2027 · Navro'z (ko'chirilgan dam olish)")).toBeInTheDocument();
    // Fayl token bilan ochiladi (`AuthFileButton`) — oddiy havola emas, tugma.
    expect(screen.getByRole('button', { name: 'shartnoma_shablon.docx' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Saqlash' })).toBeDisabled();
  });

  it("o'zgartirish → Saqlash → PUT → yangi qiymat saqlanadi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<SettingsPage />);
    const input = await screen.findByLabelText('Standart geofence radiusi');

    await user.clear(input);
    await user.type(input, '250');
    expect(screen.getByRole('button', { name: 'Saqlash' })).toBeEnabled();

    await user.click(screen.getByRole('button', { name: 'Saqlash' }));
    expect(await screen.findByRole('status')).toHaveTextContent('Saqlandi');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Saqlash' })).toBeDisabled());
    expect(screen.getByLabelText('Standart geofence radiusi')).toHaveValue(250);
  });

  it('chegaradan tashqari qiymat → 400 errors{key} → maydon ostida xato', async () => {
    const user = userEvent.setup();
    renderWithProviders(<SettingsPage />);
    const input = await screen.findByLabelText('Standart geofence radiusi');
    await user.clear(input);
    await user.type(input, '5');
    await user.click(screen.getByRole('button', { name: 'Saqlash' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      "'geofenceRadius' 50–1000 oralig'ida bo'lishi kerak.",
    );
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(screen.queryByText("Ma'lumotni yuklab bo'lmadi")).not.toBeInTheDocument();
  });

  it("Bekor qilish draft'ni tozalaydi", async () => {
    const user = userEvent.setup();
    renderWithProviders(<SettingsPage />);
    const input = await screen.findByLabelText('Kechikish chegarasi');
    await user.clear(input);
    await user.type(input, '20');
    await user.click(screen.getByRole('button', { name: 'Bekor qilish' }));
    expect(input).toHaveValue(15);
  });
});
