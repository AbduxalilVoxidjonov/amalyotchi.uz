import { useCallback, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Button, Pill, PillGroup } from '@/shared/ui';
import { tableState } from '../shared/useListParams';
import { useStudentFilters } from '../students/hooks';
import { messageHref } from './api';
import { describeFilter, hasAnyFilter, type ComposeTarget } from './audience';
import { ComposeMessageModal } from './components/ComposeMessageModal';
import styles from './components/Messages.module.css';
import { RecipientFiltersBar } from './components/RecipientFiltersBar';
import { RecipientsTable } from './components/RecipientsTable';
import { SentMessagesTable } from './components/SentMessagesTable';
import { useRecipientGroups, useRecipientsQuery, useSentMessagesQuery } from './hooks';
import type { MessageFlashState } from './MessageDetailPage';
import type { MessageRecipientRow, MessageSummary, RecipientFilter } from './types';
import { useRecipientListParams } from './useRecipientListParams';

const TABS = [
  { value: 'linked', label: 'Ulanganlar' },
  { value: 'sent', label: 'Yuborilganlar' },
] as const;

type MessagesTab = (typeof TABS)[number]['value'];

/** Tanlangan talabalar: id → qator (nomi "kimga" qatorida kerak bo'lishi mumkin). */
type Selection = ReadonlyMap<string, MessageRecipientRow>;

/**
 * Admin · Xabarlar (`/admin/messages`): ikki varaq — "Ulanganlar" (Telegram ulangan talabalar,
 * tanlash va xabar yozish) va "Yuborilganlar" (`?tab=sent`, yetkazish holati bilan).
 *
 * Tanlov sahifa holatida (varaqlar almashganda ham saqlanadi) va sahifalar/filtrlar o'zgarganda
 * TOZALANMAYDI: admin turli guruhlardan talabalarni filtr almashtirib yig'ishi mumkin, id bo'yicha
 * yuborilgani uchun yashirin qolgan tanlov ham to'g'ri. Son doim "Tanlanganlarga yozish (N)" da
 * ko'rinadi va "Tanlovni tozalash" tugmasi bor. Yuborilgach tanlov tozalanadi.
 */
export function MessagesPage() {
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const tab: MessagesTab = params.get('tab') === 'sent' ? 'sent' : 'linked';
  const [selection, setSelection] = useState<Selection>(() => new Map());
  const [target, setTarget] = useState<ComposeTarget | null>(null);

  function selectTab(next: MessagesTab) {
    setParams(
      (prev) => {
        const p = new URLSearchParams(prev);
        if (next === 'linked') p.delete('tab');
        else p.set('tab', next);
        return p;
      },
      { replace: true },
    );
  }

  function handleSent(summary: MessageSummary) {
    setTarget(null);
    setSelection(new Map());
    const state: MessageFlashState = {
      flash: `Xabar navbatga qo'yildi — ${summary.total} ta talabaga yuboriladi.`,
    };
    navigate(messageHref(summary.id), { state });
  }

  return (
    <div className={styles.page}>
      <PillGroup role="tablist" aria-label="Xabarlar bo'limlari">
        {TABS.map((t) => (
          <Pill
            key={t.value}
            id={`messages-tab-${t.value}`}
            role="tab"
            shape="tab"
            active={tab === t.value}
            aria-controls={`messages-panel-${t.value}`}
            onClick={() => selectTab(t.value)}
          >
            {t.label}
          </Pill>
        ))}
      </PillGroup>
      <div id={`messages-panel-${tab}`} role="tabpanel" aria-labelledby={`messages-tab-${tab}`}>
        {tab === 'sent' ? (
          <SentPanel />
        ) : (
          <RecipientsPanel
            selection={selection}
            onSelectionChange={setSelection}
            onCompose={setTarget}
          />
        )}
      </div>
      {target && (
        <ComposeMessageModal target={target} onClose={() => setTarget(null)} onSent={handleSent} />
      )}
    </div>
  );
}

interface RecipientsPanelProps {
  selection: Selection;
  onSelectionChange: (updater: (prev: Selection) => Selection) => void;
  onCompose: (target: ComposeTarget) => void;
}

