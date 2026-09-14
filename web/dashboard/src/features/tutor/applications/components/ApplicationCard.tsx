import { Badge } from '@/shared/ui';
import { APPLICATION_STATUS_LABEL, applicationWaited, type ApplicationSummary } from '../types';
import styles from './ApplicationCard.module.css';

export interface ApplicationCardProps {
  app: ApplicationSummary;
  selected: boolean;
  onSelect: (id: string) => void;
}

/** SPEC-SCREENS §4 — ariza kartasi (button, tanlangan/tanlanmagan holat). */
export function ApplicationCard({ app, selected, onSelect }: ApplicationCardProps) {
  const s = APPLICATION_STATUS_LABEL[app.status];
  return (
    <button
      type="button"
      className={styles.card}
      aria-pressed={selected}
      aria-label={`${app.name} — ${app.company}`}
      onClick={() => onSelect(app.id)}
    >
      <span className={styles.top}>
        <span className={styles.name}>{app.name}</span>
        <span className={styles.group}>{app.group}</span>
      </span>
      <span className={styles.company}>{app.company}</span>
      <span className={styles.bottom}>
        <Badge status={s.kind} size="sm">
          {s.label}
        </Badge>
        <span className={styles.waited}>{applicationWaited(app)}</span>
      </span>
    </button>
  );
}
