import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http } from 'msw';
import {
  detailWithPeriods,
  ENDED_OPTIONS,
  ENDED_PERIOD,
  mockProfilePeriods,
} from '@/features/tutor/students/periodTestUtils';
import { server } from '@/mocks/server';
import { problemResponse } from '../shared/mockProblem';
import { renderHierarchyPage } from '../shared/renderHierarchyPage';
import { renderWithProviders } from '../shared/renderWithProviders';
import { studentCompanyEndpoint, STUDENTS_ENDPOINT } from './api';
import { AssignCompanyModal } from './components/AssignCompanyModal';
import { TRANSFER_WARNING } from './components/StudentCompanyModal';
import { resetStudentCompanyMock } from './mocks';
import { StudentDetailPage } from './StudentDetailPage';

afterEach(() => resetStudentCompanyMock());

function renderProfile(studentId: string, search = '') {
  return renderHierarchyPage(<StudentDetailPage />, '/admin/students/:studentId', [
    `/admin/students/${studentId}${search}`,
  ]);
}

const ADMIN_EXTRA = {
  groupId: 'g1',
  department: 'Dasturiy injiniring kafedrasi',
  adminStatus: 'active',
  telegramLinked: true,
  tutor: null,
};

const COMPANY_BUTTONS = /^(Korxonaga biriktirish|Boshqa korxonaga o'tkazish)$/;

/** `POST /students/{id}/company` so'rov tanalarini yozib boradi. */
function recordCompanyPosts() {
  const bodies: unknown[] = [];
  const listener = ({ request }: { request: Request }) => {
    if (request.method === 'POST' && new URL(request.url).pathname.endsWith('/company')) {
      void request
        .clone()
        .json()
        .then((b: unknown) => bodies.push(b));
    }
  };
  server.events.on('request:start', listener);
  return { bodies, stop: () => server.events.removeListener('request:start', listener) };
}

const metaCard = () => within(screen.getByRole('region', { name: "Tashkiliy ma'lumot" }));

describe("Admin talaba profili — korxonaga biriktirish / o'tkazish", () => {
  it('biriktirilgan talabada tugma — "Boshqa korxonaga o\'tkazish"', async () => {
    renderProfile('s1');
    expect(await screen.findByRole('heading', { name: 'Aliyev Akmal' })).toBeInTheDocument();
    expect(metaCard().getByText('Tech Solutions MChJ')).toBeInTheDocument();
    expect(
      metaCard().getByRole('button', { name: "Boshqa korxonaga o'tkazish" }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Korxonaga biriktirish' })).not.toBeInTheDocument();
  });

  it('korxonasiz talabada tugma — "Korxonaga biriktirish"', async () => {
    renderProfile('s5');
    expect(await screen.findByRole('heading', { name: 'Oripov Javohir' })).toBeInTheDocument();
    expect(metaCard().getByRole('button', { name: 'Korxonaga biriktirish' })).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: "Boshqa korxonaga o'tkazish" }),
    ).not.toBeInTheDocument();
  });

  it("rejadagi davr ko'rinishida (?period=) tugma yashiriladi", async () => {
    renderProfile('s1', '?period=per-2027-bahor');
    expect(await screen.findByRole('heading', { name: 'Aliyev Akmal' })).toBeInTheDocument();
    await within(screen.getByRole('region', { name: 'Korxona' })).findByText(
      'Bu davr 01.02.2027 dan boshlanadi',
    );
    expect(metaCard().queryByRole('button', { name: COMPANY_BUTTONS })).not.toBeInTheDocument();
  });

  it("tugagan davr (sukut bo'lsa ham) ko'rinishida tugma yashiriladi", async () => {
    mockProfilePeriods('admin', detailWithPeriods(ENDED_OPTIONS, ENDED_PERIOD), ADMIN_EXTRA);
    renderProfile('s1');
    expect(await screen.findByRole('tab', { name: /Bahorgi amaliyot 2026/ })).toBeInTheDocument();
    expect(metaCard().queryByRole('button', { name: COMPANY_BUTTONS })).not.toBeInTheDocument();
  });

  it("o'tkazish: joriy korxona tanlanmaydi, POST tanasi, modal yopiladi va profil yangilanadi", async () => {
    const user = userEvent.setup();
    const rec = recordCompanyPosts();
    renderProfile('s1');

    await user.click(await screen.findByRole('button', { name: "Boshqa korxonaga o'tkazish" }));
    const dialog = within(
      await screen.findByRole('dialog', { name: "Boshqa korxonaga o'tkazish" }),
    );
    expect(dialog.getByText(TRANSFER_WARNING)).toBeInTheDocument();

    const current = await dialog.findByRole('radio', { name: 'Tech Solutions MChJ' });
    expect(current).toBeDisabled();
    expect(dialog.getByText(/joriy korxona$/)).toBeInTheDocument();
    expect(dialog.getByRole('radio', { name: "Ipak Yo'li Logistika" })).toBeDisabled();

    const submit = dialog.getByRole('button', { name: "O'tkazish" });
    expect(submit).toBeDisabled();
    await user.click(dialog.getByRole('radio', { name: 'Agrobank ATB' }));
    await user.type(dialog.getByLabelText('Izoh (ixtiyoriy)'), '  Korxona almashtirildi  ');
    await user.click(submit);

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(rec.bodies).toEqual([{ companyId: 'c2', comment: 'Korxona almashtirildi' }]);
    rec.stop();

    expect(screen.getByText("Talaba «Agrobank ATB» korxonasiga o'tkazildi.")).toBeInTheDocument();
    expect(metaCard().getByText('Agrobank ATB')).toBeInTheDocument();
    expect(
      within(screen.getByRole('region', { name: 'Korxona' })).getAllByText('Agrobank ATB').length,
    ).toBeGreaterThan(0);
    expect(
      metaCard().getByRole('button', { name: "Boshqa korxonaga o'tkazish" }),
    ).toBeInTheDocument();
  });

  it("birinchi biriktirish: ogohlantirish yo'q, izohsiz POST, tugma matni o'zgaradi", async () => {
    const user = userEvent.setup();
    const rec = recordCompanyPosts();
    renderProfile('s5');

    await user.click(await screen.findByRole('button', { name: 'Korxonaga biriktirish' }));
    const dialog = within(await screen.findByRole('dialog', { name: 'Korxonaga biriktirish' }));
    expect(dialog.queryByText(TRANSFER_WARNING)).not.toBeInTheDocument();

    await user.click(await dialog.findByRole('radio', { name: 'Tech Solutions MChJ' }));
    await user.click(dialog.getByRole('button', { name: 'Biriktirish' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(rec.bodies).toEqual([{ companyId: 'c1' }]);
    rec.stop();
    expect(
      screen.getByText('Talaba «Tech Solutions MChJ» korxonasiga biriktirildi.'),
    ).toBeInTheDocument();
    expect(
      metaCard().getByRole('button', { name: "Boshqa korxonaga o'tkazish" }),
    ).toBeInTheDocument();
  });

  it("409 xatosi modal ichida ko'rinadi, modal ochiq qoladi", async () => {
    const user = userEvent.setup();
    server.use(
      http.post(`${STUDENTS_ENDPOINT}/:id/company`, () =>
        problemResponse(409, 'Amal bajarilmadi', "Talaba guruhida ochiq amaliyot davri yo'q."),
      ),
    );
    renderProfile('s1');

    await user.click(await screen.findByRole('button', { name: "Boshqa korxonaga o'tkazish" }));
    const dialogEl = await screen.findByRole('dialog', { name: "Boshqa korxonaga o'tkazish" });
    const dialog = within(dialogEl);
    await user.click(await dialog.findByRole('radio', { name: 'Agrobank ATB' }));
    await user.click(dialog.getByRole('button', { name: "O'tkazish" }));

    const alert = await dialog.findByRole('alert');
    expect(alert).toHaveTextContent("O'tkazib bo'lmadi");
    expect(alert).toHaveTextContent("Talaba guruhida ochiq amaliyot davri yo'q.");
    expect(dialogEl).toBeInTheDocument();
    expect(metaCard().getByText('Tech Solutions MChJ')).toBeInTheDocument();
  });

  it('mock: shu korxonaga qayta biriktirish → 409, CompanyId maydon xatosi → 400', async () => {
    const same = await fetch(new URL(studentCompanyEndpoint('s1'), window.location.origin), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ companyId: 'c1' }),
    });
    expect(same.status).toBe(409);
    expect(((await same.json()) as { detail: string }).detail).toBe(
      'Talaba allaqachon shu korxonaga biriktirilgan.',
    );

    const empty = await fetch(new URL(studentCompanyEndpoint('s1'), window.location.origin), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ companyId: '' }),
    });
    expect(empty.status).toBe(400);
    expect(((await empty.json()) as { errors: Record<string, string[]> }).errors).toHaveProperty(
      'CompanyId',
    );
  });
});