/** "Ulanganlar": qidiruv + bog'liq filtrlar (URL'da) + jadval + tanlov. */
function RecipientsPanel({ selection, onSelectionChange, onCompose }: RecipientsPanelProps) {
  const list = useRecipientListParams();
  const query = useRecipientsQuery(list.params);
  const filterOptions = useStudentFilters();
  const { filters, setFilters, hasFilters, clearFilters, params } = list;
  const groups = useRecipientGroups({
    ...(filters.facultyId ? { facultyId: filters.facultyId } : {}),
    ...(filters.directionId ? { directionId: filters.directionId } : {}),
    ...(filters.course ? { course: Number(filters.course) } : {}),
  });
  const rows = query.data?.items;

  /** Fakultet o'zgarsa: unga tegishli bo'lmagan yo'nalish va guruh tozalanadi. */
  function handleFacultyChange(facultyId: string) {
    const direction = filterOptions.data?.directions.find((d) => d.id === filters.directionId);
    const keepDirection = !facultyId || !direction || direction.facultyId === facultyId;
    setFilters(
      keepDirection ? { facultyId, groupId: '' } : { facultyId, directionId: '', groupId: '' },
    );
  }

  const toggleRow = useCallback(
    (row: MessageRecipientRow, checked: boolean) =>
      onSelectionChange((prev) => {
        const next = new Map(prev);
        if (checked) next.set(row.userId, row);
        else next.delete(row.userId);
        return next;
      }),
    [onSelectionChange],
  );

  const toggleAll = useCallback(
    (checked: boolean) =>
      onSelectionChange((prev) => {
        const next = new Map(prev);
        for (const row of rows ?? []) {
          if (checked) next.set(row.userId, row);
          else next.delete(row.userId);
        }
        return next;
      }),
    [rows, onSelectionChange],
  );

  function writeSelected() {
    const only = selection.size === 1 ? [...selection.values()][0] : undefined;
    onCompose(
      only
        ? { kind: 'single', recipient: only }
        : { kind: 'selected', userIds: [...selection.keys()] },
    );
  }

  function writeBulk() {
    const estimate = query.data?.total ?? 0;
    const { page: _page, pageSize: _pageSize, q, ...rest } = params;
    const filter: RecipientFilter = { ...rest, ...(q ? { q } : {}) };
    if (!hasAnyFilter(filter)) {
      onCompose({ kind: 'all', estimate });
      return;
    }
    onCompose({
      kind: 'filter',
      filter,
      description: describeFilter(filter, filterOptions.data, groups.data),
      estimate,
    });
  }

  return (
    <RecipientsTable
      {...tableState(list, query)}
      selectedIds={selection}
      onToggleRow={toggleRow}
      onToggleAll={toggleAll}
      onClearSelection={() => onSelectionChange(() => new Map())}
      onWriteOne={(recipient) => onCompose({ kind: 'single', recipient })}
      onWriteSelected={writeSelected}
      onWriteBulk={writeBulk}
      onPageSizeChange={list.setPageSize}
      filters={
        <RecipientFiltersBar
          options={filterOptions.data}
          isLoading={filterOptions.isPending}
          isError={filterOptions.isError}
          groups={groups.data}
          groupsLoading={groups.isPending}
          values={filters}
          onFacultyChange={handleFacultyChange}
          onDirectionChange={(directionId) => setFilters({ directionId, groupId: '' })}
          onCourseChange={(course) => setFilters({ course, groupId: '' })}
          onGroupChange={(groupId) => setFilters({ groupId })}
          hasFilters={hasFilters}
          onClear={clearFilters}
          total={query.isPending || query.isError ? undefined : query.data?.total}
        />
      }
      emptyDescription={
        hasFilters ? (
          <>
            Tanlangan filtrlar bo'yicha Telegram ulangan talaba topilmadi.
            <span className={styles.emptyAction}>
              <Button size="xs" onClick={clearFilters}>
                Filtrlarni tozalash
              </Button>
            </span>
          </>
        ) : list.search ? undefined : (
          "Talabalar Telegram bot orqali HEMIS ID bilan kirgach, shu ro'yxatda paydo bo'ladi."
        )
      }
    />
  );
}

const SENT_PAGE_SIZE = 20;

/** "Yuborilganlar": sahifa `?spage=` (ulanganlar sahifasi `page` bilan aralashmasin). */
function SentPanel() {
  const [params, setParams] = useSearchParams();
  const page = Math.max(1, Math.floor(Number(params.get('spage'))) || 1);
  const query = useSentMessagesQuery({ page, pageSize: SENT_PAGE_SIZE });

  return (
    <SentMessagesTable
      data={query.data}
      isLoading={query.isPending}
      error={query.error}
      onRetry={() => void query.refetch()}
      page={page}
      pageSize={SENT_PAGE_SIZE}
      onPageChange={(next) =>
        setParams(
          (prev) => {
            const p = new URLSearchParams(prev);
            if (next > 1) p.set('spage', String(next));
            else p.delete('spage');
            return p;
          },
          { replace: true },
        )
      }
    />
  );
}

export default MessagesPage;
