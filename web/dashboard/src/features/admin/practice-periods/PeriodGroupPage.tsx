import { useParams } from 'react-router-dom';
import { EmptyState, type BreadcrumbItem } from '@/shared/ui';
import { LoadingState } from '../components/PageStatus';
import { HierarchyListPage } from '../faculties/components/HierarchyListPage';
import styles from './components/PeriodGroup.module.css';
import { PeriodGroupStudentsTable } from './components/PeriodGroupStudentsTable';
import { PeriodMetrics } from './components/PeriodMetrics';
import { PeriodStatusBadge } from './components/PeriodStatusBadge';
import { formatDate, formatRange } from './dates';
import { usePeriodGroupStudentsQuery } from './hooks';
import { PRACTICE_PERIODS_HREF, periodHref } from './paths';

const ROOT: BreadcrumbItem[] = [{ label: 'Amaliyot davrlari', to: PRACTICE_PERIODS_HREF }];

/**
 * Admin · Davr ichidagi guruh (`/admin/practice-periods/:periodId/groups/:groupId`):
 * guruhning shu davrdagi ko'rsatkichlari (KPI) va talabalar jadvali (qidiruv + saralash).
 * Talaba qatori → admin talaba profili shu davr bilan (`?period=`).
 * 404 — davr yo'q yoki guruh shu davrga biriktirilmagan.
 */
export function PeriodGroupPage() {
  const { periodId = '', groupId = '' } = useParams<{ periodId: string; groupId: string }>();
  const query = usePeriodGroupStudentsQuery(periodId, groupId);
  const data = query.data;
  const started = data ? data.period.status !== 'planned' : false;

  return (
    <HierarchyListPage
      detailQuery={query}
      rootBreadcrumb={ROOT}
      buildBreadcrumb={(d) => [
        ...ROOT,
        { label: d.period.name, to: periodHref(d.period) },
        { label: d.group.code },
      ]}
      loadingBreadcrumb={[...ROOT, { label: '…' }]}
      notFoundTitle="Guruh bu amaliyot davrida topilmadi."
      backTo={periodId ? periodHref({ id: periodId }) : PRACTICE_PERIODS_HREF}
      backLabel="Amaliyot davriga qaytish"
      pageTitle={(d) => `${d.group.code} · ${d.period.name}`}
    >
      {!data ? (
        <LoadingState />
      ) : (
        <div className={styles.stack}>
          <div className={styles.head}>
            <div className={styles.titleRow}>
              <h2 className={styles.title}>{data.group.code} guruhi</h2>
              <PeriodStatusBadge status={data.period.status} />
            </div>
            <p className={styles.meta}>
              {data.group.course}-kurs · {data.group.facultyName} · {data.group.directionName} ·{' '}
              {data.period.name} (
              <span className={styles.mono}>
                {formatRange(data.period.startDate, data.period.endDate)}
              </span>
              )
            </p>
          </div>

          {started ? (
            <PeriodMetrics
              title="Guruh ko'rsatkichlari"
              metrics={data.metrics}
              elapsedWorkDays={data.elapsedWorkDays}
            />
          ) : (
            <EmptyState
              title="Davr hali boshlanmagan"
              description={`Davomat, kundalik va baholar ko'rsatkichlari ${formatDate(data.period.startDate)} dan boshlab to'planadi.`}
            />
          )}

          <PeriodGroupStudentsTable
            periodId={data.period.id}
            students={data.students}
            started={started}
          />
        </div>
      )}
    </HierarchyListPage>
  );
}

export default PeriodGroupPage;
