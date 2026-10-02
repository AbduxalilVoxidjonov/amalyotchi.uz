import { http, HttpResponse, type HttpHandler } from 'msw';
import { problemResponse } from '../shared/mockProblem';
import { paginateMock } from '../shared/paginate';
import { MESSAGES_ENDPOINT, RECIPIENT_GROUPS_ENDPOINT, RECIPIENTS_ENDPOINT } from './api';
import {
  MESSAGE_TEXT_MAX,
  type DeliveryStatus,
  type MessageDeliveryRow,
  type MessageRecipientRow,
  type MessageSummary,
  type RecipientFilter,
  type SendMessageInput,
} from './types';

/**
 * Xabarlar MSW mock'lari (faqat `VITE_USE_MOCKS=true` va testlarda — `src/mocks/*` orqali dinamik
 * yuklanadi, prod bundle'ga kirmaydi). Fakultet/yo'nalish id'lari talabalar filtri mock'i
 * (`GET /api/admin/students/filters`) bilan bir xil: f1/f2/f3, dir1/dir2/dir3.
 *
 * Yuborish simulyatsiyasi: yangi xabar `queued`; har GET (ro'yxat, tafsilot, yetkazishlar) uni bir
 * qadam oldinga suradi — `queued → sending`, keyin har qadamda ≤15 ta yetkazish; navbat bo'shasa
 * `completed`. Bot bloklagan talaba → `blocked`; ba'zi talabalar birinchi urinishda `failed` (429),
 * retry'dan keyin yetkaziladi.
 */

interface GroupSeed {
  id: string;
  name: string;
  course: number;
  facultyId: string;
  facultyName: string;
  directionId: string;
  directionName: string;
}

const GROUPS: GroupSeed[] = [
  {
    id: 'g1',
    name: '412-22',
    course: 3,
    facultyId: 'f1',
    facultyName: 'Axborot texnologiyalari',
    directionId: 'dir1',
    directionName: 'Kompyuter injiniringi',
  },
  {
    id: 'g2',
    name: '413-22',
    course: 3,
    facultyId: 'f1',
    facultyName: 'Axborot texnologiyalari',
    directionId: 'dir1',
    directionName: 'Kompyuter injiniringi',
  },
  {
    id: 'g3',
    name: '421-23',
    course: 2,
    facultyId: 'f1',
    facultyName: 'Axborot texnologiyalari',
    directionId: 'dir2',
    directionName: 'Dasturiy injiniring',
  },
  {
    id: 'g4',
    name: '221-23',
    course: 2,
    facultyId: 'f2',
    facultyName: 'Iqtisodiyot va moliya',
    directionId: 'dir3',
    directionName: 'Bank ishi',
  },
  {
    id: 'g5',
    name: '511-21',
    course: 4,
    facultyId: 'f3',
    facultyName: 'Qurilish va arxitektura',
    directionId: 'dir-q1',
    directionName: 'Arxitektura',
  },
];

const MALE = [
  ['Aliyev', 'Akmal'],
  ['Sobirov', 'Diyor'],
  ['Karimov', 'Jasur'],
  ['Rahimov', 'Bekzod'],
  ['Toshmatov', 'Sardor'],
  ['Yusupov', 'Otabek'],
  ['Nazarov', 'Shohruh'],
  ['Qodirov', 'Aziz'],
  ['Ergashev', 'Javohir'],
  ['Mirzayev', 'Islom'],
  ['Hasanov', 'Ulugbek'],
  ['Abdullayev', 'Temur'],
  ['Saidov', 'Doniyor'],
  ['Xolmatov', 'Sherzod'],
  ['Fayzullayev', 'Mansur'],
  ['Ismoilov', 'Behruz'],
  ['Normatov', 'Farrux'],
  ['Turg‘unov', 'Alisher'],
  ['Umarov', 'Ravshan'],
  ['Jo‘rayev', 'Elbek'],
  ['Murodov', 'Nodir'],
  ['Komilov', 'Sanjar'],
] as const;

const FEMALE = [
  ['Karimova', 'Malika'],
  ['Rahimova', 'Dilnoza'],
  ['Yusupova', 'Madina'],
  ['Nazarova', 'Sevara'],
  ['Qodirova', 'Zarina'],
  ['Ergasheva', 'Nilufar'],
  ['Mirzayeva', 'Shahnoza'],
  ['Hasanova', 'Gulnoza'],
  ['Abdullayeva', 'Mohinur'],
  ['Saidova', 'Feruza'],
  ['Xolmatova', 'Laylo'],
  ['Ismoilova', 'Kamola'],
  ['Normatova', 'Dildora'],
  ['Umarova', 'Barno'],
  ['Murodova', 'Sabina'],
  ['Komilova', 'Iroda'],
  ['Tursunova', 'Munisa'],
  ['Valiyeva', 'Charos'],
  ['Sharipova', 'Nigora'],
  ['Olimova', 'Rayhona'],
  ['Hamroyeva', 'Ozoda'],
  ['Bakirova', 'Zilola'],
] as const;