describe('Ommaviy biriktirish modali (regressiya — umumiy CompanyPicker)', () => {
  it("faol bo'lmagan korxona tanlanmaydi, joriy belgisi yo'q, hisobot chiqadi", async () => {
    const user = userEvent.setup();
    const onAssigned = vi.fn();
    renderWithProviders(
      <AssignCompanyModal studentIds={['s1', 's2']} onClose={() => {}} onAssigned={onAssigned} />,
    );

    const dialog = within(await screen.findByRole('dialog', { name: 'Korxonaga biriktirish' }));
    expect(await dialog.findByRole('radio', { name: "Ipak Yo'li Logistika" })).toBeDisabled();
    expect(dialog.getByRole('radio', { name: 'Tech Solutions MChJ' })).toBeEnabled();
    expect(dialog.queryByText(/joriy korxona/)).not.toBeInTheDocument();
    expect(dialog.getByText('Tanlangan: 2 ta talaba')).toBeInTheDocument();

    await user.click(dialog.getByRole('radio', { name: 'Tech Solutions MChJ' }));
    await user.click(dialog.getByRole('button', { name: 'Biriktirish' }));

    expect(await dialog.findByRole('region', { name: 'Biriktirish natijasi' })).toBeInTheDocument();
    expect(onAssigned).toHaveBeenCalledTimes(1);
  });
});
