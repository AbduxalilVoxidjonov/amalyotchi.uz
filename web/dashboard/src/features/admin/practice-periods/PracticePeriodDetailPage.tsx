import { useMemo, useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { errorMessage } from '@/shared/api';
import { Button, ConfirmDialog, EmptyState, type BreadcrumbItem } from '@/shared/ui';
import { ErrorState, LoadingState } from '../components/PageStatus';
import { HierarchyListPage } from '../faculties/components/HierarchyListPage';
import { AddGroupsModal } from './components/AddGroupsModal';
import styles from './components/PeriodDetail.module.css';
import { PeriodEditModal } from './components/PeriodEditModal';
import { PeriodGroupsTable, type GroupMetricsState } from './components/PeriodGroupsTable';
import { PeriodInfoCard } from './components/PeriodInfoCard';
import { PeriodMetrics, PeriodMetricsSkeleton } from './components/PeriodMetrics';
import { PeriodStatusBadge } from './components/PeriodStatusBadge';
import { formatDate } from './dates';
import {
  useClosePracticePeriod,
  useDeletePracticePeriod,
  usePracticePeriodQuery,
  usePracticePeriodStatsQuery,
  useSetPracticePeriodGroups,
} from './hooks';
import { PRACTICE_PERIODS_HREF } from './paths';
import type { PeriodFlashState } from './PracticePeriodCreatePage';
import type { PeriodGroupStats, PracticePeriodGroup } from './types';

const ROOT: BreadcrumbItem[] = [{ label: 'Amaliyot davrlari', to: PRACTICE_PERIODS_HREF }];

type Dialog =
  | { kind: 'edit' }
  | { kind: 'groups' }
  | { kind: 'close' }
  | { kind: 'delete' }
  | { kind: 'detach'; group: PracticePeriodGroup }
  | null;

function readFlash(state: unknown): string | null {
  if (state && typeof state === 'object' && 'flash' in state) {
    const flash = (state as PeriodFlashState).flash;
    return typeof flash === 'string' ? flash : null;
  }
  return null;
}

/**
 * Admin · Amaliyot davri (`/admin/practice-periods/:periodId`): sarlavha + amallar
 * (Tahrirlash / Yopish / O'chirish), ma'lumot kartasi, davr ko'rsatkichlari (KPI) va
 * biriktirilgan guruhlar (ko'rsatkich ustunlari bilan; guruh → davr ichidagi guruh sahifasi).
 * Yopilgan davrda tahrirlash amallari (tahrirlash, yopish, guruh qo'shish/ajratish) yashiriladi.
 * Rejalashtirilgan davrda statistika so'ralmaydi — "Davr hali boshlanmagan".
 */
export function PracticePeriodDetailPage() {
  const { periodId = '' } = useParams<{ periodId: string }>();
  const location = useLocation();
  const navigate = useNavigate();
  const query = usePracticePeriodQuery(periodId);
  const period = query.data;
  const planned = period?.status === 'planned';
  const statsQuery = usePracticePeriodStatsQuery(period && !planned ? periodId : '');
  const stats = statsQuery.data;
  const statsByGroup = useMemo(
    () => new Map<string, PeriodGroupStats>((stats?.groups ?? []).map((g) => [g.groupId, g])),
    [stats],
  );
  const metricsState: GroupMetricsState = planned
    ? 'hidden'
    : stats
      ? 'ready'
      : statsQuery.isError
        ? 'unavailable'
        : 'loading';

  const closePeriod = useClosePracticePeriod(periodId);
  const deletePeriod = useDeletePracticePeriod(periodId);
  const setGroups = useSetPracticePeriodGroups(periodId);

  const [dialog, setDialog] = useState<Dialog>(null);
  const [flash, setFlash] = useState<string | null>(() => readFlash(location.state));

  const editable = period ? period.status !== 'closed' : false;

  function open(next: Exclude<Dialog, null>) {
    closePeriod.reset();
    deletePeriod.reset();
    setGroups.reset();
    setDialog(next);
  }

  const dismiss = () => setDialog(null);

  return (
    <HierarchyListPage
      detailQuery={query}
      rootBreadcrumb={ROOT}
      buildBreadcrumb={(p) => [...ROOT, { label: p.name }]}
      loadingBreadcrumb={[...ROOT, { label: '…' }]}
      notFoundTitle="Amaliyot davri topilmadi."
      backTo={PRACTICE_PERIODS_HREF}
      backLabel="Amaliyot davrlariga qaytish"
      pageTitle={(p) => p.name}
    >
      {!period ? (
        <LoadingState />
      ) : (
        <div className={styles.stack}>
          {flash && (
            <div className={styles.flash} role="status">
              <span>{flash}</span>
              <button
                type="button"
                className={styles.flashClose}
                aria-label="Xabarni yopish"
                onClick={() => setFlash(null)}
              >
                ×
              </button>
            </div>
          )}

          <div className={styles.head}>
            <div className={styles.titleRow}>
              <h2 className={styles.title}>{period.name}</h2>
              <PeriodStatusBadge status={period.status} />
            </div>
            <div className={styles.actions}>
              {editable && (
                <>
                  <Button size="sm" onClick={() => open({ kind: 'edit' })}>
                    Tahrirlash
                  </Button>
                  <Button size="sm" onClick={() => open({ kind: 'close' })}>
                    Yopish
                  </Button>
                </>
              )}
              <Button size="sm" variant="danger" onClick={() => open({ kind: 'delete' })}>
                O'chirish
              </Button>
            </div>
          </div>

          <PeriodInfoCard period={period} />

          {planned ? (
            <EmptyState
              title="Davr hali boshlanmagan"
              description={`Davomat, kundalik va baholar ko'rsatkichlari ${formatDate(period.startDate)} dan boshlab to'planadi.`}
            />
          ) : stats ? (
            <PeriodMetrics
              metrics={stats.totals}
              elapsedWorkDays={stats.elapsedWorkDays}
              requiredDays={stats.requiredDays}
            />
          ) : statsQuery.isError ? (
            <ErrorState error={statsQuery.error} onRetry={() => void statsQuery.refetch()} />
          ) : (
            <PeriodMetricsSkeleton />
          )}

          <PeriodGroupsTable
            periodId={period.id}
            stats={statsByGroup}
            metricsState={metricsState}
            groups={period.groups}
            editable={editable}
            onAdd={() => open({ kind: 'groups' })}
            onDetach={(group) => open({ kind: 'detach', group })}
          />

          {dialog?.kind === 'edit' && <PeriodEditModal period={period} onClose={dismiss} />}
          {dialog?.kind === 'groups' && <AddGroupsModal period={period} onClose={dismiss} />}

          <ConfirmDialog
            open={dialog?.kind === 'close'}
            title="Davrni yopish"
            confirmLabel="Ha, yopish"
            description={`«${period.name}» davrini yopasizmi? Yopilgan davrni tahrirlab va guruhlarini o'zgartirib bo'lmaydi.`}
            isLoading={closePeriod.isPending}
            error={closePeriod.isError ? errorMessage(closePeriod.error) : undefined}
            onCancel={dismiss}
            onConfirm={() => closePeriod.mutate(undefined, { onSuccess: dismiss })}
          />

          <ConfirmDialog
            open={dialog?.kind === 'delete'}
            title="Davrni o'chirish"
            confirmLabel="O'chirish"
            danger
            description={`«${period.name}» davrini o'chirasizmi? Bu amalni qaytarib bo'lmaydi.`}
            isLoading={deletePeriod.isPending}
            error={deletePeriod.isError ? errorMessage(deletePeriod.error) : undefined}
            onCancel={dismiss}
            onConfirm={() =>
              deletePeriod.mutate(undefined, {
                onSuccess: () => navigate(PRACTICE_PERIODS_HREF, { replace: true }),
              })
            }
          />

          <ConfirmDialog
            open={dialog?.kind === 'detach'}
            title="Guruhni ajratish"
            confirmLabel="Ajratish"
            danger
            description={
              dialog?.kind === 'detach'
                ? `${dialog.group.code} guruhini «${period.name}» davridan ajratasizmi?`
                : undefined
            }
            isLoading={setGroups.isPending}
            error={setGroups.isError ? errorMessage(setGroups.error) : undefined}
            onCancel={dismiss}
            onConfirm={() => {
              if (dialog?.kind !== 'detach') return;
              const removeId = dialog.group.id;
              setGroups.mutate(
                period.groups.filter((g) => g.id !== removeId).map((g) => g.id),
                { onSuccess: dismiss },
              );
            }}
          />
        </div>
      )}
    </HierarchyListPage>
  );
}

export default PracticePeriodDetailPage;
