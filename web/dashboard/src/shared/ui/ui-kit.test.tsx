import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import {
  Badge,
  Breadcrumb,
  Button,
  ConfirmDialog,
  DataTable,
  Input,
  Modal,
  pctKind,
  ProgressBar,
  SidebarNav,
  type DataTableColumn,
} from '@/shared/ui';

describe('Button', () => {
  it('click ishlaydi, default type=button, data-variant/size', async () => {
    const onClick = vi.fn();
    render(
      <Button variant="primary" size="sm" onClick={onClick}>
        Saqlash
      </Button>,
    );
    const btn = screen.getByRole('button', { name: 'Saqlash' });
    expect(btn).toHaveAttribute('type', 'button');
    expect(btn).toHaveAttribute('data-variant', 'primary');
    expect(btn).toHaveAttribute('data-size', 'sm');
    await userEvent.click(btn);
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('asChild — bolaga class va data-atributlar o‘tadi', () => {
    render(
      <MemoryRouter>
        <Button asChild variant="secondary">
          <a href="/x">Havola</a>
        </Button>
      </MemoryRouter>,
    );
    const a = screen.getByRole('link', { name: 'Havola' });
    expect(a).toHaveAttribute('data-variant', 'secondary');
    expect(a.className).not.toBe('');
  });
});

describe('Badge', () => {
  it.each(['ok', 'late', 'bad', 'neu', 'info'] as const)('status=%s', (status) => {
    render(<Badge status={status}>x</Badge>);
    expect(screen.getByText('x')).toHaveAttribute('data-status', status);
  });
});

describe('ProgressBar', () => {
  it('rang qoidasi va aria', () => {
    render(<ProgressBar value={72} label="Davomat" />);
    const bar = screen.getByRole('progressbar', { name: 'Davomat' });
    expect(bar).toHaveAttribute('aria-valuenow', '72');
    expect(bar).toHaveAttribute('data-kind', 'late');
    expect(screen.getByText('72%')).toBeInTheDocument();
    expect(pctKind(85)).toBe('ok');
    expect(pctKind(69)).toBe('bad');
  });
});

describe('Input', () => {
  it('label htmlFor, error → role=alert + aria-invalid', () => {
    render(<Input label="Parol" error="Xato" />);
    const input = screen.getByLabelText('Parol');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('alert')).toHaveTextContent('Xato');
    expect(input).toHaveAttribute('aria-describedby', screen.getByRole('alert').id);
  });
});

interface Row {
  id: string;
  name: string;
  pct: number;
}
const columns: DataTableColumn<Row>[] = [
  { key: 'name', header: 'Ism', width: '2fr' },
  { key: 'pct', header: 'Foiz', width: '80px', mono: true, render: (r) => `${r.pct}%` },
];

describe('DataTable', () => {
  it('qatorlar, ustun sarlavhalari, actions, density', () => {
    const rows: Row[] = [
      { id: '1', name: 'Akmal', pct: 90 },
      { id: '2', name: 'Dilnoza', pct: 70 },
    ];
    render(
      <DataTable
        aria-label="Talabalar"
        columns={columns}
        rows={rows}
        rowKey={(r) => r.id}
        density="compact"
        actions={(r) => <button type="button">Ko'rish {r.name}</button>}
      />,
    );
    const table = screen.getByRole('table', { name: 'Talabalar' });
    expect(table).toHaveAttribute('data-density', 'compact');
    expect(screen.getAllByRole('columnheader')).toHaveLength(3);
    expect(screen.getAllByRole('row')).toHaveLength(3); // head + 2
    expect(screen.getByText('90%')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: "Ko'rish Dilnoza" })).toBeInTheDocument();
  });

  it("ustunlar tekis: actions bor/yo'q qatorlarda katak soni va grid shabloni bir xil", () => {
    const rows: Row[] = [
      { id: '1', name: 'Akmal', pct: 90 },
      { id: '2', name: 'Dilnoza', pct: 70 },
    ];
    render(
      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(r) => r.id}
        actionsWidth="minmax(120px,max-content)"
        actions={(r) => (r.id === '1' ? <button type="button">Tasdiqlash</button> : null)}
      />,
    );
    const [head, ...body] = screen.getAllByRole('row');
    expect(body).toHaveLength(2);
    // Bitta grid: head va barcha qatorlar bitta rowgroup ichida — ustunlar avtomatik tekislanadi.
    const grid = screen.getByRole('rowgroup');
    for (const r of [head!, ...body]) expect(r.parentElement).toBe(grid);
    expect(head!.querySelectorAll('[role="columnheader"]')).toHaveLength(columns.length + 1);
    for (const r of body)
      expect(r.querySelectorAll('[role="cell"]')).toHaveLength(columns.length + 1);
    // Ustun shabloni bitta joyda (jadval darajasida), actions kengligi shu yerda.
    expect(screen.getByRole('table').style.getPropertyValue('--cols')).toBe(
      '2fr 80px minmax(120px,max-content)',
    );
  });

  it("bo'sh holat", () => {
    render(
      <DataTable columns={columns} rows={[]} rowKey={(r) => r.id} emptyText="Hech narsa yo'q" />,
    );
    expect(screen.getByText("Hech narsa yo'q")).toBeInTheDocument();
  });

  it('onRowClick', async () => {
    const onRowClick = vi.fn();
    render(
      <DataTable
        columns={columns}
        rows={[{ id: '1', name: 'Akmal', pct: 1 }]}
        rowKey={(r) => r.id}
        onRowClick={onRowClick}
      />,
    );
    await userEvent.click(screen.getByText('Akmal'));
    expect(onRowClick).toHaveBeenCalledWith(expect.objectContaining({ id: '1' }), 0);
    // Klaviatura: qator `display: contents` — birinchi katak fokuslanadi, Enter → onRowClick.
    const firstCell = screen.getAllByRole('cell')[0]!;
    expect(firstCell).toHaveAttribute('tabindex', '0');
    firstCell.focus();
    await userEvent.keyboard('{Enter}');
    expect(onRowClick).toHaveBeenCalledTimes(2);
  });

  it('rowDim — faqat mos qatorga data-row-dim="true" qo\'yadi (amallar ustuni xira bo\'lmaydi)', () => {
    const rows: Row[] = [
      { id: '1', name: 'Akmal', pct: 90 },
      { id: '2', name: 'Dilnoza', pct: 70 },
    ];
    render(
      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(r) => r.id}
        rowDim={(r) => r.id === '2'}
        actions={() => <button type="button">Amal</button>}
      />,
    );
    const [, ...bodyRows] = screen.getAllByRole('row');
    expect(bodyRows[0]).not.toHaveAttribute('data-row-dim');
    expect(bodyRows[1]).toHaveAttribute('data-row-dim', 'true');
  });
});

