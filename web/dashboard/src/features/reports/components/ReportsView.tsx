import { Button, Card, Chip } from '@/shared/ui';
import { fmtDateRange } from '@/features/tutor/format';
import { formatLabel, type ReportsCatalog } from '../types';
import styles from './ReportsView.module.css';

export interface ReportsViewProps {
  data: ReportsCatalog;
  onDownload: (reportId: string) => void;
}

/** SPEC-SCREENS §12 — filtr kartasi (readonly qiymatlar) + hisobot kartalari. */
export function ReportsView({ data, onDownload }: ReportsViewProps) {
  return (
    <div className={styles.root}>
      <Card padded className={styles.filters} aria-label="Hisobot filtrlari">
        <div>
          <div className={styles.filterLabel}>Sana oraligi</div>
          <div className={styles.filterValue} data-mono>
            {fmtDateRange(data.filter.dateFrom, data.filter.dateTo)}
          </div>
        </div>
        <div>
          <div className={styles.filterLabel}>Ko'lam</div>
          <div className={styles.filterValue}>
            {data.filter.scope} · {data.filter.studentCount} talaba
          </div>
        </div>
      </Card>

      <div className={styles.grid}>
        {data.reports.map((r) => (
          <Card key={r.id} as="article" className={styles.report} aria-label={r.name}>
            <div className={styles.reportHead}>
              <div className={styles.reportName}>{r.name}</div>
              <Chip variant="fmt">{formatLabel(r.formats)}</Chip>
            </div>
            <div className={styles.reportDesc}>{r.desc}</div>
            <Button
              size="sm"
              className={styles.download}
              disabled={!r.available}
              title={!r.available && r.note ? r.note : undefined}
              onClick={() => onDownload(r.id)}
            >
              Yuklab olish
            </Button>
          </Card>
        ))}
      </div>
    </div>
  );
}
