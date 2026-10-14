import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http } from 'msw';
import { afterEach, describe, expect, it } from 'vitest';
import { buildDetail } from '@/features/admin/student-profile/mocks';
import { mockProfilePeriods } from '@/features/admin/student-profile/periodTestUtils';
import type { StudentProfileDetail } from '@/features/admin/student-profile/types';
import { server } from '@/mocks/server';
import { fmtDateOnly, todayInTashkent } from '../shared/fmt';
import { problemResponse } from '../shared/mockProblem';
import { renderHierarchyPage } from '../shared/renderHierarchyPage';
import { STUDENTS_ENDPOINT, studentWorkHoursEndpoint } from './api';
import { StudentDetailPage } from './StudentDetailPage';
import { addDays, validateWorkHours } from './workHours';

function renderProfile(studentId: string) {
  return renderHierarchyPage(<StudentDetailPage />, '/admin/students/:studentId', [
    `/admin/students/${studentId}`,
  ]);
}

const ADMIN_EXTRA = {
  groupId: 'g1',
  department: 'Dasturiy injiniring kafedrasi',
  adminStatus: 'active',
  telegramLinked: true,
};

function baseDetail(): StudentProfileDetail {
  const base = buildDetail('s-341030');
  if (!base || base === 'periodNotFound') throw new Error('mock profil topilmadi');
  return base;
}

const tomorrow = () => fmtDateOnly(addDays(todayInTashkent(), 1));
const card = async () => within(await screen.findByRole('region', { name: 'Ish vaqti' }));

/** `PUT /students/{id}/work-hours` so'rov tanalarini yozib boradi. */
function recordPuts() {
  const bodies: unknown[] = [];
  server.events.on('request:start', async ({ request }) => {
    if (request.method === 'PUT' && request.url.endsWith('/work-hours')) {
      bodies.push(await request.clone().json());
    }
  });
  return bodies;
}

afterEach(() => server.events.removeAllListeners());

describe('validateWorkHours', () => {
  it('server qoidalari: ikkalasi shart, ketish > kelish, kamida 1 soat', () => {
    expect(validateWorkHours('09:00', '18:00')).toEqual({});
    expect(validateWorkHours('', '18:00')).toEqual({ start: 'Kelish vaqtini kiriting.' });
    expect(validateWorkHours('09:00', '')).toEqual({ end: 'Ketish vaqtini kiriting.' });
    expect(validateWorkHours('18:00', '09:00').end).toBe(
      "Ketish vaqti kelish vaqtidan keyin bo'lishi kerak.",
    );
    expect(validateWorkHours('09:00', '09:45').end).toBe("Ish vaqti kamida 1 soat bo'lishi kerak.");
  });
});

