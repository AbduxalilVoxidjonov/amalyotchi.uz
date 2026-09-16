import {
  Avatar,
  Badge,
  Card,
  CardBody,
  CardHeader,
  EmptyState,
  FactGrid,
  FileBox,
  MapPlaceholder,
  ProgressBar,
  StatGrid,
  StatTile,
  type FactItem,
} from '@/shared/ui';
import {
  fmtBytes,
  fmtDate,
  fmtDateOnly,
  fmtDecimal,
  fmtDistance,
  fmtPhone,
  fmtTin,
} from '../../format';
import {
  fmtCoords,
  isCompanyBoundApplication,
  STUDENT_APPLICATION_STATUS_LABEL,
  STUDENT_STATUS_LABEL,
  studentStateLabel,
  workDaysLabel,
  type TutorStudentDetail,
} from '../types';
import styles from './StudentDetailView.module.css';

const person = (name: string | null, phone: string | null): string =>
  name ? (phone ? `${name} · ${fmtPhone(phone)}` : name) : '—';

/** Talaba profili — sarlavha kartasi, korxona, ariza va amaliyot davri bloklari (KONTRAKT §2.1). */
export function StudentDetailView({ detail }: { detail: TutorStudentDetail }) {
  const state = studentStateLabel(detail);
  const status = STUDENT_STATUS_LABEL[detail.status];
  const a = detail.attendance;
  const company = detail.company;
  const application = detail.application;
  const period = detail.period;

  const profileFacts: FactItem[] = [
    { k: 'HEMIS ID', v: detail.hemisId },
    { k: 'Guruh', v: detail.group },
    { k: 'Kurs', v: `${detail.course}-kurs` },
    { k: 'Fakultet', v: detail.faculty },
    { k: "Yo'nalish", v: detail.direction },
    { k: 'Telefon', v: detail.phone ? fmtPhone(detail.phone) : '—' },
  ];

  return (
    <>
      <Card as="article" aria-label={`Talaba: ${detail.name}`}>
        <header className={styles.head}>
          <div className={styles.headLeft}>
            <Avatar name={detail.name} variant="detail" />
            <div>
              <h2 className={styles.name}>{detail.name}</h2>
              <div className={styles.meta}>
                HEMIS {detail.hemisId} · {detail.group} · {detail.course}-kurs · {detail.faculty}
              </div>
            </div>
          </div>
          <div className={styles.badges}>
            <Badge status={status.kind} size="md">
              {status.label}
            </Badge>
            <Badge status={state.kind} size="md">
              {state.label}
            </Badge>
          </div>
        </header>

        <FactGrid variant="detail" min={190} items={profileFacts} className={styles.facts} />

        <div className={styles.progress}>
          <span className={styles.progressLabel}>Davomat</span>
          <ProgressBar value={a.attendancePct} label={`${detail.name} davomati`} />
          <span className={styles.progressNote}>
            {a.attendedDays + a.lateDays}/{a.totalDays} kun · {fmtDecimal(a.attendancePct)}%
          </span>
        </div>

        <StatGrid min={150} className={styles.stats}>
          <StatTile dot="ok" label="Keldi" value={a.attendedDays} />
          <StatTile dot="late" label="Kech keldi" value={a.lateDays} />
          <StatTile dot="bad" label="Kelmadi" value={a.absentDays} />
          <StatTile dot="info" label="Sababli" value={a.excusedDays} />
          <StatTile
            dot="bad"
            label="Shubhali"
            value={a.suspiciousDays}
            note={
              detail.suspiciousCount > 0 ? `${detail.suspiciousCount} ta belgilangan` : undefined
            }
          />
          <StatTile
            label="Kundaliklar"
            value={detail.diary.count}
            note={`${detail.diary.scoredCount} ta baholangan · o'rtacha ${fmtDecimal(detail.diary.avg)}`}
          />
          <StatTile
            label="Yakuniy ball"
            value={detail.grade ? fmtDecimal(detail.grade.total) : '—'}
            note={detail.grade?.grade ? `Baho: ${detail.grade.grade}` : 'Hali baholanmagan'}
          />
        </StatGrid>
      </Card>

      <Card as="section" aria-label="Korxona">
        <CardHeader title="Korxona" subtitle={company ? company.name : undefined} />
        <CardBody>
          {company ? (
            <div className={styles.columns}>
              <FactGrid
                variant="detail"
                min={200}
                items={[
                  { k: 'Korxona', v: company.name },
                  { k: 'STIR', v: fmtTin(company.tin) },
                  { k: 'Faoliyat turi', v: company.activity },
                  { k: 'Manzil', v: company.address },
                  { k: 'Rahbar', v: person(company.supervisorName, company.supervisorPhone) },
                  { k: 'Mentor', v: person(company.mentorName, company.mentorPhone) },
                  { k: 'Geofence radiusi', v: fmtDistance(company.radiusM) },
                  { k: 'Lokatsiya', v: fmtCoords(company.lat, company.lng) ?? '—' },
                ]}
              />
              <MapPlaceholder
                title="Korxona joylashuvi"
                coords={fmtCoords(company.lat, company.lng)}
                note={`Radius: ${fmtDistance(company.radiusM)}`}
                height={186}
              />
            </div>
          ) : (
            <EmptyState
              title={
                application && !isCompanyBoundApplication(application.status)
                  ? 'Ariza hali tasdiqlanmagan'
                  : 'Korxona biriktirilmagan'
              }
              description="Korxona faqat tasdiqlangan arizadan keyin biriktiriladi."
            />
          )}
        </CardBody>
      </Card>

      <Card as="section" aria-label="Ariza">
        <CardHeader title="Ariza" />
        <CardBody>
          {application ? (
            <>
              <FactGrid
                variant="detail"
                min={200}
                items={[
                  {
                    k: 'Holat',
                    v: (
                      <Badge status={STUDENT_APPLICATION_STATUS_LABEL[application.status].kind}>
                        {STUDENT_APPLICATION_STATUS_LABEL[application.status].label}
                      </Badge>
                    ),
                  },
                  { k: 'Yuborilgan', v: fmtDate(application.submittedAt) },
                  {
                    k: 'Qaror sanasi',
                    v: application.decidedAt ? fmtDate(application.decidedAt) : '—',
                  },
                ]}
              />
              {application.comment && (
                <p className={styles.comment}>
                  <span className={styles.commentLabel}>Tyutor izohi: </span>
                  {application.comment}
                </p>
              )}
              {application.contract ? (
                <FileBox
                  className={styles.file}
                  name={application.contract.name}
                  meta={
                    <>
                      {application.contract.pages !== null &&
                        `${application.contract.pages} bet · `}
                      {fmtBytes(application.contract.sizeBytes)} ·{' '}
                      <a href={application.contract.url} target="_blank" rel="noreferrer">
                        shartnomani ochish
                      </a>
                    </>
                  }
                />
              ) : (
                <FileBox className={styles.file} name="Shartnoma yuklanmagan" meta="fayl yo'q" />
              )}
            </>
          ) : (
            <EmptyState title="Ariza topshirilmagan" />
          )}
        </CardBody>
      </Card>

      <Card as="section" aria-label="Amaliyot davri">
        <CardHeader title="Amaliyot davri" subtitle={period ? period.name : undefined} />
        <CardBody>
          {period ? (
            <FactGrid
              variant="detail"
              min={180}
              items={[
                { k: 'Davr', v: period.name },
                { k: 'Boshlanishi', v: fmtDateOnly(period.startDate) },
                { k: 'Tugashi', v: fmtDateOnly(period.endDate) },
                { k: 'Kunlik vaqt', v: `${period.dailyStart} — ${period.dailyEnd}` },
                { k: 'Ish kunlari', v: workDaysLabel(period.workDays) },
                { k: 'Talab qilinadigan kunlar', v: `${period.requiredDays} kun` },
              ]}
            />
          ) : (
            <EmptyState title="Faol amaliyot davri yo'q" />
          )}
        </CardBody>
      </Card>
    </>
  );
}