interface RecipientSeed extends MessageRecipientRow {
  facultyId: string | null;
  directionId: string | null;
  groupId: string | null;
}

/** Bot bloklangan talabalar (indeks bo'yicha) — xabar yetmaydi. */
const BLOCKED_INDEXES = new Set([6, 19]);
/** Birinchi urinishda Telegram 429 qaytaradigan talabalar — retry'dan keyin yetkaziladi. */
const FLAKY_INDEXES = new Set([3, 27]);

const LINKED_BASE = Date.parse('2026-09-01T09:00:00Z');

function buildRecipients(): RecipientSeed[] {
  const rows: RecipientSeed[] = [];
  for (let i = 0; i < 44; i += 1) {
    const pool = i % 2 === 0 ? MALE : FEMALE;
    const [last, first] = pool[Math.floor(i / 2) % pool.length]!;
    // Oxirgi talaba — guruhga biriktirilmagan (fakultet/guruh "—").
    const group = i === 43 ? null : GROUPS[i % GROUPS.length]!;
    rows.push({
      userId: `tg-u${i + 1}`,
      fullName: `${last} ${first}`,
      // Bitta talabada HEMIS ID yo'q (qo'lda qo'shilgan hisob).
      hemisId: i === 11 ? null : String(341000 + i * 7),
      telegramUserId: 5_100_000_000 + i * 104_729,
      telegramLinkedAt: i === 15 ? null : new Date(LINKED_BASE + i * 7.5 * 3_600_000).toISOString(),
      botBlocked: BLOCKED_INDEXES.has(i),
      facultyId: group?.facultyId ?? null,
      facultyName: group?.facultyName ?? null,
      directionId: group?.directionId ?? null,
      directionName: group?.directionName ?? null,
      groupId: group?.id ?? null,
      groupName: group?.name ?? null,
      course: group?.course ?? null,
    });
  }
  return rows.sort((a, b) => a.fullName.localeCompare(b.fullName));
}

export const mockRecipients: readonly RecipientSeed[] = buildRecipients();

function toRow(seed: RecipientSeed): MessageRecipientRow {
  const { facultyId: _f, directionId: _d, groupId: _g, ...row } = seed;
  return row;
}

function readFilter(url: string): RecipientFilter {
  const sp = new URL(url).searchParams;
  const course = Number(sp.get('course'));
  const pick = (key: string) => sp.get(key) || null;
  const q = pick('q');
  const facultyId = pick('facultyId');
  const directionId = pick('directionId');
  const groupId = pick('groupId');
  return {
    ...(q ? { q } : {}),
    ...(facultyId ? { facultyId } : {}),
    ...(directionId ? { directionId } : {}),
    ...(groupId ? { groupId } : {}),
    ...(Number.isInteger(course) && course > 0 ? { course } : {}),
  };
}

function applyFilter(rows: readonly RecipientSeed[], f: RecipientFilter): RecipientSeed[] {
  const q = (f.q ?? '').trim().toLowerCase();
  return rows.filter(
    (r) =>
      (!f.facultyId || r.facultyId === f.facultyId) &&
      (!f.directionId || r.directionId === f.directionId) &&
      (!f.groupId || r.groupId === f.groupId) &&
      (!f.course || r.course === f.course) &&
      (!q ||
        [r.fullName, r.hemisId, String(r.telegramUserId)].some((s) =>
          s?.toLowerCase().includes(q),
        )),
  );
}

/* ───────────────────────── Yuborilgan xabarlar holati ───────────────────────── */

interface DeliveryState extends MessageDeliveryRow {
  attempts: number;
  flaky: boolean;
}

interface MessageState {
  summary: MessageSummary;
  deliveries: DeliveryState[];
}

/** Bir qadamda nechta yetkazish bajariladi. */
const CHUNK = 15;
const BLOCKED_ERROR = 'Forbidden: bot was blocked by the user';
const FLAKY_ERROR = 'Too Many Requests: retry after 5';

function recount(m: MessageState): void {
  const count = (s: DeliveryStatus) => m.deliveries.filter((d) => d.status === s).length;
  m.summary = {
    ...m.summary,
    total: m.deliveries.length,
    sent: count('sent'),
    failed: count('failed'),
    blocked: count('blocked'),
    pending: count('pending'),
  };
}

