import { useEffect, useId, useMemo, useState, type ReactNode } from 'react';
import { errorMessage } from '@/shared/api';
import { Button, Checkbox, EmptyState, Input, Modal } from '@/shared/ui';
import { ErrorState, LoadingState } from '../../components/PageStatus';
import { useSetTutorScopes, useTutorScopeTreeQuery } from '../hooks';
import { SCOPE_LEVEL_LABEL, type TutorScope } from '../types';
import {
  computeNodeStates,
  descendantKeys,
  flattenScopeTree,
  scopeInputOf,
  scopeKeysOf,
  visibleKeys,
  type ScopeNode,
  type ScopeNodeState,
} from './scopeTree';
import styles from './ScopePickerModal.module.css';
import formStyles from './TutorForms.module.css';

export interface ScopePickerModalProps {
  open: boolean;
  tutorId: string;
  /** Hozirgi ko'lamlar (`TutorDetail.scopes`) — boshlang'ich tanlov. */
  currentScopes: readonly TutorScope[];
  onClose: () => void;
  /** 200 dan keyin (sahifa "Ko'lam saqlandi." xabarini ko'rsatadi). */
  onSaved?: () => void;
}

/** Tugun holatiga qarab checkbox yonidagi izoh. */
function noteOf(state: ScopeNodeState): string | null {
  switch (state.kind) {
    case 'covered':
      return '— ota orqali';
    case 'taken':
      return `— ${state.tutorName}`;
    case 'takenViaAncestor':
      return `— ${state.tutorName} orqali`;
    case 'takenInside':
      return `ichida ${state.tutorName} biriktirilgan`;
    default:
      return null;
  }
}

function metaOf(node: ScopeNode): string {
  if (node.level === 'group') return `${node.course}-kurs · ${node.students} talaba`;
  return `${node.groups} guruh · ${node.students} talaba`;
}

/**
 * "Guruhlarni biriktirish" — tyutor fakulteti daraxti (fakultet → kafedra → yo'nalish → guruh), har
 * tugunda checkbox. Tanlangan tugun avlodlarini qamrab oladi (ular tanlovdan chiqadi — normalizatsiya);
 * boshqa tyutor tuguni, uning avlodlari va ajdodlari tanlab bo'lmaydi (kesishuv).
 * Saqlash → `PUT /tutors/{id}/scopes` (to'plam almashadi).
 */