describe('Modal', () => {
  it("role=dialog/aria-modal, Esc yopadi, overlay bosilsa yopadi, fokus ichkariga o'tadi", async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(
      <Modal open onClose={onClose} title="Sarlavha" description="Tavsif">
        <input aria-label="Maydon" />
      </Modal>,
    );
    const dialog = screen.getByRole('dialog', { name: 'Sarlavha' });
    expect(dialog).toHaveAttribute('aria-modal', 'true');
    expect(dialog).toHaveAccessibleDescription('Tavsif');
    expect(screen.getByLabelText('Maydon')).toHaveFocus();

    await user.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('overlay (fon) bosilsa yopiladi, panel ichi bosilsa yopilmaydi', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(
      <Modal open onClose={onClose} title="Sarlavha">
        <p>Kontent</p>
      </Modal>,
    );
    await user.click(screen.getByText('Kontent'));
    expect(onClose).not.toHaveBeenCalled();

    // `createPortal` — overlay `document.body`ga to'g'ridan-to'g'ri chiqadi; dialog uning bolasi.
    const dialog = screen.getByRole('dialog');
    const overlay = dialog.parentElement;
    expect(overlay).not.toBeNull();
    // overlay elementiga bevosita bosish (bola emas) — mousedown target === currentTarget.
    await user.click(overlay as Element);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('open=false — hech narsa render qilmaydi', () => {
    render(
      <Modal open={false} onClose={vi.fn()} title="Sarlavha">
        <p>Kontent</p>
      </Modal>,
    );
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});

describe('ConfirmDialog', () => {
  it('tasdiqlash/bekor qilish tugmalari, danger variant, xato banner', async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn();
    const onCancel = vi.fn();
    render(
      <ConfirmDialog
        open
        title="O'chirishni tasdiqlang"
        description="Bu amalni qaytarib bo'lmaydi."
        danger
        error="Xatolik yuz berdi."
        onConfirm={onConfirm}
        onCancel={onCancel}
      />,
    );
    expect(screen.getByRole('alert')).toHaveTextContent('Xatolik yuz berdi.');
    const confirmBtn = screen.getByRole('button', { name: 'Tasdiqlash' });
    expect(confirmBtn).toHaveAttribute('data-variant', 'danger');
    await user.click(confirmBtn);
    expect(onConfirm).toHaveBeenCalledTimes(1);
    await user.click(screen.getByRole('button', { name: 'Bekor qilish' }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });
});

describe('SidebarNav', () => {
  it('aktiv element aria-current="page", badge ko‘rinadi', () => {
    render(
      <MemoryRouter initialEntries={['/tutor/students']}>
        <SidebarNav
          items={[
            { label: 'Bugun', to: '/tutor', end: true, badge: 38 },
            { label: 'Talabalarim', to: '/tutor/students', badge: 12 },
            { label: 'Xarita', to: '/tutor/map' },
          ]}
        />
      </MemoryRouter>,
    );
    expect(screen.getByRole('link', { name: /Talabalarim/ })).toHaveAttribute(
      'aria-current',
      'page',
    );
    expect(screen.getByRole('link', { name: /Bugun/ })).not.toHaveAttribute('aria-current');
    expect(screen.getByText('12')).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Asosiy' })).toBeInTheDocument();
  });
});

describe('Breadcrumb', () => {
  it('nav aria-label="Yo\'l", oraliq elementlar havola, oxirgisi aria-current="page" va havola emas', () => {
    render(
      <MemoryRouter>
        <Breadcrumb
          items={[
            { label: 'Fakultetlar', to: '/admin/faculties' },
            { label: 'Axborot texnologiyalari', to: '/admin/faculties/f1' },
            { label: 'Kompyuter injiniringi kafedrasi' },
          ]}
        />
      </MemoryRouter>,
    );
    const nav = screen.getByRole('navigation', { name: "Yo'l" });
    expect(nav).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Fakultetlar' })).toHaveAttribute(
      'href',
      '/admin/faculties',
    );
    expect(screen.getByRole('link', { name: 'Axborot texnologiyalari' })).toHaveAttribute(
      'href',
      '/admin/faculties/f1',
    );
    const current = screen.getByText('Kompyuter injiniringi kafedrasi');
    expect(current).toHaveAttribute('aria-current', 'page');
    expect(current.tagName).not.toBe('A');
  });
});