function newDeliveries(rows: readonly RecipientSeed[]): DeliveryState[] {
  return rows.map((r) => ({
    userId: r.userId,
    fullName: r.fullName,
    hemisId: r.hemisId,
    status: 'pending',
    error: null,
    sentAt: null,
    attempts: 0,
    flaky: FLAKY_INDEXES.has(Number(r.userId.slice('tg-u'.length)) - 1),
  }));
}

function deliver(d: DeliveryState, at: string): void {
  const recipient = mockRecipients.find((r) => r.userId === d.userId);
  d.attempts += 1;
  if (recipient?.botBlocked) {
    d.status = 'blocked';
    d.error = BLOCKED_ERROR;
  } else if (d.flaky && d.attempts === 1) {
    d.status = 'failed';
    d.error = FLAKY_ERROR;
  } else {
    d.status = 'sent';
    d.error = null;
    d.sentAt = at;
  }
}

/** Simulyatsiya qadami: queued → sending → (≤ CHUNK yetkazish) → completed. */
function tick(m: MessageState): void {
  if (m.summary.status === 'completed') return;
  if (m.summary.status === 'queued') {
    m.summary = { ...m.summary, status: 'sending' };
    return;
  }
  const now = new Date().toISOString();
  m.deliveries
    .filter((d) => d.status === 'pending')
    .slice(0, CHUNK)
    .forEach((d) => deliver(d, now));
  recount(m);
  if (m.summary.pending === 0) m.summary = { ...m.summary, status: 'completed' };
}

/** Darhol yakunlangan (seed) xabar. */
function completedMessage(
  id: string,
  createdAt: string,
  text: string,
  audienceLabel: string,
  rows: readonly RecipientSeed[],
  attachAppButton = true,
): MessageState {
  const m: MessageState = {
    summary: {
      id,
      text,
      createdAt,
      createdByName: 'Admin Adminov',
      audienceLabel,
      attachAppButton,
      status: 'completed',
      total: 0,
      sent: 0,
      failed: 0,
      blocked: 0,
      pending: 0,
    },
    deliveries: newDeliveries(rows),
  };
  m.deliveries.forEach((d) => deliver(d, createdAt));
  recount(m);
  return m;
}

function seedMessages(): MessageState[] {
  const group412 = mockRecipients.filter((r) => r.groupId === 'g1');
  return [
    completedMessage(
      'm2',
      '2026-09-28T05:30:00Z',
      "412-22 guruh talabalari, diqqat!\nAmaliyot kundaligini juma kuni soat 18:00 gacha to'ldiring.",
      'Filtr: Guruh: 412-22',
      group412,
    ),
    completedMessage(
      'm1',
      '2026-09-20T04:00:00Z',
      'Assalomu alaykum! Amaliyot davri 1-oktabrdan boshlanadi. Korxonangizni ilova orqali tanlang va tyutoringizga tasdiqlating.',
      'Barcha ulanganlar',
      mockRecipients,
    ),
  ];
}

let messages: MessageState[] = seedMessages();
let nextId = 3;

export function resetMessagesMock(): void {
  messages = seedMessages();
  nextId = 3;
}

function audienceRows(input: SendMessageInput['audience']): RecipientSeed[] {
  if (input.kind === 'all') return [...mockRecipients];
  if (input.kind === 'selected') {
    const ids = new Set(input.userIds);
    return mockRecipients.filter((r) => ids.has(r.userId));
  }
  return applyFilter(mockRecipients, input);
}

function audienceLabel(
  input: SendMessageInput['audience'],
  rows: readonly RecipientSeed[],
): string {
  if (input.kind === 'all') return 'Barcha ulanganlar';
  if (input.kind === 'selected') {
    return rows.length === 1 ? rows[0]!.fullName : `Tanlangan: ${rows.length} ta`;
  }
  const group = GROUPS.find((g) => g.id === input.groupId);
  const faculty = GROUPS.find((g) => g.facultyId === input.facultyId)?.facultyName;
  const direction = GROUPS.find((g) => g.directionId === input.directionId)?.directionName;
  const parts = [
    faculty && `Fakultet: ${faculty}`,
    direction && `Yo'nalish: ${direction}`,
    input.course && `${input.course}-kurs`,
    group && `Guruh: ${group.name}`,
    input.q && `Qidiruv: «${input.q}»`,
  ].filter(Boolean);
  return `Filtr: ${parts.join(' · ') || 'barchasi'}`;
}

function findMessage(id: string): MessageState | undefined {
  return messages.find((m) => m.summary.id === id);
}

