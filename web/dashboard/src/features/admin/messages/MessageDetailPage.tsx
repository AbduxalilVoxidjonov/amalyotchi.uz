import { useState } from 'react';
import { useLocation, useParams } from 'react-router-dom';
import { errorMessage } from '@/shared/api';
import { Button, ProgressBar, StatGrid, StatTile, type BreadcrumbItem } from '@/shared/ui';
import { LoadingState } from '../components/PageStatus';
import { HierarchyListPage } from '../faculties/components/HierarchyListPage';
import { formatCount, formatDateTime, formatPct } from '../shared/format';
import { useListParams, tableState } from '../shared/useListParams';
import { SENT_MESSAGES_HREF } from './api';
import { DeliveriesTable } from './components/DeliveriesTable';
import { MessageStatusBadge } from './components/MessageStatusBadge';
import styles from './components/Messages.module.css';
import { processedPct } from './format';
import { useDeliveriesQuery, useMessageQuery, useRetryMessage } from './hooks';
import { isMessageActive, type DeliveryStatus, type MessageSummary } from './types';

/** Yuborilgandan keyin tafsilotda bir martalik xabar (loyihada toast yo'q). */
export interface MessageFlashState {
  flash?: string;
}

function readFlash(state: unknown): string | null {
  if (state && typeof state === 'object' && 'flash' in state) {
    const flash = (state as MessageFlashState).flash;
    return typeof flash === 'string' ? flash : null;
  }
  return null;
}

const ROOT: BreadcrumbItem[] = [{ label: 'Xabarlar', to: SENT_MESSAGES_HREF }];

/**
 * Admin · Xabar tafsiloti (`/admin/messages/:messageId`): to'liq matn, statistika (jami, yetkazildi,
 * xato, bloklangan, navbatda), progress, yetkazishlar jadvali (holat filtri, qidiruv, sahifalash).
 * Xato bo'lsa — "Xatolarni qayta yuborish". Yuborish davom etayotganda har 3 s yangilanadi.
 */
export function MessageDetailPage() {
  const { messageId = '' } = useParams<{ messageId: string }>();
  const location = useLocation();
  const query = useMessageQuery(messageId);
  const message = query.data;
  const [flash, setFlash] = useState<string | null>(() => readFlash(location.state));

  return (
    <HierarchyListPage
      detailQuery={query}
      rootBreadcrumb={ROOT}
      buildBreadcrumb={(m) => [...ROOT, { label: formatDateTime(m.createdAt) }]}
      loadingBreadcrumb={[...ROOT, { label: '…' }]}
      notFoundTitle="Xabar topilmadi."
      backTo={SENT_MESSAGES_HREF}
      backLabel="Yuborilganlarga qaytish"
      pageTitle={() => 'Xabar'}
    >
      {!message ? (
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
          <MessageDetail message={message} />
        </div>
      )}
    </HierarchyListPage>
  );
}

function MessageDetail({ message }: { message: MessageSummary }) {
  const retry = useRetryMessage(message.id);
  const active = isMessageActive(message.status);
  const pct = processedPct(message);

  return (
    <>
      <div className={styles.head}>
        <div>
          <div className={styles.titleRow}>
            <h2 className={styles.title}>{message.audienceLabel}</h2>
            <MessageStatusBadge status={message.status} />
          </div>
          <p className={styles.meta}>
            {formatDateTime(message.createdAt)} · {message.createdByName}
          </p>
        </div>
        {message.failed > 0 && (
          <div className={styles.actions}>
            {active && <span className={styles.actionHint}>Yuborish tugagach mumkin</span>}
            <Button
              size="sm"
              variant="primary"
              disabled={active || retry.isPending}
              onClick={() => retry.mutate()}
            >
              {retry.isPending ? 'Navbatga qo‘yilmoqda…' : 'Xatolarni qayta yuborish'}
            </Button>
          </div>
        )}
      </div>
      {retry.isError && (
        <p role="alert" className={styles.retryError}>
          {errorMessage(retry.error)}
        </p>
      )}

      <section aria-label="Xabar matni">
        <p className={styles.textCard}>{message.text}</p>
        {message.attachAppButton && (
          <span className={styles.appNote}>Xabar ostida «Ilovani ochish» tugmasi bor.</span>
        )}
      </section>

      <StatGrid min={150} role="group" aria-label="Yetkazish statistikasi">
        <StatTile label="Jami" value={formatCount(message.total)} />
        <StatTile label="Yetkazildi" dot="ok" value={formatCount(message.sent)} />
        <StatTile label="Xato" dot="bad" value={formatCount(message.failed)} />
        <StatTile label="Bloklangan" dot="late" value={formatCount(message.blocked)} />
        <StatTile label="Navbatda" dot="neu" value={formatCount(message.pending)} />
      </StatGrid>

      <div className={styles.progressCard}>
        <div className={styles.progressHead}>
          <span>{active ? 'Yuborilmoqda…' : 'Yuborish yakunlangan'}</span>
          <span>{formatPct(Math.round(pct))}</span>
        </div>
        <ProgressBar
          value={pct}
          showValue={false}
          color="var(--color-accent)"
          label="Yuborish jarayoni"
        />
      </div>

      <Deliveries messageId={message.id} live={active} />
    </>
  );
}

function Deliveries({ messageId, live }: { messageId: string; live: boolean }) {
  const list = useListParams();
  const [status, setStatus] = useState<DeliveryStatus | ''>('');
  const query = useDeliveriesQuery(
    messageId,
    { ...list.params, ...(status ? { status } : {}) },
    live,
  );

  return (
    <DeliveriesTable
      {...tableState(list, query)}
      status={status}
      onStatusChange={(next) => {
        setStatus(next);
        list.setPage(1);
      }}
    />
  );
}

export default MessageDetailPage;
