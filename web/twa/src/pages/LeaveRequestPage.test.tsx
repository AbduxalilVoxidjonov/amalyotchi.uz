import { fireEvent, screen, within } from '@testing-library/react';
import { renderApp } from '@/test/render-app';

describe('LeaveRequestPage (isRuxsatForm)', () => {
  it("ro'yxat va forma", async () => {
    renderApp('/ruxsat');
    expect(await screen.findByText("Yangi ruxsat so'rovi")).toBeInTheDocument();
    const list = await screen.findByRole('region', { name: "Mening so'rovlarim" });
    expect(within(list).getByText('Kasallik — poliklinika spravkasi')).toBeInTheDocument();
    expect(within(list).getByText('Tasdiqlangan')).toBeInTheDocument();
    expect(within(list).getByText('Rad etilgan')).toBeInTheDocument();
    expect(within(list).getByText('spravka.pdf')).toBeInTheDocument();
    expect(within(list).getByRole('link', { name: 'xat.pdf' })).toHaveAttribute(
      'href',
      '/files/xat.pdf',
    );
    expect(within(list).getByText('Tyutor: Sabab yetarli emas')).toBeInTheDocument();
  });

  it("validatsiya → submit → yangi so'rov 'Kutilmoqda' bilan ro'yxat boshida", async () => {
    renderApp('/ruxsat');
    await screen.findByText("Yangi ruxsat so'rovi");

    fireEvent.click(screen.getByRole('button', { name: 'Tyutorga yuborish' }));
    expect(await screen.findByText('Boshlanish sanasi majburiy.')).toBeInTheDocument();
    expect(screen.getByText('Tugash sanasi majburiy.')).toBeInTheDocument();
    expect(screen.getByText('Sababni kamida 10 belgi bilan yozing.')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Boshlanish sanasi'), {
      target: { value: '20.10.2026' },
    });
    fireEvent.change(screen.getByLabelText('Tugash sanasi'), { target: { value: '19.10.2026' } });
    fireEvent.change(screen.getByLabelText('Sabab'), {
      target: { value: 'Oilaviy sabab — tumanga safar' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Tyutorga yuborish' }));
    expect(
      await screen.findByText('Tugash sanasi boshlanishdan oldin bo‘lishi mumkin emas.'),
    ).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Tugash sanasi'), { target: { value: '21.10.2026' } });
    fireEvent.click(screen.getByRole('button', { name: 'Tyutorga yuborish' }));

    expect(await screen.findByText("So'rov tyutorga yuborildi.")).toBeInTheDocument();
    const list = screen.getByRole('region', { name: "Mening so'rovlarim" });
    const rows = within(list).getAllByRole('listitem');
    expect(rows).toHaveLength(4);
    expect(rows[0]).toHaveTextContent('20.10.2026 – 21.10.2026');
    expect(rows[0]).toHaveTextContent('Oilaviy sabab — tumanga safar');
    expect(rows[0]).toHaveTextContent('Kutilmoqda');
    expect(screen.getByLabelText('Boshlanish sanasi')).toHaveValue('');
  });

  it("kesishuvchi sanalar → 409 'allaqachon bor'", async () => {
    renderApp('/ruxsat');
    await screen.findByText("Yangi ruxsat so'rovi");
    fireEvent.change(screen.getByLabelText('Boshlanish sanasi'), {
      target: { value: '14.10.2026' },
    });
    fireEvent.change(screen.getByLabelText('Tugash sanasi'), { target: { value: '14.10.2026' } });
    fireEvent.change(screen.getByLabelText('Sabab'), {
      target: { value: 'Kasallik — qayta yuborish' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Tyutorga yuborish' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      "Bu sanalar uchun ruxsat so'rovi allaqachon bor.",
    );
  });
});
