import { fireEvent, screen, waitFor } from '@testing-library/react';
import { renderApp } from '@/test/render-app';

describe('CalendarPage (kalendarim)', () => {
  it('oy gridi, kataklar va legend; oy almashtirish ?month= ga yoziladi', async () => {
    const router = renderApp('/kalendar');
    expect(await screen.findByRole('heading', { name: 'Oktabr 2026' })).toBeInTheDocument();
    expect(screen.getByText('Aliyev Akmal · 412-22')).toBeInTheDocument();

    expect(screen.getByLabelText('1 · Keldi')).toHaveTextContent('K');
    expect(screen.getByLabelText('4 · Dam olish')).toBeInTheDocument(); // yakshanba
    expect(screen.getByLabelText('5 · Kech keldi')).toHaveTextContent('k'); // dushanba
    expect(screen.getByLabelText('9 · Kelmadi')).toHaveTextContent('×');
    expect(screen.getByLabelText('12 · Sababli')).toHaveTextContent('S');
    expect(screen.getByLabelText('13 · Kutilmoqda')).toHaveTextContent('…'); // bugun
    expect(screen.getByLabelText('14 · Kelgusi kun')).toBeInTheDocument();

    const legend = screen.getByRole('list', { name: 'Belgilar' });
    for (const label of [
      'Keldi',
      'Kech keldi',
      'Kelmadi',
      'Sababli',
      'Dam olish',
      'Kutilmoqda',
      'Kelgusi kun',
    ]) {
      expect(legend).toHaveTextContent(label);
    }

    fireEvent.click(screen.getByRole('button', { name: 'Noyabr ›' }));
    await waitFor(() => expect(router.state.location.search).toBe('?month=2026-11'));
    expect(await screen.findByRole('heading', { name: 'Noyabr 2026' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: '‹ Oktabr' }));
    expect(await screen.findByRole('heading', { name: 'Oktabr 2026' })).toBeInTheDocument();
  });
});
