import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { renderWithProviders } from '../shared/renderWithProviders';
import { SETTINGS_ENDPOINT } from './api';
import { mockSettings, resetSettingsMock } from './mocks';
import { SettingsPage } from './SettingsPage';
import type { AdminSettings, SettingsUpdate } from './types';

/** PUT so'rovlari tanasini yozib boradi, javobni asosiy mock handler'ga qoldiradi. */
function capturePuts(): SettingsUpdate[] {
  const bodies: SettingsUpdate[] = [];
  server.use(
    http.put(SETTINGS_ENDPOINT, async ({ request }) => {
      bodies.push((await request.clone().json()) as SettingsUpdate);
      return undefined;
    }),
  );
  return bodies;
}

function group(name: string | RegExp): HTMLElement {
  return screen.getByRole('region', { name });
}

describe('SettingsPage', () => {
  afterEach(() => resetSettingsMock());

  it("guruhlar, qiymatlar (switch/chip/raqam + birlik) va bayramlarni ko'rsatadi", async () => {
    renderWithProviders(<SettingsPage />);
    expect(await screen.findByRole('region', { name: 'Davomat va geofence' })).toBeInTheDocument();
    expect(group('Talaba hisoboti — nimalar majburiy')).toBeInTheDocument();
    expect(group('Korxonalar')).toBeInTheDocument();
    expect(group('Bayram va dam olish kunlari')).toBeInTheDocument();
    // Barcha kalitlar ma'lum → "Boshqa" chiqmaydi.
    expect(screen.queryByRole('region', { name: 'Boshqa' })).not.toBeInTheDocument();

    const attendance = group('Davomat va geofence');
    expect(within(attendance).getByLabelText('Standart geofence radiusi')).toHaveValue(200);
    expect(within(attendance).getByLabelText('Check-in oynasi')).toHaveValue(90);
    expect(within(attendance).getAllByText('daqiqa').length).toBeGreaterThan(0);
    const days = within(attendance).getByRole('group', { name: 'Ish kunlari' });
    expect(within(days).getByRole('button', { name: 'Dushanba' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    expect(within(days).getByRole('button', { name: 'Yakshanba' })).toHaveAttribute(
      'aria-pressed',
      'false',
    );

    const report = group('Talaba hisoboti — nimalar majburiy');
    expect(within(report).getByRole('switch', { name: 'Kundalik hisobot majburiy' })).toBeChecked();
    expect(
      within(report).getByRole('switch', { name: 'Hisobotga PDF majburiy' }),
    ).not.toBeChecked();
    expect(
      within(report).getByRole('switch', { name: 'Check-in uchun rasm majburiy' }),
    ).toBeInTheDocument();
    expect(within(group('Korxonalar')).getByLabelText('Korxonaga maksimal talaba')).toHaveValue(10);

    expect(screen.getByText("Navro'z bayrami")).toBeInTheDocument();
    expect(screen.getByText('20.03.2027')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Saqlash' })).toBeDisabled();
  });

  it("'Hujjat shablonlari' kartasi ko'rsatilmaydi", async () => {
    renderWithProviders(<SettingsPage />);
    await screen.findByRole('region', { name: 'Davomat va geofence' });
    expect(screen.queryByText('Hujjat shablonlari')).not.toBeInTheDocument();
    expect(screen.queryByText('Shablonlar hali yuklanmagan.')).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'shartnoma_shablon.docx' }),
    ).not.toBeInTheDocument();
  });

  it('diaryPdfRequired switch → Saqlash → PUT faqat shu kalit bilan', async () => {
    const user = userEvent.setup();
    const bodies = capturePuts();
    renderWithProviders(<SettingsPage />);
    await screen.findByRole('region', { name: 'Davomat va geofence' });
    const pdf = within(group('Talaba hisoboti — nimalar majburiy')).getByRole('switch', {
      name: 'Hisobotga PDF majburiy',
    });

    await user.click(pdf);
    expect(pdf).toBeChecked();
    await user.click(screen.getByRole('button', { name: 'Saqlash' }));

    expect(await screen.findByRole('status')).toHaveTextContent('Saqlandi');
    expect(bodies).toEqual([{ values: { diaryPdfRequired: 'true' } }]);
    expect(pdf).toBeChecked();
  });

  it("workDays chip'lari → to'g'ri CSV yuboriladi", async () => {
    const user = userEvent.setup();
    const bodies = capturePuts();
    renderWithProviders(<SettingsPage />);
    const days = await screen.findByRole('group', { name: 'Ish kunlari' });

    await user.click(within(days).getByRole('button', { name: 'Shanba' })); // 6 o'chadi
    await user.click(within(days).getByRole('button', { name: 'Yakshanba' })); // 7 yoqiladi
    await user.click(within(days).getByRole('button', { name: 'Seshanba' })); // 2 o'chadi
    expect(within(days).getByRole('button', { name: 'Shanba' })).toHaveAttribute(
      'aria-pressed',
      'false',
    );
    await user.click(screen.getByRole('button', { name: 'Saqlash' }));

    expect(await screen.findByRole('status')).toHaveTextContent('Saqlandi');
    expect(bodies).toEqual([{ values: { workDays: '1,3,4,5,7' } }]);
  });

  it('noma\'lum kalit "Boshqa" guruhida chiqadi', async () => {
    const data: AdminSettings = {
      ...mockSettings,
      settings: [
        ...mockSettings.settings,
        {
          key: 'futureFlag',
          label: 'Kelajakdagi sozlama',
          value: 'false',
          type: 'bool',
          unit: null,
          note: "Backend keyin qo'shgan kalit.",
          min: null,
          max: null,
          updatedAt: null,
        },
      ],
    };
    server.use(http.get(SETTINGS_ENDPOINT, () => HttpResponse.json(data)));
    renderWithProviders(<SettingsPage />);
    const other = await screen.findByRole('region', { name: 'Boshqa' });
    expect(within(other).getByRole('switch', { name: 'Kelajakdagi sozlama' })).toBeInTheDocument();
    expect(
      within(group('Davomat va geofence')).queryByText('Kelajakdagi sozlama'),
    ).not.toBeInTheDocument();
  });

  it("o'zgartirish → Saqlash → PUT → yangi qiymat saqlanadi", async () => {
    const user = userEvent.setup();
    const bodies = capturePuts();
    renderWithProviders(<SettingsPage />);
    const input = await screen.findByLabelText('Standart geofence radiusi');

    await user.clear(input);
    await user.type(input, '250');
    expect(screen.getByRole('button', { name: 'Saqlash' })).toBeEnabled();

    await user.click(screen.getByRole('button', { name: 'Saqlash' }));
    expect(await screen.findByRole('status')).toHaveTextContent('Saqlandi');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Saqlash' })).toBeDisabled());
    expect(screen.getByLabelText('Standart geofence radiusi')).toHaveValue(250);
    expect(bodies).toEqual([{ values: { geofenceRadius: '250' } }]);
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
    await user.click(screen.getByRole('switch', { name: 'Kundalik hisobot majburiy' }));
    await user.click(screen.getByRole('button', { name: 'Bekor qilish' }));
    expect(input).toHaveValue(15);
    expect(screen.getByRole('switch', { name: 'Kundalik hisobot majburiy' })).toBeChecked();
    expect(screen.getByRole('button', { name: 'Saqlash' })).toBeDisabled();
  });
});
