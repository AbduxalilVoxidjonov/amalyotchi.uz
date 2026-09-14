import { Link } from 'react-router-dom';
import { Card, CardHeader, ProgressBar, StatGrid, StatTile } from '@/shared/ui';
import { auditDetail, auditWho } from '../../audit/describe';
import {
  formatCount,
  formatHours,
  formatPct,
  formatScope,
  formatShortDateTime,
} from '../../shared/format';
import { buildStatCards, pendingKind } from '../derive';
import type { AdminDashboard } from '../types';
import styles from './DashboardView.module.css';

export interface DashboardViewProps {
  data: AdminDashboard;
}

/** SPEC-SCREENS §7 — stat grid (dot yo'q) + fakultet/tyutor kartalar + audit oxirgi yozuvlar. */
export function DashboardView({ data }: DashboardViewProps) {
  const cards = buildStatCards(data.stats);
  return (
    <div className={styles.root}>
      <StatGrid min={180}>
        {cards.map((s) => (
          <StatTile
            key={s.label}
            label={s.label}
            value={s.value}
            note={s.note}
            {...(s.tone && { noteTone: s.tone })}
          />
        ))}
      </StatGrid>

      <div className={styles.twoCol}>
        <Card aria-labelledby="dash-faculties">
          <CardHeader title={<span id="dash-faculties">Fakultetlar kesimida davomat</span>} />
          <ul className={styles.list}>
            {data.faculties.map((f) => (
              <li key={f.id} className={styles.facultyRow}>
                <div className={styles.facultyMain}>
                  <div className={styles.facultyName}>{f.name}</div>
                  <ProgressBar
                    className={styles.facultyBar}
                    value={f.attendancePct}
                    showValue={false}
                    label={`${f.name} davomati`}
                  />
                </div>
                <div className={styles.right}>
                  <div className={styles.facultyPct}>{formatPct(f.attendancePct)}</div>
                  <div className={styles.faint}>{formatCount(f.studentCount)} talaba</div>
                </div>
              </li>
            ))}
          </ul>
        </Card>

        <Card aria-labelledby="dash-tutors">
          <CardHeader title={<span id="dash-tutors">Tyutorlar faolligi</span>} />
          <ul className={styles.list}>
            {data.tutors.map((t) => (
              <li key={t.id} className={styles.tutorRow}>
                <div className={styles.tutorMain}>
                  <div className={styles.tutorName}>{t.name}</div>
                  <div className={styles.tutorScope}>{formatScope(t.facultyCode, t.groups)}</div>
                </div>
                <div className={styles.right} data-nowrap>
                  <div className={styles.tutorPending} data-kind={pendingKind(t)}>
                    {t.pendingCount} ariza
                  </div>
                  <div className={styles.faint}>
                    {t.avgDecisionHours == null
                      ? "qaror yo'q"
                      : `o'rtacha ${formatHours(t.avgDecisionHours)}`}
                  </div>
                </div>
              </li>
            ))}
          </ul>
        </Card>
      </div>

      <Card aria-labelledby="dash-audit">
        <CardHeader
          title={<span id="dash-audit">Audit jurnali — oxirgi qo'lda aralashuvlar</span>}
          actions={
            <Link to="/admin/audit" className={styles.link}>
              Hammasi
            </Link>
          }
        />
        <ul className={styles.list}>
          {data.audit.map((a) => (
            <li key={a.id} className={styles.auditRow}>
              <time className={styles.auditTime} dateTime={a.at}>
                {formatShortDateTime(a.at)}
              </time>
              <div className={styles.auditText}>{auditDetail(a)}</div>
              <div className={styles.auditWho}>{auditWho(a)}</div>
            </li>
          ))}
        </ul>
      </Card>
    </div>
  );
}
