import { useCallback, useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Button, EmptyState, Pill, PillGroup } from '@/shared/ui';
import { ErrorState, LoadingState, QueryState } from '../components/QueryState';
import { mutationErrorMessage } from '../errors';
import { ApplicationCard } from './components/ApplicationCard';
import { ApplicationDetailPanel } from './components/ApplicationDetailPanel';
import { useApplicationDecision, useApplicationDetailQuery, useApplicationsQuery } from './hooks';
import {
  APPLICATION_TABS,
  isApplicationTab,
  type ApplicationDecision,
  type ApplicationDecisionRequest,
  type ApplicationDetail,
  type ApplicationTab,
} from './types';
import styles from './ApplicationsPage.module.css';

/** ≤900px — master-detail bitta ustun (ApplicationsPage.module.css bilan bir xil chegara). */
const NARROW_MQ = '(max-width: 900px)';
const isNarrow = () =>
  typeof window !== 'undefined' &&
  typeof window.matchMedia === 'function' &&
  window.matchMedia(NARROW_MQ).matches;
const scrollTo = (el: HTMLElement | null) =>
  el?.scrollIntoView?.({ behavior: 'smooth', block: 'start' });

/** Tyutor · Arizalar (SPEC-SCREENS §4) — master-detail container. URL: `?tab=` · `?id=`. */
export function ApplicationsPage() {
  const [params, setParams] = useSearchParams();
  const tab: ApplicationTab = isApplicationTab(params.get('tab'))
    ? (params.get('tab') as ApplicationTab)
    : 'submitted';
  const idParam = params.get('id');

  const list = useApplicationsQuery({ tab });
  const items = list.data?.items ?? [];
  // ?id= ro'yxatda bo'lmasa (yoki yo'q bo'lsa) — birinchi karta (dizayn: sel=0).
  const selectedId = items.some((a) => a.id === idParam) ? idParam : (items[0]?.id ?? null);

  // Tor ekranda karta bosilganda detail pastda — unga scroll qilamiz (faqat foydalanuvchi tanlovida).
  // Detail yuklanib bo'lgach (Loading → panel almashuvi smooth scroll'ni uzib qo'yadi).
  const listRef = useRef<HTMLDivElement>(null);
  const detailRef = useRef<HTMLDivElement>(null);
  const scrollPending = useRef(false);
  const onDetailReady = useCallback(() => {
    if (!scrollPending.current) return;
    scrollPending.current = false;
    if (isNarrow()) scrollTo(detailRef.current);
  }, []);

  const update = (patch: { tab?: ApplicationTab; id?: string | null }) => {
    const sp = new URLSearchParams(params);
    if (patch.tab !== undefined) {
      if (patch.tab === 'submitted') sp.delete('tab');
      else sp.set('tab', patch.tab);
      sp.delete('id');
    }
    if (patch.id !== undefined) {
      if (patch.id === null) sp.delete('id');
      else sp.set('id', patch.id);
    }
    setParams(sp);
  };

  return (
    <div className={styles.page}>
      <PillGroup role="tablist" aria-label="Ariza holati">
        {APPLICATION_TABS.map((t) => (
          <Pill
            key={t.value}
            role="tab"
            shape="tab"
            active={tab === t.value}
            count={list.data?.counts[t.value] ?? ''}
            onClick={() => update({ tab: t.value })}
          >
            {t.label}
          </Pill>
        ))}
      </PillGroup>

      <QueryState
        status={list.status}
        data={list.data}
        error={list.error}
        refetch={list.refetch}
        isEmpty={(d) => d.items.length === 0}
        empty={
          <EmptyState
            title="Bu bo'limda arizalar yo'q"
            description="Yangi ariza kelganda shu yerda ko'rinadi."
          />
        }
      >
        {(data) => (
          <div className={styles.grid}>
            <div ref={listRef} className={styles.list} role="list" aria-label="Arizalar ro'yxati">
              {data.items.map((a) => (
                <div role="listitem" key={a.id}>
                  <ApplicationCard
                    app={a}
                    selected={a.id === selectedId}
                    onSelect={(id) => {
                      scrollPending.current = true;
                      update({ id });
                    }}
                  />
                </div>
              ))}
            </div>
            {selectedId && (
              <div ref={detailRef} className={styles.detail}>
                <Button size="xs" className={styles.back} onClick={() => scrollTo(listRef.current)}>
                  ← Ro'yxatga
                </Button>
                <ApplicationDetailContainer
                  id={selectedId}
                  onReady={onDetailReady}
                  onDecided={() => update({ id: null })}
                />
              </div>
            )}
          </div>
        )}
      </QueryState>
    </div>
  );
}

function ApplicationDetailContainer({
  id,
  onReady,
  onDecided,
}: {
  id: string;
  /** Detail ma'lumoti render bo'lgach (har `id` uchun bir marta). */
  onReady: () => void;
  onDecided: () => void;
}) {
  const detail = useApplicationDetailQuery(id);
  const ready = detail.status === 'success';
  useEffect(() => {
    if (ready) onReady();
  }, [id, ready, onReady]);
  if (detail.status === 'pending') return <LoadingState label="Ariza yuklanmoqda…" />;
  if (detail.status === 'error')
    return <ErrorState error={detail.error} onRetry={() => void detail.refetch()} />;
  // key=id — talaba almashganda radius/checklist lokal holati tozalanadi.
  return <ApplicationDetailForm key={id} detail={detail.data} onDecided={onDecided} />;
}

function ApplicationDetailForm({
  detail,
  onDecided,
}: {
  detail: ApplicationDetail;
  onDecided: () => void;
}) {
  const [radiusM, setRadiusM] = useState(detail.radiusM);
  const [checked, setChecked] = useState<ReadonlySet<number>>(() => new Set(detail.checklist));
  const [comment, setComment] = useState('');
  const [localError, setLocalError] = useState<string | undefined>(undefined);
  const decision = useApplicationDecision();

  const toggle = (i: number) =>
    setChecked((prev) => {
      const next = new Set(prev);
      if (next.has(i)) next.delete(i);
      else next.add(i);
      return next;
    });

  // Backend: approve → radiusM (+checklist); return/reject → comment majburiy (400 `errors`).
  const decide = (d: ApplicationDecision) => {
    const trimmed = comment.trim();
    if (d !== 'approve' && !trimmed) {
      setLocalError('Qaytarish/rad etish sababi (izoh) majburiy.');
      return;
    }
    setLocalError(undefined);
    const body: ApplicationDecisionRequest =
      d === 'approve'
        ? {
            decision: d,
            radiusM,
            checklist: [...checked].sort((a, b) => a - b),
            ...(trimmed ? { comment: trimmed } : {}),
          }
        : { decision: d, comment: trimmed };
    decision.mutate({ id: detail.id, body }, { onSuccess: onDecided });
  };

  return (
    <ApplicationDetailPanel
      detail={detail}
      radiusM={radiusM}
      onRadiusChange={setRadiusM}
      checked={checked}
      onToggleCheck={toggle}
      comment={comment}
      onCommentChange={(v) => {
        setComment(v);
        if (localError) setLocalError(undefined);
      }}
      onDecision={decide}
      pending={decision.isPending}
      error={localError ?? (decision.isError ? mutationErrorMessage(decision.error) : undefined)}
    />
  );
}

export default ApplicationsPage;
