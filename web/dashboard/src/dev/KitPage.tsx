import { useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Alert,
  AlertList,
  AlertRow,
  Avatar,
  Badge,
  Button,
  Card,
  CardBody,
  CardFooter,
  CardHeader,
  CardRow,
  Checkbox,
  Chip,
  ChipRow,
  DataTable,
  EmptyState,
  Eyebrow,
  FactGrid,
  FileBox,
  Input,
  MapPlaceholder,
  PersonCell,
  Pill,
  PillGroup,
  ProgressBar,
  Select,
  SidebarNav,
  StatGrid,
  StatTile,
  STATUS_KINDS,
  Textarea,
  Topbar,
  type ButtonSize,
  type ButtonVariant,
  type DataTableColumn,
} from '@/shared/ui';
import { navForRole } from '@/app/nav';
import { formatClock } from '@/app/layout/useClock';
import styles from './KitPage.module.css';

interface DemoStudent {
  id: string;
  name: string;
  group: string;
  company: string;
  checkIn: string | null;
  distance: string;
  status: 'ok' | 'late' | 'bad' | 'info';
  statusLabel: string;
  pct: number;
}

const STUDENTS: DemoStudent[] = [
  {
    id: '1',
    name: 'Akmal Aliyev',
    group: '412-22',
    company: 'UzAuto Motors',
    checkIn: '08:52',
    distance: '45 m',
    status: 'ok',
    statusLabel: 'Keldi',
    pct: 92,
  },
  {
    id: '2',
    name: 'Dilnoza Karimova',
    group: '412-22',
    company: 'Artel Electronics',
    checkIn: '09:21',
    distance: '120 m',
    status: 'late',
    statusLabel: 'Kech keldi',
    pct: 78,
  },
  {
    id: '3',
    name: 'Sardor Yusupov',
    group: '413-22',
    company: 'Uzbektelecom',
    checkIn: null,
    distance: '—',
    status: 'bad',
    statusLabel: 'Kelmadi',
    pct: 54,
  },
  {
    id: '4',
    name: 'Malika Rashidova',
    group: '413-22',
    company: 'Beeline',
    checkIn: null,
    distance: '—',
    status: 'info',
    statusLabel: 'Sababli',
    pct: 88,
  },
];

const COLUMNS: DataTableColumn<DemoStudent>[] = [
  {
    key: 'name',
    header: 'Talaba',
    width: '1.8fr',
    render: (r) => <PersonCell name={r.name} sub={r.group} />,
  },
  { key: 'company', header: 'Korxona', width: '1.3fr' },
  {
    key: 'checkIn',
    header: 'Check-in',
    width: '90px',
    mono: true,
    dim: true,
    render: (r) => r.checkIn ?? '—',
  },
  { key: 'distance', header: 'Masofa', width: '80px', mono: true },
  {
    key: 'status',
    header: 'Holat',
    width: '110px',
    render: (r) => <Badge status={r.status}>{r.statusLabel}</Badge>,
  },
  {
    key: 'pct',
    header: 'Davomat',
    width: '150px',
    render: (r) => <ProgressBar value={r.pct} label={`${r.name} davomati`} />,
  },
];

const VARIANTS: ButtonVariant[] = ['primary', 'secondary', 'danger', 'dashed', 'alert'];
const SIZES: ButtonSize[] = ['xs', 'sm', 'md', 'lg'];

function Section({
  id,
  title,
  children,
}: {
  id: string;
  title: string;
  children: React.ReactNode;
}) {
  return (
    <section id={id} className={styles.section}>
      <Eyebrow as="h2" margin="none">
        {title}
      </Eyebrow>
      {children}
    </section>
  );
}

/**
 * /dev/kit — UI kit ko'rgazmasi (Storybook o'rniga). Faqat DEV.
 * Keyingi agentlar: har komponentning barcha variantlari shu yerda; yangi komponent qo'shsangiz — bu yerga ham.
 */
