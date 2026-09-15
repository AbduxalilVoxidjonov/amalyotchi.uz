import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { errorMessage } from '@/shared/api';
import { Button, Checkbox, EmptyState, Input, Modal } from '@/shared/ui';
import { ErrorState, LoadingState } from '../../components/PageStatus';
import { useAvailableGroupsQuery, useSetTutorGroups } from '../hooks';
import type { AvailableGroup } from '../types';
import styles from './GroupsPickerModal.module.css';
import formStyles from './TutorForms.module.css';

export interface GroupsPickerModalProps {
  open: boolean;
  tutorId: string;
  /** Hozir biriktirilgan guruhlar (`TutorDetail.groups[].groupId`) — boshlang'ich belgilanganlar. */
  currentGroupIds: readonly string[];
  onClose: () => void;
  /** 200 dan keyin (sahifa "Guruhlar saqlandi." xabarini ko'rsatadi). */
  onSaved?: () => void;
}

interface Section {
  /** "Kafedra › Yo'nalish" — legend matni. */
  key: string;
  departmentName: string;
  directionName: string;
  groups: AvailableGroup[];
}

/** Kafedra › yo'nalish bo'yicha guruhlash (kelish tartibi saqlanadi). */
function groupBySection(groups: readonly AvailableGroup[]): Section[] {
  const map = new Map<string, Section>();
  for (const g of groups) {
    const key = `${g.departmentName} › ${g.directionName}`;
    let section = map.get(key);
    if (!section) {
      section = {
        key,
        departmentName: g.departmentName,
        directionName: g.directionName,
        groups: [],
      };
      map.set(key, section);
    }
    section.groups.push(g);
  }
  return [...map.values()];
}

function matches(g: AvailableGroup, q: string): boolean {
  if (!q) return true;
  return [g.name, g.directionName, g.departmentName, g.tutorName].some((s) =>
    s?.toLowerCase().includes(q),
  );
}

/**
 * "Guruhlarni tahrirlash" — tyutor fakultetidagi faol guruhlar checkbox bilan; boshqa tyutorga
 * biriktirilganlar disabled (+ "— <FISH>"). Saqlash → `PUT /tutors/{id}/groups` (to'plam almashadi).
 */
export function GroupsPickerModal({
  open,
  tutorId,
  currentGroupIds,
  onClose,
  onSaved,
}: GroupsPickerModalProps) {
  const query = useAvailableGroupsQuery(tutorId, open);
  const mutation = useSetTutorGroups();
  const [selected, setSelected] = useState<ReadonlySet<string>>(() => new Set());
  const [search, setSearch] = useState('');

  // Har ochilishda joriy biriktirmalardan boshlash.
  useEffect(() => {
    if (!open) return;
    setSelected(new Set(currentGroupIds));
    setSearch('');
    mutation.reset();
    // `currentGroupIds` har render yangi massiv bo'lishi mumkin — faqat ochilishda o'qiladi.
  }, [open, tutorId]);

  const q = search.trim().toLowerCase();
  const sections = useMemo(() => {
    const all = query.data ?? [];
    return groupBySection(all.filter((g) => matches(g, q)));
  }, [query.data, q]);

  function toggle(id: string, checked: boolean) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (checked) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  function handleClose() {
    if (mutation.isPending) return;
    onClose();
  }

  function handleSave() {
    mutation.mutate(
      { id: tutorId, groupIds: [...selected] },
      {
        onSuccess: () => {
          onSaved?.();
          onClose();
        },
      },
    );
  }

  const hasGroups = (query.data?.length ?? 0) > 0;

  let body: ReactNode;
  if (query.isPending) {
    body = <LoadingState />;
  } else if (query.isError) {
    body = <ErrorState inline error={query.error} onRetry={() => void query.refetch()} />;
  } else if (!hasGroups) {
    body = <EmptyState tone="plain" title="Fakultetda faol guruhlar yo'q" />;
  } else if (sections.length === 0) {
    body = (
      <EmptyState
        tone="plain"
        title="Hech narsa topilmadi"
        description="Qidiruv bo'yicha guruh yo'q."
      />
    );
  } else {
    body = (
      <div className={styles.sections}>
        {sections.map((section) => (
          <fieldset key={section.key} className={styles.section}>
            <legend className={styles.legend}>{section.key}</legend>
            <ul className={styles.list}>
              {section.groups.map((g) => {
                const takenByOther = g.tutorId !== null && g.tutorId !== tutorId;
                return (
                  <li key={g.id} className={styles.item} data-taken={takenByOther || undefined}>
                    <Checkbox
                      wrapperClassName={styles.check}
                      checked={selected.has(g.id)}
                      disabled={takenByOther || mutation.isPending}
                      onChange={(e) => toggle(g.id, e.target.checked)}
                      label={
                        <>
                          <span className={styles.name}>{g.name}</span>
                          <span className={styles.meta}>
                            {g.course}-kurs · {g.students} talaba
                          </span>
                          {takenByOther && <span className={styles.taken}>— {g.tutorName}</span>}
                        </>
                      }
                    />
                  </li>
                );
              })}
            </ul>
          </fieldset>
        ))}
      </div>
    );
  }

  return (
    <Modal
      open={open}
      onClose={handleClose}
      title="Guruhlarni tahrirlash"
      description="Tyutor fakultetidagi faol guruhlar. Boshqa tyutorga biriktirilganlarni tanlab bo'lmaydi."
      width="560px"
      footer={
        <>
          <span className={styles.count} aria-live="polite">
            Tanlangan: {selected.size}
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
          placeholder="Guruh, yo'nalish yoki kafedra…"
          aria-label="Guruh qidirish"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          disabled={!hasGroups}
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
