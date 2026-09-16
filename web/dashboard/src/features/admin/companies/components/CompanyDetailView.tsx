import {
  Alert,
  AlertRow,
  Badge,
  Card,
  CardBody,
  CardHeader,
  DataTable,
  EmptyState,
  FactGrid,
  MapPlaceholder,
  StatGrid,
  StatTile,
  type DataTableColumn,
} from '@/shared/ui';
import { DASH, formatCount, formatDayMonth, formatPhone, formatTin } from '../../shared/format';
import {
  COMPANY_FLAG_LABEL,
  studentsOfLimit,
  type CompanyDetail,
  type CompanyPeriod,
} from '../types';
import styles from './CompanyDetailView.module.css';

const PERIOD_COLUMNS: DataTableColumn<CompanyPeriod>[] = [
  { key: 'name', header: 'Davr', width: 'minmax(200px,1.8fr)', strong: true },
  {
    key: 'range',
    header: 'Muddat',
    width: 'minmax(170px,1.2fr)',
    mono: true,
    dim: true,
    render: (r) => `${formatDayMonth(r.startDate, true)} — ${formatDayMonth(r.endDate, true)}`,
  },
  {
    key: 'students',
    header: 'Talaba',
    width: 'minmax(90px,.6fr)',
    mono: true,
    render: (r) => formatCount(r.students),
  },
];

/** "41.3111, 69.2797" — xarita hali yo'q, koordinata matn sifatida ko'rsatiladi. */
function formatCoords(lat: number, lng: number): string {
  return `${lat.toFixed(4)}, ${lng.toFixed(4)}`;
}

function contact(name: string | null, phone: string | null) {
  if (!name) return DASH;
  return (
    <span className={styles.contact}>
      <span>{name}</span>
      <span className={styles.mono}>{formatPhone(phone)}</span>
    </span>
  );
}

/**
 * Admin · Korxona detali (presentation): ma'lumot kartasi + lokatsiya, STIR nazorati bloki
 * va amaliyot davrlari kesimi. Talabalar jadvali alohida komponent.
 */
export function CompanyDetailView({ company }: { company: CompanyDetail }) {
  const flag = company.flag ? COMPANY_FLAG_LABEL[company.flag] : null;
  return (
    <div className={styles.page}>
      <Card aria-label="Korxona ma'lumotlari">
        <CardHeader
          title={company.name}
          subtitle={
            <span className={styles.sub}>
              <span className={styles.mono}>{formatTin(company.tin)}</span> · {company.activity}
            </span>
          }
          actions={
            <>
              {flag && <Badge status={flag.kind}>{flag.label}</Badge>}
              {company.isActive ? (
                <Badge status="ok">Faol</Badge>
              ) : (
                <Badge status="neu">Faol emas</Badge>
              )}
            </>
          }
        />
        <CardBody className={styles.body}>
          <FactGrid
            variant="detail"
            min={180}
            items={[
              { k: 'STIR', v: <span className={styles.mono}>{formatTin(company.tin)}</span> },
              { k: 'Faoliyat', v: company.activity },
              { k: 'Manzil', v: company.address },
              { k: 'Radius', v: <span className={styles.mono}>{company.radiusM} m</span> },
              { k: 'Rahbar', v: contact(company.supervisorName, company.supervisorPhone) },
              { k: 'Mentor', v: contact(company.mentorName, company.mentorPhone) },
              {
                k: 'Lokatsiya',
                v: <span className={styles.mono}>{formatCoords(company.lat, company.lng)}</span>,
              },
              {
                k: 'Talabalar',
                v: (
                  <span className={styles.mono}>
                    {studentsOfLimit(company.students, company.maxStudents)}
                  </span>
                ),
              },
            ]}
          />
          <MapPlaceholder
            height={186}
            title="Korxona lokatsiyasi"
            coords={formatCoords(company.lat, company.lng)}
            note={`Geofence radiusi ${company.radiusM} m. Haqiqiy xarita keyingi bosqichda ulanadi.`}
          />
        </CardBody>
      </Card>

      <Card aria-label="STIR nazorati">
        <CardHeader
          title="STIR nazorati"
          subtitle={`Bitta STIR ostida biriktirilgan talabalar soni chegara bilan solishtiriladi (STIR ${formatTin(company.tin)}).`}
        />
        <CardBody className={styles.body}>
          {company.overLimit && (
            <Alert title="STIR chegarasi oshgan">
              <AlertRow>
                {company.name} korxonasiga {formatCount(company.students)} talaba biriktirilgan —
                ruxsat etilgan chegara {company.maxStudents} ta. Ortiqcha biriktirishlarni
                tekshiring.
              </AlertRow>
            </Alert>
          )}
          <StatGrid min={180}>
            <StatTile
              label="Biriktirilgan talabalar"
              value={formatCount(company.students)}
              note={company.overLimit ? 'Chegaradan oshgan' : 'Chegara doirasida'}
              {...(company.overLimit ? { noteTone: 'bad' as const, dot: 'bad' as const } : {})}
            />
            <StatTile
              label="Chegara"
              value={formatCount(company.maxStudents)}
              note="Sozlama: korxonaga maksimal talaba"
            />
            <StatTile
              label="Shubhali kunlar"
              value={formatCount(company.suspiciousDays)}
              {...(company.suspiciousDays >= 3
                ? { note: "Shubhali to'planish", noteTone: 'bad' as const }
                : {})}
            />
          </StatGrid>
        </CardBody>
      </Card>

      <DataTable
        aria-label="Amaliyot davrlari"
        columns={PERIOD_COLUMNS}
        rows={company.periods}
        rowKey={(r) => r.id}
        minWidth="520px"
        density="compact"
        toolbar={
          <div>
            <h2 className={styles.tableTitle}>Amaliyot davrlari</h2>
            <p className={styles.tableSub}>Qaysi davrda nechta talaba shu korxonada.</p>
          </div>
        }
        emptyText={
          <EmptyState
            tone="plain"
            className={styles.empty}
            title="Amaliyot davrlari yo'q"
            description="Bu korxonaga hali birorta davrda talaba biriktirilmagan."
          />
        }
      />
    </div>
  );
}
