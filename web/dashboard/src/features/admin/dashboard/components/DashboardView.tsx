import { Link } from 'react-router-dom';
import { Card, CardHeader, EmptyState, ProgressBar, StatGrid, StatTile } from '@/shared/ui';
import { auditDetail, auditWho } from '../../audit/describe';
import { formatCount, formatShortDateTime } from '../../shared/format';
import { attendanceEmptyReason, buildStatCards, facultyPctText } from '../derive';
import type { AdminDashboard } from '../types';
import styles from './DashboardView.module.css';

export interface DashboardViewProps {
  data: AdminDashboard;
}

/** SPEC-SCREENS §7 — stat grid (dot yo'q) + fakultetlar kesimida davomat + audit oxirgi yozuvlar. */
export function DashboardView({ data }: DashboardViewProps) {
  const cards = buildStatCards(data.stats);
  const attendanceEmpty = attendanceEmptyReason(data.stats);
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

      <Card aria-labelledby="dash-faculties">
        <CardHeader title={<span id="dash-faculties">Fakultetlar kesimida davomat</span>} />
        {attendanceEmpty && <p className={styles.cardNote}>{attendanceEmpty}</p>}
        {data.faculties.length === 0 ? (
          <EmptyState tone="plain" title="Fakultetlar yo'q" />
        ) : (
          <ul className={styles.list}>
            {data.faculties.map((f) => (
              <li key={f.id} className={styles.facultyRow}>
                <div className={styles.facultyMain}>
                  <div className={styles.facultyName}>{f.name}</div>
                  <ProgressBar
                    className={styles.facultyBar}
                    value={f.expectedToday > 0 ? f.attendancePct : 0}
                    showValue={false}
                    label={`${f.name} davomati`}
                  />
                </div>
                <div className={styles.right}>
                  <div className={styles.facultyPct}>{facultyPctText(f)}</div>
                  <div className={styles.faint}>{formatCount(f.studentCount)} talaba</div>
                </div>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card aria-labelledby="dash-audit">
        <CardHeader
          title={<span id="dash-audit">Audit jurnali — oxirgi qo'lda aralashuvlar</span>}
          actions={
            <Link to="/admin/audit" className={styles.link}>
              Hammasi
            </Link>
          }
        />
        {data.audit.length === 0 ? (
          <EmptyState tone="plain" title="Hozircha yozuv yo'q" />
        ) : (
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
        )}
      </Card>
    </div>
  );
}
