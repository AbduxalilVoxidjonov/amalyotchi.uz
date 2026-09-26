import { http, HttpResponse } from 'msw';
import { fireEvent, screen, within } from '@testing-library/react';
import { server } from '@/mocks/server';
import { mockCompanies, mockPastPlace, mockPlace, setMockPlace } from '@/features/place/mocks';
import { renderApp } from '@/test/render-app';

const tinField = () => screen.getByLabelText('Korxona STIR raqami');
const search = () => screen.getByRole('button', { name: 'Qidirish' });
const typeTin = (value: string) => fireEvent.change(tinField(), { target: { value } });

describe('PlacePage (isJoyim)', () => {
  it("korxona ma'lumotlari, shartnoma va geofence", async () => {
    renderApp('/joyim');
    expect(await screen.findByText("Korxona ma'lumotlari")).toBeInTheDocument();
    expect(screen.getByText('Tasdiqlangan')).toBeInTheDocument();
    expect(screen.getByText('304 512 889')).toBeInTheDocument();
    expect(screen.getByText('Islomov B. · +998 90 123 45 67')).toBeInTheDocument();
    expect(screen.getByText('01.10–15.11.2026')).toBeInTheDocument();
    expect(screen.getByText('shartnoma_aliyev.pdf')).toBeInTheDocument();
    expect(screen.getByText('2 bet · 1,8 MB · 24.09.2026 da yuklangan')).toBeInTheDocument();
    expect(screen.getByText('08.10.2026 da tyutor N. Saidova tasdiqladi')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Xarita (nuqta + 150 m doira)' })).toHaveTextContent(
      '41.3111, 69.2797',
    );
  });

  it("joy biriktirilgan bo'lsa STIR formasi ko'rinmaydi", async () => {
    renderApp('/joyim');
    expect(await screen.findByText("Korxona ma'lumotlari")).toBeInTheDocument();
    expect(screen.queryByText('Amaliyot joyini tanlash')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Korxona STIR raqami')).not.toBeInTheDocument();
  });

  it("submitted → forma yo'q, 'ko'rib chiqilmoqda' eslatmasi bor", async () => {
    setMockPlace({ ...mockPlace, status: 'submitted', contract: null });
    renderApp('/joyim');
    expect(await screen.findByText('Tekshiruvda')).toBeInTheDocument();
    expect(screen.getByText("Ariza tyutorga yuborildi — ko'rib chiqilmoqda.")).toBeInTheDocument();
    expect(screen.queryByLabelText('Korxona STIR raqami')).not.toBeInTheDocument();
  });

  it("revisionNeeded → 'Qayta topshirish' + tyutor izohi; shartnoma yo'q; qayta tanlash formasi", async () => {
    server.use(
      http.get('/api/student/place', () =>
        HttpResponse.json({
          ...mockPlace,
          status: 'revisionNeeded',
          comment: 'Shartnomada muhr yo‘q',
          mentorName: null,
          mentorPhone: null,
          contract: null,
        }),
      ),
    );
    renderApp('/joyim');
    expect(await screen.findByText('Qayta topshirish')).toBeInTheDocument();
    expect(screen.getByText('Tyutor izohi: Shartnomada muhr yo‘q')).toBeInTheDocument();
    expect(screen.getByText('Shartnoma hali yuklanmagan')).toBeInTheDocument();
    expect(screen.getByText('—')).toBeInTheDocument(); // mentor yo'q
    expect(screen.getByText('Amaliyot joyini qayta tanlash')).toBeInTheDocument();
    expect(screen.getByLabelText('Korxona STIR raqami')).toBeInTheDocument();
  });

  it("404 → STIR formasi ko'rinadi (korxona ma'lumoti qo'lda kiritilmaydi)", async () => {
    setMockPlace(null);
    renderApp('/joyim');
    expect(await screen.findByText('Amaliyot joyini tanlash')).toBeInTheDocument();
    expect(
      screen.getByText(
        "Korxona ma'lumotini o'zingiz yozmaysiz — STIR raqamini kiriting, qolgani tizimdan topiladi.",
      ),
    ).toBeInTheDocument();
    expect(tinField()).toBeInTheDocument();
    expect(search()).toBeInTheDocument();
  });

  it("noto'g'ri STIR formati → xabar (so'rov yuborilmaydi)", async () => {
    setMockPlace(null);
    renderApp('/joyim');
    await screen.findByText('Amaliyot joyini tanlash');

    typeTin('12345');
    fireEvent.click(search());
    expect(
      await screen.findByText("STIR 9 ta raqamdan iborat bo'lishi kerak. Namuna: 123456789"),
    ).toBeInTheDocument();

    // Harflar umuman kiritilmaydi (faqat raqam, 9 tagacha).
    typeTin('abc12345678901');
    expect(tinField()).toHaveValue('123456789');
  });

  it("noma'lum STIR → 404 xabari (tyutorga murojaat)", async () => {
    setMockPlace(null);
    renderApp('/joyim');
    await screen.findByText('Amaliyot joyini tanlash');

    typeTin('111111111');
    fireEvent.click(search());
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Bu STIR bilan faol korxona topilmadi.',
    );
    expect(
      screen.queryByRole('button', { name: 'Tasdiqlash va yuborish' }),
    ).not.toBeInTheDocument();
  });

  it('mavjud STIR → korxona kartasi → yuborilsa holat "Tekshiruvda" ga o\'tadi', async () => {
    setMockPlace(null);
    renderApp('/joyim');
    await screen.findByText('Amaliyot joyini tanlash');

    const company = mockCompanies[1]!;
    typeTin(company.tin);
    fireEvent.click(search());

    const card = await screen.findByLabelText('Topilgan korxona');
    expect(within(card).getByText(company.name)).toBeInTheDocument();
    expect(within(card).getByText('305 881 204')).toBeInTheDocument();
    expect(within(card).getByText(company.activity)).toBeInTheDocument();
    expect(within(card).getByText(company.address)).toBeInTheDocument();
    expect(within(card).getByText('Qodirov J. · +998 90 777 88 99')).toBeInTheDocument();
    expect(within(card).getByText('120 m')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Tasdiqlash va yuborish' }));

    // POST 201 → `place` invalidate → sahifa "ko'rib chiqilmoqda" holatini ko'rsatadi.
    expect(await screen.findByText("Korxona ma'lumotlari")).toBeInTheDocument();
    expect(screen.getByText('Tekshiruvda')).toBeInTheDocument();
    expect(screen.getByText(company.name)).toBeInTheDocument();
    expect(screen.getByText("Ariza tyutorga yuborildi — ko'rib chiqilmoqda.")).toBeInTheDocument();
    expect(screen.queryByLabelText('Korxona STIR raqami')).not.toBeInTheDocument();
  });

  it("allaqachon tasdiqlangan joy → 409 xabari ko'rinadi", async () => {
    server.use(
      http.get('/api/student/place', () =>
        HttpResponse.json({ ...mockPlace, status: 'rejected', comment: 'Korxona mos emas' }),
      ),
    );
    renderApp('/joyim');
    await screen.findByText('Amaliyot joyini qayta tanlash');

    typeTin(mockCompanies[0]!.tin);
    fireEvent.click(search());
    await screen.findByLabelText('Topilgan korxona');
    fireEvent.click(screen.getByRole('button', { name: 'Tasdiqlash va yuborish' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Sizga allaqachon amaliyot joyi biriktirilgan.',
    );
  });

  describe("o'tgan amaliyot davri (isPast)", () => {
    it("sarlavha, davr nomi va sanalari, neytral 'Yakunlangan'; harakatlar yo'q", async () => {
      setMockPlace(mockPastPlace);
      renderApp('/joyim');
      const card = await screen.findByRole('region', { name: "O'tgan amaliyot davri" });
      expect(
        within(card).getByRole('heading', { name: "O'tgan amaliyot davri" }),
      ).toBeInTheDocument();
      expect(
        within(card).getByText('Bahorgi amaliyot 2026 · 02.02–28.03.2026'),
      ).toBeInTheDocument();
      expect(within(card).getByText('Yakunlangan')).toBeInTheDocument();
      expect(
        within(card).getByText('Hozirda siz hech bir korxonaga biriktirilmagansiz.'),
      ).toBeInTheDocument();
      // Faqat o'qish uchun ma'lumot qoladi.
      expect(within(card).getByText(mockPastPlace.company)).toBeInTheDocument();
      expect(within(card).getByText('304 512 889')).toBeInTheDocument();
      expect(within(card).getByText(mockPastPlace.address)).toBeInTheDocument();
      expect(screen.getByRole('img', { name: 'Xarita (nuqta + 150 m doira)' })).toBeInTheDocument();
      expect(screen.getByText('shartnoma_aliyev.pdf')).toBeInTheDocument();

      // "Hozirgi korxona" ma'nosidagi matnlar va harakatlar yo'q.
      expect(screen.queryByText("Korxona ma'lumotlari")).not.toBeInTheDocument();
      expect(screen.queryByText('Tasdiqlangan')).not.toBeInTheDocument();
      expect(screen.queryByText(/koordinatani o'zgartirish mumkin emas/)).not.toBeInTheDocument();
      expect(
        screen.queryByRole('button', { name: 'Shablonni yuklab olish' }),
      ).not.toBeInTheDocument();
      expect(screen.queryByText('Amaliyot joyini qayta tanlash')).not.toBeInTheDocument();
    });

    it("o'tgan davrda rad etilgan ariza → qayta topshirish/izoh yo'q; shartnoma bo'lmasa karta yo'q", async () => {
      setMockPlace({
        ...mockPastPlace,
        status: 'rejected',
        comment: 'Korxona mos emas',
        contract: null,
      });
      renderApp('/joyim');
      await screen.findByRole('heading', { name: "O'tgan amaliyot davri" });
      expect(screen.getByText('Yakunlangan')).toBeInTheDocument();
      expect(screen.queryByText('Rad etilgan')).not.toBeInTheDocument();
      expect(screen.queryByText('Tyutor izohi: Korxona mos emas')).not.toBeInTheDocument();
      expect(screen.queryByText('Amaliyot joyini qayta tanlash')).not.toBeInTheDocument();
      expect(screen.queryByText('Shartnoma hali yuklanmagan')).not.toBeInTheDocument();
      expect(screen.queryByRole('heading', { name: 'Shartnoma' })).not.toBeInTheDocument();
    });

    it("ochiq yangi davr bo'lsa — shu davr uchun STIR orqali joy tanlash formasi", async () => {
      setMockPlace(mockPastPlace);
      renderApp('/joyim');
      await screen.findByRole('heading', { name: "O'tgan amaliyot davri" });
      // today mock: "Kuzgi amaliyot 2026" davom etmoqda, o'tgan joy esa bahorgi davrga tegishli.
      expect(screen.getByText('Amaliyot joyini tanlash')).toBeInTheDocument();
      expect(screen.getByText('Kuzgi amaliyot 2026 uchun')).toBeInTheDocument();

      typeTin(mockCompanies[1]!.tin);
      fireEvent.click(search());
      await screen.findByLabelText('Topilgan korxona');
      fireEvent.click(screen.getByRole('button', { name: 'Tasdiqlash va yuborish' }));

      // POST 201 → yangi davrdagi ariza: odatiy "Korxona ma'lumotlari" ko'rinishi.
      expect(await screen.findByText("Korxona ma'lumotlari")).toBeInTheDocument();
      expect(screen.getByText('Tekshiruvda')).toBeInTheDocument();
      expect(screen.queryByText("O'tgan amaliyot davri")).not.toBeInTheDocument();
    });

    it("isPast: false → hozirgi ko'rinish (regressiya)", async () => {
      setMockPlace({ ...mockPlace, isPast: false });
      renderApp('/joyim');
      expect(await screen.findByText("Korxona ma'lumotlari")).toBeInTheDocument();
      expect(screen.getByText('Tasdiqlangan')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Shablonni yuklab olish' })).toBeInTheDocument();
      expect(screen.queryByText("O'tgan amaliyot davri")).not.toBeInTheDocument();
      expect(
        screen.queryByText('Hozirda siz hech bir korxonaga biriktirilmagansiz.'),
      ).not.toBeInTheDocument();
    });

    it("eski server (isPast/periodName yo'q) → hozirgi ko'rinish", async () => {
      const { isPast, periodName, periodId, ...legacy } = mockPlace;
      server.use(http.get('/api/student/place', () => HttpResponse.json(legacy)));
      renderApp('/joyim');
      expect(await screen.findByText("Korxona ma'lumotlari")).toBeInTheDocument();
      expect(screen.getByText('Tasdiqlangan')).toBeInTheDocument();
      expect(screen.queryByText("O'tgan amaliyot davri")).not.toBeInTheDocument();
      expect(screen.queryByText('Yakunlangan')).not.toBeInTheDocument();
    });
  });
});