const notFound = () => problemResponse(404, 'Topilmadi', 'Xabar topilmadi.');

export const messagesHandlers: HttpHandler[] = [
  // `/recipients/groups` — `/recipients` va `/:id` dan oldin.
  http.get(RECIPIENT_GROUPS_ENDPOINT, ({ request }) => {
    const f = readFilter(request.url);
    const rows = GROUPS.filter(
      (g) =>
        (!f.facultyId || g.facultyId === f.facultyId) &&
        (!f.directionId || g.directionId === f.directionId) &&
        (!f.course || g.course === f.course),
    )
      .map((g) => ({ id: g.id, name: g.name }))
      .sort((a, b) => a.name.localeCompare(b.name));
    return HttpResponse.json(rows);
  }),

  http.get(RECIPIENTS_ENDPOINT, ({ request }) => {
    const filtered = applyFilter(mockRecipients, readFilter(request.url)).map(toRow);
    // `q` allaqachon qo'llangan — paginateMock'ga qidiruvsiz URL beriladi.
    const url = new URL(request.url);
    url.searchParams.delete('q');
    return HttpResponse.json(paginateMock(url.toString(), filtered, () => []));
  }),

  http.post(MESSAGES_ENDPOINT, async ({ request }) => {
    const body = (await request.json().catch(() => null)) as SendMessageInput | null;
    const text = (body?.text ?? '').trim();
    if (!text) {
      return problemResponse(400, "Ma'lumotlar noto'g'ri", 'Xabar matnini kiriting.', {
        errors: { Text: ['Xabar matnini kiriting.'] },
      });
    }
    if (text.length > MESSAGE_TEXT_MAX) {
      return problemResponse(
        400,
        "Ma'lumotlar noto'g'ri",
        `Xabar ${MESSAGE_TEXT_MAX} belgidan oshmasligi kerak.`,
        { errors: { Text: [`Xabar ${MESSAGE_TEXT_MAX} belgidan oshmasligi kerak.`] } },
      );
    }
    const audience = body?.audience ?? { kind: 'all' as const };
    const rows = audienceRows(audience);
    if (rows.length === 0) {
      return problemResponse(
        400,
        "Ma'lumotlar noto'g'ri",
        "Tanlangan auditoriyada Telegram ulangan talaba yo'q.",
      );
    }
    const m: MessageState = {
      summary: {
        id: `m${nextId++}`,
        text,
        createdAt: new Date().toISOString(),
        createdByName: 'Admin Adminov',
        audienceLabel: audienceLabel(audience, rows),
        attachAppButton: body?.attachAppButton ?? true,
        status: 'queued',
        total: 0,
        sent: 0,
        failed: 0,
        blocked: 0,
        pending: 0,
      },
      deliveries: newDeliveries(rows),
    };
    recount(m);
    messages.unshift(m);
    return HttpResponse.json(m.summary, { status: 201 });
  }),

  http.get(MESSAGES_ENDPOINT, ({ request }) => {
    messages.forEach(tick);
    const sorted = [...messages].sort((a, b) =>
      b.summary.createdAt.localeCompare(a.summary.createdAt),
    );
    return HttpResponse.json(
      paginateMock(
        request.url,
        sorted.map((m) => m.summary),
        () => [],
      ),
    );
  }),

  http.get(`${MESSAGES_ENDPOINT}/:id/deliveries`, ({ params, request }) => {
    const m = findMessage(String(params['id']));
    if (!m) return notFound();
    tick(m);
    const sp = new URL(request.url).searchParams;
    const status = sp.get('status');
    const rows = m.deliveries
      .filter((d) => !status || d.status === status)
      .map(({ attempts: _a, flaky: _f, ...row }) => ({ ...row }));
    return HttpResponse.json(paginateMock(request.url, rows, (d) => [d.fullName, d.hemisId]));
  }),

  http.post(`${MESSAGES_ENDPOINT}/:id/retry`, ({ params }) => {
    const m = findMessage(String(params['id']));
    if (!m) return notFound();
    let requeued = 0;
    m.deliveries.forEach((d) => {
      if (d.status !== 'failed') return;
      d.status = 'pending';
      d.error = null;
      requeued += 1;
    });
    recount(m);
    if (requeued > 0) m.summary = { ...m.summary, status: 'queued' };
    return HttpResponse.json(m.summary);
  }),

  http.get(`${MESSAGES_ENDPOINT}/:id`, ({ params }) => {
    const m = findMessage(String(params['id']));
    if (!m) return notFound();
    tick(m);
    return HttpResponse.json(m.summary);
  }),
];
