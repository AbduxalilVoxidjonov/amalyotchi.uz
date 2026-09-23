import { fireEvent, screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { mockDiary } from '@/features/diary/mocks';
import type { DiaryEntryDto } from '@/features/diary/types';
import { mockToday, setDiaryPdfRequired } from '@/features/today/mocks';
import { problem } from '@/mocks/problem';
import { server } from '@/mocks/server';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import { renderApp } from '@/test/render-app';

const LONG_TEXT =
  'Bugun mentor bilan birga mijozlar ro‘yxati sahifasining filtrlash mantiqini qayta yozdik. ' +
  'Avval so‘rovlar sekin ishlayotgan edi, indeks qo‘shib, natijani Postman orqali tekshirdik. ' +
  'Kechga yaqin hisobot tayyorladim.';

describe('DiaryPage (kundaligim)', () => {
  it("o'z yozuvlari ro'yxati — tyutor tugmalarisiz", async () => {
    renderApp('/kundalik');
    expect(await screen.findByText('Yozuvlarim · 4')).toBeInTheDocument();
    expect(screen.getByText('Yuborilgan')).toBeInTheDocument();
    expect(screen.getByText("Tyutor ko'rdi")).toBeInTheDocument();
    expect(screen.getByText('Qayta yozish kerak')).toBeInTheDocument();
    expect(screen.getByText('Tasdiqlangan')).toBeInTheDocument();
    expect(screen.getByText('11.10.2026 · 17:42')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'ekran_surati_1.png' })).toHaveAttribute(
      'href',
      '/files/ekran_surati_1.png',
    );
    expect(screen.queryByRole('button', { name: 'Tasdiqlash' })).not.toBeInTheDocument();
    expect(screen.getByText(/Tyutor izohi:/)).toBeInTheDocument();
  });

  it("qisqa matn — mijoz validatsiyasi; to'liq matn → yangi yozuv ro'yxat boshida", async () => {
    renderApp('/kundalik');
    await screen.findByText('Yozuvlarim · 4');
    const ta = screen.getByPlaceholderText('Bugun bajarilgan ishlar — kamida 150 belgi');

    fireEvent.change(ta, { target: { value: 'Qisqa' } });
    fireEvent.click(screen.getByRole('button', { name: 'Yuborish' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Kamida 150 belgi yozing (hozir 5).',
    );

    fireEvent.change(ta, { target: { value: LONG_TEXT } });
    fireEvent.change(screen.getByPlaceholderText("O'rganilgan yangilik (ixtiyoriy)"), {
      target: { value: 'Indekslar' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Yuborish' }));

    expect(await screen.findByText('Kundalik yuborildi.')).toBeInTheDocument();
    expect(screen.getByText('Yozuvlarim · 5')).toBeInTheDocument();
    const articles = screen.getAllByRole('article');
    expect(articles[0]).toHaveTextContent(LONG_TEXT);
    expect(articles[0]).toHaveTextContent('Indekslar');
    expect(ta).toHaveValue('');
  });

  it('bugungisi allaqachon bor → 409 xabari formada', async () => {
    renderApp('/kundalik');
    await screen.findByText('Yozuvlarim · 4');
    const ta = screen.getByPlaceholderText('Bugun bajarilgan ishlar — kamida 150 belgi');
    fireEvent.change(ta, { target: { value: LONG_TEXT } });
    fireEvent.click(screen.getByRole('button', { name: 'Yuborish' }));
    await screen.findByText('Kundalik yuborildi.');

    fireEvent.change(ta, { target: { value: LONG_TEXT } });
    fireEvent.click(screen.getByRole('button', { name: 'Yuborish' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Bugungi hisobot allaqachon yuborilgan.',
    );
    expect(screen.getByText('Yozuvlarim · 5')).toBeInTheDocument();
  });

  describe('diaryPdfRequired', () => {
    const PDF_MSG = 'Hisobotga PDF fayl biriktirilishi shart.';
    afterEach(() => server.events.removeAllListeners());

    function pdf(name = 'hisobot.pdf'): File {
      return new File([new Uint8Array(32)], name, { type: 'application/pdf' });
    }

    function attach(file: File) {
      fireEvent.change(screen.getByLabelText('Fayl tanlash'), { target: { files: [file] } });
    }

    async function fillText() {
      await screen.findByText('Yozuvlarim · 4');
      fireEvent.change(screen.getByPlaceholderText('Bugun bajarilgan ishlar — kamida 150 belgi'), {
        target: { value: LONG_TEXT },
      });
    }

    it("true va PDF yo'q → eslatma ko'rinadi, yuborilmaydi", async () => {
      setDiaryPdfRequired(true);
      let posted = 0;
      server.events.on('request:start', ({ request }) => {
        if (request.method === 'POST' && request.url.endsWith(STUDENT_ENDPOINTS.diary)) posted++;
      });
      renderApp('/kundalik');
      await fillText();
      expect(await screen.findByText('PDF hisobot majburiy')).toBeInTheDocument();

      // Rasm PDF emas — talab bajarilmaydi.
      attach(new File([new Uint8Array(8)], 'rasm.png', { type: 'image/png' }));
      fireEvent.click(screen.getByRole('button', { name: 'Yuborish' }));

      expect(await screen.findByRole('alert')).toHaveTextContent(PDF_MSG);
      expect(screen.getByText('PDF hisobot majburiy')).toBeInTheDocument();
      expect(screen.queryByText('Kundalik yuborildi.')).not.toBeInTheDocument();
      expect(screen.getByText('Yozuvlarim · 4')).toBeInTheDocument();
      expect(posted).toBe(0);
    });

    it('true → PDF qo‘shilgach yuboriladi', async () => {
      setDiaryPdfRequired(true);
      // Yuborilgan multipart tanasi (nusxasi) o'qiladi; handler javob qaytarmaydi — so'rov standart
      // diary mock'iga o'tadi (u ham `request.formData()` bilan PDF talabini tekshiradi).
      let sent: FormDataEntryValue[] = [];
      server.use(
        http.post(STUDENT_ENDPOINTS.diary, async ({ request }) => {
          sent = (await request.clone().formData()).getAll('files');
        }),
      );
      renderApp('/kundalik');
      await fillText();
      await screen.findByText('PDF hisobot majburiy');

      attach(pdf());
      expect(screen.getByText('PDF biriktirilgan')).toBeInTheDocument();
      fireEvent.click(screen.getByRole('button', { name: 'Yuborish' }));

      expect(await screen.findByText('Kundalik yuborildi.')).toBeInTheDocument();
      expect(sent).toHaveLength(1);
      const [file] = sent;
      expect(file).toBeInstanceOf(File);
      expect(file).toMatchObject({ name: 'hisobot.pdf', type: 'application/pdf', size: 32 });
      expect(screen.getByRole('link', { name: 'hisobot.pdf' })).toHaveAttribute(
        'href',
        '/files/hisobot.pdf',
      );
      expect(screen.getByText('Yozuvlarim · 5')).toBeInTheDocument();
      expect(screen.queryByText(PDF_MSG)).not.toBeInTheDocument();
    });

    it("false → eslatma yo'q, PDF'siz yuboriladi", async () => {
      expect(mockToday.diary.pdfRequired).toBe(false);
      renderApp('/kundalik');
      await fillText();
      fireEvent.click(screen.getByRole('button', { name: 'Yuborish' }));

      expect(await screen.findByText('Kundalik yuborildi.')).toBeInTheDocument();
      expect(screen.queryByText('PDF hisobot majburiy')).not.toBeInTheDocument();
      expect(screen.queryByText(PDF_MSG)).not.toBeInTheDocument();
    });

    it('qayta yozish: yozuvda avvaldan PDF bor → talab bajarilgan', async () => {
      setDiaryPdfRequired(true);
      const rewrite: DiaryEntryDto = {
        ...mockDiary[0]!,
        id: 'd-today',
        date: mockToday.date,
        status: 'rewrite',
        files: [{ id: 'f-9', name: 'Eski_Hisobot.PDF', url: '/files/eski.pdf' }],
      };
      server.use(
        http.get(STUDENT_ENDPOINTS.diary, () => HttpResponse.json([rewrite, ...mockDiary])),
        http.post(STUDENT_ENDPOINTS.diary, () =>
          HttpResponse.json({ ...rewrite, status: 'submitted', text: LONG_TEXT }, { status: 201 }),
        ),
      );
      renderApp('/kundalik');
      await screen.findByText('Yozuvlarim · 5');
      fireEvent.change(screen.getByPlaceholderText('Bugun bajarilgan ishlar — kamida 150 belgi'), {
        target: { value: LONG_TEXT },
      });
      expect(await screen.findByText('PDF biriktirilgan')).toBeInTheDocument();
      fireEvent.click(screen.getByRole('button', { name: 'Yuborish' }));

      expect(await screen.findByText('Kundalik yuborildi.')).toBeInTheDocument();
      // Qayta yozilgan yozuv dublikat bo'lmaydi.
      expect(screen.getByText('Yozuvlarim · 5')).toBeInTheDocument();
    });

    it('server 400 (errors.Files) matni forma ostida ko‘rinadi', async () => {
      server.use(
        http.post(STUDENT_ENDPOINTS.diary, () =>
          problem(400, "Ma'lumotlar noto'g'ri", PDF_MSG, { errors: { Files: [PDF_MSG] } }),
        ),
      );
      renderApp('/kundalik');
      await fillText();
      fireEvent.click(screen.getByRole('button', { name: 'Yuborish' }));

      const form = screen.getByRole('form', { name: 'Yangi yozuv' });
      expect(await within(form).findByRole('alert')).toHaveTextContent(PDF_MSG);
      expect(screen.queryByText('Kundalik yuborildi.')).not.toBeInTheDocument();
    });

    it("server 400 (faqat detail) matni forma ostida ko'rinadi", async () => {
      server.use(http.post(STUDENT_ENDPOINTS.diary, () => problem(400, "Noto'g'ri amal", PDF_MSG)));
      renderApp('/kundalik');
      await fillText();
      fireEvent.click(screen.getByRole('button', { name: 'Yuborish' }));

      expect(await screen.findByRole('alert')).toHaveTextContent(PDF_MSG);
    });
  });
});