export function ScopePickerModal({
  open,
  tutorId,
  currentScopes,
  onClose,
  onSaved,
}: ScopePickerModalProps) {
  const query = useTutorScopeTreeQuery(tutorId, open);
  const mutation = useSetTutorScopes();
  const [selected, setSelected] = useState<ReadonlySet<string>>(() => new Set());
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(() => new Set());
  const [search, setSearch] = useState('');
  const uid = useId();

  // Har ochilishda joriy ko'lamlardan boshlash.
  useEffect(() => {
    if (!open) return;
    setSelected(scopeKeysOf(currentScopes));
    setCollapsed(new Set());
    setSearch('');
    mutation.reset();
    // `currentScopes` har render yangi massiv bo'lishi mumkin — faqat ochilishda o'qiladi.
  }, [open, tutorId]);

  const nodes = useMemo(() => (query.data ? flattenScopeTree(query.data) : []), [query.data]);
  const states = useMemo(
    () => computeNodeStates(nodes, selected, tutorId),
    [nodes, selected, tutorId],
  );
  const visible = useMemo(() => visibleKeys(nodes, search), [nodes, search]);
  const searching = visible !== null;

  const summary = useMemo(() => {
    let count = 0;
    let groups = 0;
    for (const n of nodes) {
      if (states.get(n.key)?.kind !== 'selected') continue;
      count += 1;
      groups += n.groups;
    }
    return { count, groups };
  }, [nodes, states]);

  function toggle(node: ScopeNode, checked: boolean) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (checked) {
        next.add(node.key);
        // Normalizatsiya: avlodlar ota orqali qamrab olinadi.
        for (const k of descendantKeys(nodes, node.key)) next.delete(k);
      } else {
        next.delete(node.key);
      }
      return next;
    });
  }

  function toggleCollapsed(key: string) {
    setCollapsed((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  function handleClose() {
    if (mutation.isPending) return;
    onClose();
  }

  function handleSave() {
    const scopes = nodes.filter((n) => states.get(n.key)?.kind === 'selected').map(scopeInputOf);
    mutation.mutate(
      { id: tutorId, scopes },
      {
        onSuccess: () => {
          onSaved?.();
          onClose();
        },
      },
    );
  }

  function renderNode(node: ScopeNode): ReactNode {
    if (visible && !visible.has(node.key)) return null;
    const state = states.get(node.key) ?? { kind: 'free' };
    const checked = state.kind === 'selected' || state.kind === 'covered';
    const disabled = mutation.isPending || (state.kind !== 'free' && state.kind !== 'selected');
    const note = noteOf(state);
    const hasChildren = node.childKeys.length > 0;
    // Qidiruvda hamma ko'rinadigan tugunlar ochiq.
    const expanded = hasChildren && (searching || !collapsed.has(node.key));
    const childNodes = node.childKeys.map((k) => nodes.find((n) => n.key === k)!);
    const noteId = note ? `${uid}-${node.key}` : undefined;

    return (
      <li key={node.key} className={styles.node} data-level={node.level} data-state={state.kind}>
        <div className={styles.row}>
          {hasChildren ? (
            <button
              type="button"
              className={styles.toggle}
              aria-label={`${node.name} — ${expanded ? 'yopish' : 'ochish'}`}
              aria-expanded={expanded}
              disabled={searching}
              onClick={() => toggleCollapsed(node.key)}
            >
              {expanded ? '▾' : '▸'}
            </button>
          ) : (
            <span className={styles.toggleSpacer} aria-hidden="true" />
          )}
          <Checkbox
            wrapperClassName={styles.check}
            checked={checked}
            disabled={disabled}
            aria-label={`${node.name} (${SCOPE_LEVEL_LABEL[node.level]})`}
            aria-describedby={noteId}
            onChange={(e) => toggle(node, e.target.checked)}
            label={
              <>
                <span className={styles.name}>{node.name}</span>
                <span className={styles.level}>{SCOPE_LEVEL_LABEL[node.level]}</span>
                <span className={styles.meta}>{metaOf(node)}</span>
                {note && (
                  <span id={noteId} className={styles.note}>
                    {note}
                  </span>
                )}
              </>
            }
          />
        </div>
        {expanded && <ul className={styles.children}>{childNodes.map(renderNode)}</ul>}
      </li>
    );
  }

  const root = nodes[0];

  let body: ReactNode;
  if (query.isPending) {
    body = <LoadingState />;
  } else if (query.isError) {
    body = <ErrorState inline error={query.error} onRetry={() => void query.refetch()} />;
  } else if (!root) {
    body = <EmptyState tone="plain" title="Fakultet daraxti bo'sh" />;
  } else if (visible && visible.size === 0) {
    body = (
      <EmptyState
        tone="plain"
        title="Hech narsa topilmadi"
        description="Qidiruv bo'yicha tugun yo'q."
      />
    );
  } else {
    body = <ul className={styles.tree}>{renderNode(root)}</ul>;
  }

  return (
    <Modal
      open={open}
      onClose={handleClose}
      title="Guruhlarni biriktirish"
      description="Fakultet, kafedra, yo'nalish yoki guruhni tanlang — ota tanlansa ichidagi hamma narsa qamrab olinadi. Boshqa tyutor ko'lami bilan kesishib bo'lmaydi."
      width="620px"
      footer={
        <>
          <span className={styles.count} aria-live="polite">
            Tanlangan: {summary.count} ta ko'lam · ~{summary.groups} guruh
          </span>
          <Button type="button" onClick={handleClose} disabled={mutation.isPending}>
            Bekor qilish
          </Button>
          <Button
            type="button"
            variant="primary"
            onClick={handleSave}
            disabled={mutation.isPending || !query.isSuccess}
          >
            {mutation.isPending ? 'Saqlanmoqda…' : 'Saqlash'}
          </Button>
        </>
      }
    >
      <div className={styles.body}>
        <Input
          variant="search"
          type="search"
          placeholder="Kafedra, yo'nalish yoki guruh…"
          aria-label="Tugun qidirish"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          disabled={!root}
        />
        <div className={styles.scroll}>{body}</div>
        {mutation.isError && (
          <p role="alert" className={formStyles.error}>
            {errorMessage(mutation.error)}
          </p>
        )}
      </div>
    </Modal>
  );
}
