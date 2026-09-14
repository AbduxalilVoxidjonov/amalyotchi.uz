import { fireEvent, screen } from '@testing-library/react';
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
});