export function KitPage() {
  const [density, setDensity] = useState<'comfortable' | 'compact'>('comfortable');
  const [filter, setFilter] = useState('Hammasi');
  const [tab, setTab] = useState('Yangi');
  const [score, setScore] = useState(4);
  const sections = [
    'button',
    'pill',
    'badge',
    'table',
    'card',
    'progress',
    'stat',
    'facts',
    'field',
    'sidebar',
    'topbar',
    'alert',
    'avatar',
    'chip',
    'map',
    'eyebrow',
    'empty',
  ];

  return (
    <div className={styles.page}>
      <div className={styles.top}>
        <div>
          <h1 className={styles.h1}>UI kit</h1>
          <p className={styles.lead}>
            SPEC-TOKENS.md komponentlari. Import: <code className={styles.code}>@/shared/ui</code>{' '}
            (bazaviylari <code className={styles.code}>@amaliyotchi/shared/ui</code> dan).{' '}
            <Link to="/login">→ Login</Link>
          </p>
        </div>
        <nav className={styles.toc} aria-label="Bo'limlar">
          {sections.map((s) => (
            <a key={s} href={`#${s}`}>
              {s}
            </a>
          ))}
        </nav>
      </div>

      <Section id="button" title="Button — variant × size, asChild, block, disabled">
        {VARIANTS.map((v) => (
          <div key={v} className={styles.row}>
            <span className={styles.code}>{v}</span>
            {SIZES.map((s) => (
              <Button key={s} variant={v} size={s}>
                {v} {s}
              </Button>
            ))}
            <Button variant={v} disabled>
              disabled
            </Button>
          </div>
        ))}
        <div className={styles.row}>
          <span className={styles.code}>score</span>
          {[1, 2, 3, 4, 5].map((n) => (
            <Button key={n} variant="score" aria-pressed={score === n} onClick={() => setScore(n)}>
              {n}
            </Button>
          ))}
          <span className={styles.code}>asChild → Link</span>
          <Button asChild variant="primary" size="sm">
            <Link to="/login">Login sahifasi</Link>
          </Button>
          <span className={styles.code}>ghost-dark (dark fonda)</span>
          <span className={styles.dark} style={{ padding: 8 }}>
            <Button variant="ghost-dark">Chiqish</Button>
          </span>
        </div>
        <div className={styles.grid2}>
          <Button variant="primary" size="lg" block>
            Kirish (lg, block)
          </Button>
          <Button variant="checkin">KELDIM</Button>
          <Button variant="checkin" tone="dark">
            KETDIM
          </Button>
        </div>
      </Section>

      <Section id="pill" title="Pill — round / square / tab (count) / wide">
        <PillGroup aria-label="Kun filtri">
          {['Hammasi', 'Keldi', 'Kech keldi', 'Kelmadi', 'Shubhali'].map((f) => (
            <Pill key={f} active={filter === f} onClick={() => setFilter(f)}>
              {f}
            </Pill>
          ))}
        </PillGroup>
        <PillGroup>
          {["Ro'yxat", 'Kalendar', 'Xarita', 'Korxona'].map((v, i) => (
            <Pill key={v} shape="square" active={i === 0}>
              {v}
            </Pill>
          ))}
        </PillGroup>
        <PillGroup role="tablist">
          {[
            ['Yangi', 7],
            ['Tuzatishda', 2],
            ['Tasdiqlangan', 24],
            ['Rad etilgan', 1],
          ].map(([t, c]) => (
            <Pill
              key={t}
              role="tab"
              shape="tab"
              count={c}
              active={tab === t}
              onClick={() => setTab(String(t))}
            >
              {t}
            </Pill>
          ))}
        </PillGroup>
        <PillGroup stretch style={{ maxWidth: 400 }}>
          <Pill shape="wide" active>
            Tyutor
          </Pill>
          <Pill shape="wide">Admin</Pill>
          <Pill shape="wide">Talaba</Pill>
        </PillGroup>
      </Section>

      <Section id="badge" title="Badge — status × size">
        {(['sm', 'default', 'md', 'lg'] as const).map((size) => (
          <div key={size} className={styles.row}>
            <span className={styles.code}>{size}</span>
            {STATUS_KINDS.map((k) => (
              <Badge key={k} status={k} size={size}>
                {k}
              </Badge>
            ))}
          </div>
        ))}
      </Section>

      <Section id="table" title="DataTable — grid, toolbar, actions, density, bo'sh holat">
        <DataTable
          aria-label="Bugungi davomat"
          columns={COLUMNS}
          rows={STUDENTS}
          rowKey={(r) => r.id}
          density={density}
          minWidth="820px"
          toolbar={
            <>
              <Input variant="search" placeholder="Qidirish…" aria-label="Qidirish" />
              <PillGroup>
                <Pill
                  shape="square"
                  active={density === 'comfortable'}
                  onClick={() => setDensity('comfortable')}
                >
                  comfortable
                </Pill>
                <Pill
                  shape="square"
                  active={density === 'compact'}
                  onClick={() => setDensity('compact')}
                >
                  compact
                </Pill>
              </PillGroup>
            </>
          }
          actions={() => (
            <>
              <Button size="xs">Ko'rish</Button>
              <Button size="xs">Izoh</Button>
            </>
          )}
          footer={
            <>
              <span>1–4 / 38</span>
              <span className={styles.row}>
                <Button size="xs">Oldingi</Button>
                <Button size="xs">Keyingi</Button>
              </span>
            </>
          }
        />
        <DataTable
          aria-label="Bo'sh jadval"
          columns={COLUMNS.slice(0, 3)}
          rows={[]}
          rowKey={(r) => r.id}
          emptyText="Bugun uchun yozuvlar yo'q"
        />
      </Section>

      <Section id="card" title="Card — header / body / row / footer, padded">
        <div className={styles.grid2}>
          <Card>
            <CardHeader
              title="Section sarlavha"
              subtitle="izoh"
              actions={<Button size="sm">Eksport</Button>}
            />
            <CardRow>Qator 1 (12px 18px)</CardRow>
            <CardRow>Qator 2</CardRow>
            <CardFooter>
              <Button variant="primary" size="sm">
                Tasdiqlash
              </Button>
              <Button size="sm">Bekor qilish</Button>
            </CardFooter>
          </Card>
          <Card as="article" padded>
            <Eyebrow>Kundalik · 12.10.2026</Eyebrow>
            <p>
              `padded` kartochka (18px). Matn 13.5px, line-height 1.6 — kundalik yozuvi, xulosa va
              h.k.
            </p>
          </Card>
          <Card>
            <CardHeader title="CardBody" level={3} />
            <CardBody>Body — 18px padding.</CardBody>
          </Card>
        </div>
      </Section>

      <Section id="progress" title="ProgressBar — rang qoidasi ≥85 ok · ≥70 late · <70 bad">
        <div className={styles.grid2}>
          <ProgressBar value={92} label="92%" />
          <ProgressBar value={76} label="76%" />
          <ProgressBar value={48} label="48%" />
          <ProgressBar value={60} color="var(--color-info-fg)" showValue={false} label="custom" />
        </div>
      </Section>

      <Section id="stat" title="StatTile + StatGrid">
        <StatGrid min={170}>
          <StatTile label="Keldi" value="31" note="38 dan" dot="ok" />
          <StatTile label="Kech keldi" value="4" note="+2 kechagidan" dot="late" noteTone="late" />
          <StatTile label="Kelmadi" value="2" note="Shubhali: 1" dot="bad" noteTone="bad" />
          <StatTile label="Sababli" value="1" dot="info" />
          <StatTile label="Davomat" value="86%" note="oy bo'yicha" />
        </StatGrid>
      </Section>

      <Section id="facts" title="FactGrid — mono / detail / portfolio">
        <FactGrid
          columns={2}
          items={[
            { k: 'Vaqt', v: '08:52' },
            { k: 'Masofa', v: '45 m' },
            { k: 'Koordinata', v: '41.3111, 69.2797' },
            { k: 'Radius', v: '150 m' },
          ]}
        />
        <FactGrid
          variant="detail"
          items={[
            { k: 'Korxona', v: 'UzAuto Motors' },
            { k: 'Manzil', v: "Toshkent, Yakkasaroy tumani, Bobur ko'chasi 12" },
            { k: 'STIR', v: '305 123 456' },
          ]}
        />
        <FactGrid
          variant="portfolio"
          min={120}
          items={[
            { k: 'Kunlar', v: '42' },
            { k: 'Davomat', v: '92%' },
            { k: 'Kundalik', v: '38' },
            { k: 'Baho', v: '5' },
          ]}
        />
      </Section>

      <Section
        id="field"
        title="Input / Textarea / Select / Checkbox — label, hint, error, mono, variant"
      >
        <div className={styles.grid2}>
          <Input label="HEMIS ID" mono placeholder="210457" hint="Faqat raqamlar" />
          <Input
            label="Parol"
            type="password"
            placeholder="••••••••"
            error="Parol kamida 8 ta belgi"
          />
          <Input label="Boshlanish sanasi" variant="form" mono type="date" />
          <Input label="Qidiruv (search)" variant="search" placeholder="Ism yoki guruh…" />
          <Input label="Readonly qiymat" variant="readonly" readOnly value="150 m" />
          <Select
            label="Fakultet"
            placeholder="Tanlang"
            defaultValue=""
            options={[
              { value: 'at', label: 'Axborot texnologiyalari' },
              { value: 'iq', label: 'Iqtisodiyot' },
            ]}
          />
          <Textarea label="Kundalik yozuvi" placeholder="Bugun nima qildingiz…" />
          <Textarea
            label="Sabab (form)"
            variant="form"
            placeholder="Qisqacha…"
            hint="Kamida 20 belgi"
          />
        </div>
        <div className={styles.row}>
          <Checkbox label="Bu qurilmada eslab qolish" defaultChecked />
          <Checkbox label="O'chirilgan" disabled />
        </div>
      </Section>

      <Section id="sidebar" title="SidebarNav — aktiv (aria-current), badge (tyutor ro'yxati)">
        <div className={styles.grid2}>
          <div className={styles.dark}>
            <SidebarNav items={navForRole('Tutor')} />
          </div>
          <div className={styles.dark}>
            <SidebarNav items={navForRole('Admin')} />
          </div>
        </div>
        <p className={styles.code}>
          To'liq Sidebar (brend + user bloki) — AppShell ichida: /admin yoki /tutor ga kiring.
        </p>
      </Section>

      <Section id="topbar" title="Topbar — crumb, h1, sana chipi, actions">
        <div className={styles.demoTopbar}>
          <Topbar
            crumb="Tyutor · 412-22, 413-22 · 3-kurs amaliyoti"
            title="Bugun"
            clock={formatClock(new Date())}
            actions={<Button size="sm">Eksport</Button>}
            onMenuClick={() => undefined}
          />
        </div>
      </Section>

      <Section id="alert" title="Alert (amber) + AlertRow + action">
        <Alert>
          <AlertList>
            <AlertRow action={<Button variant="alert">Ko'rish</Button>}>
              3 talaba 3 kundan beri check-in qilmagan
            </AlertRow>
            <AlertRow action={<Button variant="alert">Eslatma yuborish</Button>}>
              12 kundalik tekshirilmagan
            </AlertRow>
            <AlertRow>Geofence radiusi 2 korxonada sozlanmagan</AlertRow>
          </AlertList>
        </Alert>
      </Section>

      <Section id="avatar" title="Avatar — table / card / detail / sidebar">
        <div className={styles.row}>
          <Avatar name="Nodira Saidova" variant="table" />
          <Avatar name="Nodira Saidova" variant="card" />
          <Avatar name="Nodira Saidova" variant="detail" />
          <span className={styles.dark} style={{ padding: 8 }}>
            <Avatar name="Nodira Saidova" variant="sidebar" />
          </span>
          <Avatar initials="?" />
        </div>
      </Section>

      <Section id="chip" title="Chip — file / fmt, FileBox">
        <ChipRow>
          <Chip>hisobot-oktabr.pdf</Chip>
          <Chip>rasm-01.jpg</Chip>
          <Chip variant="fmt">PDF</Chip>
          <Chip variant="fmt">XLSX</Chip>
        </ChipRow>
        <div className={styles.grid2}>
          <FileBox
            name="Amaliyot shartnomasi.pdf"
            meta="412 KB · 12.10.2026"
            action={<Button size="sm">Yuklab olish</Button>}
          />
          <FileBox name="Tasdiqlovchi hujjat" meta="yuklanmagan" />
        </div>
      </Section>

      <Section id="map" title="MapPlaceholder — 186 / 150 / 460">
        <div className={styles.grid2}>
          <MapPlaceholder title="Geofence nuqtasi" coords="41.3111, 69.2797" note="radius 150 m" />
          <MapPlaceholder title="Amaliyot joyim" coords="41.2995, 69.2401" height={150} />
        </div>
      </Section>

      <Section id="eyebrow" title="Eyebrow — spacing normal / wide, tone alert">
        <Eyebrow>Geofence nuqtasi</Eyebrow>
        <Eyebrow spacing="wide">Bugun · 12.10.2026</Eyebrow>
        <Eyebrow tone="alert" spacing="wide">
          Diqqat talab qiladi
        </Eyebrow>
      </Section>

      <Section id="empty" title="EmptyState — dashed / plain (❓ dizaynda yo'q)">
        <div className={styles.grid2}>
          <EmptyState
            title="Arizalar yo'q"
            description="Yangi ariza kelganda shu yerda ko'rinadi."
            action={
              <Button variant="primary" size="sm">
                Yangilash
              </Button>
            }
          />
          <EmptyState tone="plain" title="Ma'lumot yo'q" />
        </div>
      </Section>
    </div>
  );
}

export default KitPage;
