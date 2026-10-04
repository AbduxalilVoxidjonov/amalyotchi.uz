import { screen, within } from '@testing-library/react';
import { renderTutorRoute } from '../test-utils';

describe('MapPage (/tutor/map)', () => {
  it("placeholder va nuqtalar ro'yxati mock bilan ko'rinadi", async () => {
    renderTutorRoute('/tutor/map');
    const list = await screen.findByRole('region', { name: 'Bugungi nuqtalar · 12.10' });
    expect(within(list).getAllByRole('listitem')).toHaveLength(5);
    expect(within(list).getByText('45 m / 150 m')).toBeInTheDocument();
    expect(within(list).getByText('3,4 km — rad etildi')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Xarita maydoni' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Xarita');
  });
});