describe('Admin talaba profili · Ish vaqti (v3.32)', () => {
  it("karta: bugungi vaqt, manba 'Davr vaqti', davr vaqti va 'O'zgartirish' tugmasi", async () => {
    renderProfile('s1');
    const c = await card();
    const region = await screen.findByRole('region', { name: 'Ish vaqti' });
    expect(region).toHaveTextContent('Bugun09:00–18:00');
    expect(region).toHaveTextContent('ManbaDavr vaqti');
    expect(region).toHaveTextContent('Davr vaqti09:00–18:00');
    expect(c.getByRole('button', { name: "O'zgartirish" })).toBeInTheDocument();
    expect(c.queryByRole('note')).not.toBeInTheDocument();
  });

  it("saqlash → PUT { start, end }, xabar 'ertadan kuchga kiradi', kartada kutilayotgan o'zgarish", async () => {
    const user = userEvent.setup();
    const puts = recordPuts();
    renderProfile('s1');
    await user.click((await card()).getByRole('button', { name: "O'zgartirish" }));
    const dialog = await screen.findByRole('dialog', { name: 'Ish vaqtini belgilash' });
    expect(within(dialog).getByLabelText('Kelish')).toHaveValue('09:00');
    expect(within(dialog).getByLabelText('Ketish')).toHaveValue('18:00');
    expect(
      within(dialog).queryByRole('button', { name: 'Davr vaqtiga qaytarish' }),
    ).not.toBeInTheDocument();

    await user.clear(within(dialog).getByLabelText('Kelish'));
    await user.type(within(dialog).getByLabelText('Kelish'), '13:00');
    await user.clear(within(dialog).getByLabelText('Ketish'));
    await user.type(within(dialog).getByLabelText('Ketish'), '19:00');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    expect(await screen.findByRole('status')).toHaveTextContent(
      `Ish vaqti saqlandi. O'zgarish ertadan (${tomorrow()}) kuchga kiradi.`,
    );
    expect(puts).toEqual([{ start: '13:00', end: '19:00' }]);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect((await card()).getByRole('note')).toHaveTextContent(
      `${tomorrow()} dan yangi vaqt: 13:00–19:00.`,
    );
  });

  it("klient validatsiyasi: ketish ≤ kelish → so'rov yuborilmaydi", async () => {
    const user = userEvent.setup();
    const puts = recordPuts();
    renderProfile('s1');
    await user.click((await card()).getByRole('button', { name: "O'zgartirish" }));
    const dialog = await screen.findByRole('dialog', { name: 'Ish vaqtini belgilash' });

    await user.clear(within(dialog).getByLabelText('Kelish'));
    await user.type(within(dialog).getByLabelText('Kelish'), '18:00');
    await user.clear(within(dialog).getByLabelText('Ketish'));
    await user.type(within(dialog).getByLabelText('Ketish'), '09:00');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    expect(
      within(dialog).getByText("Ketish vaqti kelish vaqtidan keyin bo'lishi kerak."),
    ).toBeInTheDocument();
    expect(within(dialog).getByLabelText('Ketish')).toHaveAttribute('aria-invalid', 'true');
    expect(puts).toHaveLength(0);
  });

  it('server 400 errors.Start → maydon ostida, modal ochiq qoladi', async () => {
    const user = userEvent.setup();
    server.use(
      http.put(`${STUDENTS_ENDPOINT}/:id/work-hours`, () =>
        problemResponse(400, "Ma'lumotlar noto'g'ri", "Ish vaqti noto'g'ri.", {
          errors: { Start: ['Vaqtni HH:mm formatida kiriting.'] },
        }),
      ),
    );
    renderProfile('s1');
    await user.click((await card()).getByRole('button', { name: "O'zgartirish" }));
    const dialog = await screen.findByRole('dialog', { name: 'Ish vaqtini belgilash' });
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));

    expect(await within(dialog).findByText('Vaqtni HH:mm formatida kiriting.')).toBeInTheDocument();
    expect(within(dialog).getByLabelText('Kelish')).toHaveAttribute('aria-invalid', 'true');
  });

  it("admin belgilagan vaqt → manba 'Admin belgilagan'; davr vaqtiga qaytarish → PUT { null, null }", async () => {
    const user = userEvent.setup();
    const puts = recordPuts();
    renderProfile('s1');
    // Avval belgilaymiz (mock holati), so'ng qaytaramiz.
    await user.click((await card()).getByRole('button', { name: "O'zgartirish" }));
    let dialog = await screen.findByRole('dialog', { name: 'Ish vaqtini belgilash' });
    await user.clear(within(dialog).getByLabelText('Kelish'));
    await user.type(within(dialog).getByLabelText('Kelish'), '08:00');
    await user.click(within(dialog).getByRole('button', { name: 'Saqlash' }));
    await screen.findByRole('status');

    await user.click((await card()).getByRole('button', { name: "O'zgartirish" }));
    dialog = await screen.findByRole('dialog', { name: 'Ish vaqtini belgilash' });
    expect(within(dialog).getByLabelText('Kelish')).toHaveValue('08:00');
    await user.click(within(dialog).getByRole('button', { name: 'Davr vaqtiga qaytarish' }));

    await waitFor(() =>
      expect(screen.getByRole('status')).toHaveTextContent(
        `Davr vaqtiga qaytarildi. O'zgarish ertadan (${tomorrow()}) kuchga kiradi.`,
      ),
    );
    expect(puts).toEqual([
      { start: '08:00', end: '18:00' },
      { start: null, end: null },
    ]);
    expect((await card()).getByRole('note')).toHaveTextContent(
      `${tomorrow()} dan davr vaqtiga qaytadi (09:00–18:00).`,
    );
  });

  it("bugun admin belgilagan vaqt amalda → 'Admin belgilagan' belgisi", async () => {
    mockProfilePeriods(baseDetail(), {
      ...ADMIN_EXTRA,
      workHours: {
        start: '10:00',
        end: '19:00',
        effectiveFrom: '2020-01-01',
        todayStart: '10:00',
        todayEnd: '19:00',
        periodStart: '09:00',
        periodEnd: '18:00',
      },
    });
    renderProfile('s1');
    const region = await screen.findByRole('region', { name: 'Ish vaqti' });
    expect(region).toHaveTextContent('Bugun10:00–19:00');
    expect(region).toHaveTextContent('ManbaAdmin belgilagan');
    expect(within(region).queryByRole('note')).not.toBeInTheDocument();
  });

  it("eski server (workHours yo'q) → karta ko'rsatilmaydi", async () => {
    mockProfilePeriods(baseDetail(), ADMIN_EXTRA);
    renderProfile('s1');
    await screen.findByRole('region', { name: "Tashkiliy ma'lumot" });
    expect(screen.queryByRole('region', { name: 'Ish vaqti' })).not.toBeInTheDocument();
  });

  it("endpoint yo'li", () => {
    expect(studentWorkHoursEndpoint('s 1')).toBe('/api/admin/students/s%201/work-hours');
  });
});
