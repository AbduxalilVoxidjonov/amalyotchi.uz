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
import { fmtDateRange, fmtDistance, fmtPhone, fmtTin } from '../../format';
import {
  COMPANY_FLAG_LABEL,
  studentsOfLimit,
  type CompanyPeriod,
  type TutorCompanyDetail,
} from '../types';
import styles from './CompanyDetailView.module.css';

const PERIOD_COLUMNS: DataTableColumn<CompanyPeriod>[] = [
  { key: 'name', header: 'Davr', width: 'minmax(190px,1.8fr)', strong: true },
  {
    key: 'range',
    header: 'Muddat',
    width: 'minmax(170px,1.2fr)',
    mono: true,
    dim: true,
    render: (r) => fmtDateRange(r.startDate, r.endDate),
  },
  { key: 'students', header: 'Aktiv talaba', width: 'minmax(90px,.6fr)', mono: true },
];

/** "41.3111, 69.2797" — haqiqiy xarita hali yo'q, koordinata matn sifatida. */
function formatCoords(lat: number, lng: number): string {
  return `${lat.toFixed(4)}, ${lng.toFixed(4)}`;
}

function contact(name: string | null, phone: string | null) {
  if (!name) return '—';
  return (
    <span className={styles.contact}>
      <span>{name}</span>
      <span className={styles.mono}>{phone ? fmtPhone(phone) : '—'}</span>
    </span>
  );
}

/**
 * Tyutor · Korxona detali (presentation): ma'lumot kartasi + lokatsiya, STIR nazorati
 * va aktiv amaliyot davri (faqat hozir davom etayotgan ochiq davr(lar); bo'sh bo'lishi mumkin).
 * Talaba sonlari — faqat aktiv amaliyotchilar. Talabalar jadvali — alohida komponent.
 */
export function CompanyDetailView({ company }: { company: TutorCompanyDetail }) {
  const flag = company.flag ? COMPANY_FLAG_LABEL[company.flag] : null;
  return (
    <div className={styles.blocks}>
      <Card aria-label="Korxona ma'lumotlari">
        <CardHeader
          title={company.name}
          subtitle={
            <span className={styles.sub}>
              <span className={styles.mono}>{fmtTin(company.tin)}</span> · {company.activity}
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
              { k: 'STIR', v: <span className={styles.mono}>{fmtTin(company.tin)}</span> },
              { k: 'Faoliyat', v: company.activity },
              { k: 'Manzil', v: company.address },
              {
                k: 'Radius',
                v: <span className={styles.mono}>{fmtDistance(company.radiusM)}</span>,
              },
              { k: 'Rahbar', v: contact(company.supervisorName, company.supervisorPhone) },
              { k: 'Mentor', v: contact(company.mentorName, company.mentorPhone) },
              {
                k: 'Lokatsiya',
                v: <span className={styles.mono}>{formatCoords(company.lat, company.lng)}</span>,
              },
              {
                k: 'Aktiv talabalarim',
                v: (
                  <span className={styles.mono}>
                    {company.students} / {company.totalStudents}
                  </span>
                ),
              },
            ]}
          />
          <MapPlaceholder
            height={186}
            title="Korxona lokatsiyasi"
            coords={formatCoords(company.lat, company.lng)}
            note={`Geofence radiusi ${fmtDistance(company.radiusM)}. Haqiqiy xarita keyingi bosqichda ulanadi.`}
          />
        </CardBody>
      </Card>

      <Card aria-label="STIR nazorati">
        <CardHeader
          title="STIR nazorati"
          subtitle={`Bitta STIR ostida aktiv amaliyot o'tayotgan talabalar soni chegara bilan solishtiriladi (STIR ${fmtTin(company.tin)}).`}
        />
        <CardBody className={styles.body}>
          {company.overLimit && (
            <Alert title="STIR chegarasi oshgan">
              <AlertRow>
                {company.name} korxonasida tizim bo'yicha {company.totalStudents} talaba aktiv
                amaliyot o'tamoqda — ruxsat etilgan chegara {company.maxStudents} ta. Sizning
                ko'lamingizda {company.students} talaba.
              </AlertRow>
            </Alert>
          )}
          <StatGrid min={170}>
            <StatTile label="Ko'lamdagi aktiv talabalar" value={company.students} />
            <StatTile
              label="Jami aktiv talabalar"
              value={studentsOfLimit(company.totalStudents, company.maxStudents)}
              note={company.overLimit ? 'Chegaradan oshgan' : 'Chegara doirasida'}
              {...(company.overLimit ? { noteTone: 'bad' as const, dot: 'bad' as const } : {})}
            />
            <StatTile
              label="Shubhali kunlar"
              value={company.suspiciousDays}
              {...(company.suspiciousDays >= 3
                ? { note: "Shubhali to'planish", noteTone: 'bad' as const }
                : {})}
            />
          </StatGrid>
        </CardBody>
      </Card>

      <DataTable
        aria-label="Aktiv amaliyot davri"
        columns={PERIOD_COLUMNS}
        rows={company.periods}
        rowKey={(r) => r.id}
        minWidth="520px"
        density="compact"
        toolbar={
          <div>
            <h2 className={styles.tableTitle}>Aktiv amaliyot davri</h2>
            <p className={styles.tableSub}>
              Hozir davom etayotgan davr va unda shu korxonada amaliyot o'tayotgan talabalar soni.
            </p>
          </div>
        }
        emptyText={
          <EmptyState
            tone="plain"
            className={styles.empty}
            title="Aktiv davr yo'q"
            description="Hozirda bu korxonada davom etayotgan amaliyot davri yo'q."
          />
        }
      />
    </div>
  );
}
