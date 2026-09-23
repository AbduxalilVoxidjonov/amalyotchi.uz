import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { DataTable, type DataTableColumn } from '@/shared/ui';

interface Row {
  id: string;
  name: string;
}

const rows: Row[] = [
  { id: '1', name: 'Akmal' },
  { id: '2', name: 'Dilnoza' },
];

function renderTable(
  opts: { rowHref?: (r: Row) => string | undefined; onButton?: () => void } = {},
) {
  const columns: DataTableColumn<Row>[] = [
    { key: 'name', header: 'Ism' },
    {
      key: 'extra',
      header: 'Qo‘shimcha',
      render: (r) => (
        <>
          <input type="checkbox" aria-label={`Tanlash ${r.name}`} />
          <span data-row-click-ignore>Ignor {r.name}</span>
        </>
      ),
    },
  ];
  return render(
    <MemoryRouter initialEntries={['/list']}>
      <Routes>
        <Route
          path="/list"
          element={
            <DataTable
              columns={columns}
              rows={rows}
              rowKey={(r) => r.id}
              rowHref={opts.rowHref ?? ((r) => `/items/${r.id}`)}
              actions={(r) => (
                <button type="button" onClick={opts.onButton}>
                  Tahrirlash {r.name}
                </button>
              )}
            />
          }
        />
        <Route path="/items/:id" element={<p>Ichki sahifa</p>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('DataTable rowHref', () => {
  it('oddiy katak bosilsa href ga o‘tadi', async () => {
    renderTable();
    const row = screen.getByText('Dilnoza').closest('[role="row"]')!;
    expect(row).toHaveAttribute('data-row-link', 'true');
    // a11y: qator/katak fokus olmaydi
    expect(row.querySelector('[tabindex]')).toBeNull();
    await userEvent.click(screen.getByText('Dilnoza'));
    expect(screen.getByText('Ichki sahifa')).toBeInTheDocument();
  });

  it('button, checkbox va [data-row-click-ignore] navigatsiya qilmaydi; button onClick ishlaydi', async () => {
    const onButton = vi.fn();
    renderTable({ onButton });
    await userEvent.click(screen.getByRole('button', { name: 'Tahrirlash Akmal' }));
    expect(onButton).toHaveBeenCalledTimes(1);
    const checkbox = screen.getByRole('checkbox', { name: 'Tanlash Akmal' });
    await userEvent.click(checkbox);
    expect(checkbox).toBeChecked();
    await userEvent.click(screen.getByText('Ignor Akmal'));
    expect(screen.queryByText('Ichki sahifa')).not.toBeInTheDocument();
    expect(screen.getByText('Akmal')).toBeInTheDocument();
  });

  it('rowHref undefined qaytarsa navigatsiya yo‘q', async () => {
    renderTable({ rowHref: (r) => (r.id === '1' ? undefined : `/items/${r.id}`) });
    const row = screen.getByText('Akmal').closest('[role="row"]')!;
    expect(row).not.toHaveAttribute('data-row-link');
    await userEvent.click(screen.getByText('Akmal'));
    expect(screen.queryByText('Ichki sahifa')).not.toBeInTheDocument();
  });

  it('ctrl/meta bilan yoki o‘rta tugma — window.open (yangi tab)', () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    renderTable();
    fireEvent.click(screen.getByText('Akmal'), { ctrlKey: true });
    fireEvent.click(screen.getByText('Dilnoza'), { metaKey: true });
    fireEvent(screen.getByText('Akmal'), new MouseEvent('auxclick', { bubbles: true, button: 1 }));
    expect(open).toHaveBeenNthCalledWith(1, '/items/1', '_blank', 'noopener');
    expect(open).toHaveBeenNthCalledWith(2, '/items/2', '_blank', 'noopener');
    expect(open).toHaveBeenNthCalledWith(3, '/items/1', '_blank', 'noopener');
    expect(screen.queryByText('Ichki sahifa')).not.toBeInTheDocument();
    open.mockRestore();
  });
});
