import { useId, useState } from 'react';
import { Button, Select, type SelectOption } from '@/shared/ui';
import { ErrorState } from '../../components/PageStatus';
import type { GroupRow } from '../../faculties/groups/types';
import { groupAvailability } from '../availability';
import {
  usePickerDepartments,
  usePickerDirections,
  usePickerFaculties,
  usePickerGroups,
} from '../hooks';
import type { PracticePeriodGroup } from '../types';
import styles from './GroupPicker.module.css';

export interface GroupPickerProps {
  /** Tanlangan guruhlar — bir nechta yo'nalish/fakultet bo'ylab to'planadi. */
  selected: readonly PracticePeriodGroup[];
  onChange: (next: PracticePeriodGroup[]) => void;
  /** Davr oralig'i — band guruhlarni aniqlash uchun (bo'sh bo'lsa faqat info ko'rsatiladi). */
  startDate: string;
  endDate: string;
  /** Tahrirlanayotgan davr — o'z guruhlari "band" hisoblanmaydi. */
  currentPeriodId?: string | undefined;
  disabled?: boolean;
}

function toOptions<T extends { id: string; name: string; isActive: boolean }>(
  items: readonly T[] | undefined,
): SelectOption[] {
  return (items ?? []).filter((x) => x.isActive).map((x) => ({ value: x.id, label: x.name }));
}

/** "Fakultet › Yo'nalish" bo'yicha guruhlash (tanlash tartibi saqlanadi). */
function groupBySection(groups: readonly PracticePeriodGroup[]) {
  const sections = new Map<string, { title: string; items: PracticePeriodGroup[] }>();
  for (const g of groups) {
    const key = `${g.facultyId}/${g.directionId}`;
    const section = sections.get(key) ?? {
      title: `${g.facultyName} › ${g.directionName}`,
      items: [],
    };
    section.items.push(g);
    sections.set(key, section);
  }
  return [...sections.entries()];
}

/**
 * Guruh tanlash: Fakultet → Kafedra → Yo'nalish kaskadi + yo'nalish guruhlari checkbox ro'yxati +
 * "Tanlangan guruhlar" paneli. Tanlov yo'nalish almashganda saqlanadi (holat — `selected` prop'da).
 * Yaratish sahifasi va "Guruh qo'shish" modali shu komponentni ishlatadi.
 */
export function GroupPicker({
  selected,
  onChange,
  startDate,
  endDate,
  currentPeriodId,
  disabled = false,
}: GroupPickerProps) {
  const baseId = useId();
  const [facultyId, setFacultyId] = useState('');
  const [departmentId, setDepartmentId] = useState('');
  const [directionId, setDirectionId] = useState('');

  const faculties = usePickerFaculties();
  const departments = usePickerDepartments(facultyId);
  const directions = usePickerDirections(departmentId);
  const groups = usePickerGroups(directionId);

  const faculty = faculties.data?.items.find((f) => f.id === facultyId);
  const department = departments.data?.items.find((d) => d.id === departmentId);
  const direction = directions.data?.items.find((d) => d.id === directionId);

  const selectedIds = new Set(selected.map((g) => g.id));
  const rows = groups.data?.items ?? [];
  const rowState = rows.map((row) => ({
    row,
    ...groupAvailability(row, startDate, endDate, currentPeriodId),
    checked: selectedIds.has(row.id),
  }));
  // Band guruh — agar allaqachon tanlangan bo'lsa ham olib tashlash mumkin bo'lishi kerak.
  const selectable = rowState.filter((r) => !r.busy || r.checked);
  const checkedCount = selectable.filter((r) => r.checked).length;
  const allChecked = selectable.length > 0 && checkedCount === selectable.length;
  const someChecked = checkedCount > 0 && !allChecked;

  function toPeriodGroup(row: GroupRow): PracticePeriodGroup | null {
    if (!faculty || !department || !direction) return null;
    return {
      id: row.id,
      code: row.code,
      course: row.course,
      studentsCount: row.students,
      facultyId: faculty.id,
      facultyName: faculty.name,
      departmentId: department.id,
      departmentName: department.name,
      directionId: direction.id,
      directionName: direction.name,
    };
  }

  function toggle(row: GroupRow) {
    if (selectedIds.has(row.id)) {
      onChange(selected.filter((g) => g.id !== row.id));
      return;
    }
    const g = toPeriodGroup(row);
    if (g) onChange([...selected, g]);
  }

  function toggleAll() {
    if (allChecked) {
      const ids = new Set(selectable.map((r) => r.row.id));
      onChange(selected.filter((g) => !ids.has(g.id)));
      return;
    }
    const additions = selectable
      .filter((r) => !r.checked && !r.busy)
      .map((r) => toPeriodGroup(r.row))
      .filter((g): g is PracticePeriodGroup => g !== null);
    onChange([...selected, ...additions]);
  }

  const totalStudents = selected.reduce((s, g) => s + g.studentsCount, 0);

  return (
    <div className={styles.picker}>
      <div className={styles.cascade}>
        <Select
          label="Fakultet"
          variant="form"
          value={facultyId}
          placeholder={faculties.isPending ? 'Yuklanmoqda…' : 'Fakultetni tanlang'}
          options={toOptions(faculties.data?.items)}
          disabled={disabled || faculties.isPending}
          onChange={(e) => {
            setFacultyId(e.target.value);
            setDepartmentId('');
            setDirectionId('');
          }}
        />
        <Select
          label="Kafedra"
          variant="form"
          value={departmentId}
          placeholder={facultyId && departments.isPending ? 'Yuklanmoqda…' : 'Kafedrani tanlang'}
          options={toOptions(departments.data?.items)}
          disabled={disabled || !facultyId || departments.isPending}
          onChange={(e) => {
            setDepartmentId(e.target.value);
            setDirectionId('');
          }}
        />
        <Select
          label="Yo'nalish"
          variant="form"
          value={directionId}
          placeholder={
            departmentId && directions.isPending ? 'Yuklanmoqda…' : "Yo'nalishni tanlang"
          }
          options={toOptions(directions.data?.items)}
          disabled={disabled || !departmentId || directions.isPending}
          onChange={(e) => setDirectionId(e.target.value)}
        />
      </div>

      {faculties.isError && (
        <ErrorState inline error={faculties.error} onRetry={() => void faculties.refetch()} />
      )}

      {!directionId ? (
        <p className={styles.placeholder}>
          Guruhlarni ko‘rish uchun fakultet, kafedra va yo‘nalishni tanlang.
        </p>
      ) : groups.isPending ? (
        <p className={styles.placeholder} role="status">
          Yuklanmoqda…
        </p>
      ) : groups.isError ? (
        <ErrorState inline error={groups.error} onRetry={() => void groups.refetch()} />
      ) : rows.length === 0 ? (
        <p className={styles.placeholder}>Bu yo‘nalishda guruh yo‘q.</p>
      ) : (
        <fieldset className={styles.list} disabled={disabled}>
          <legend className={styles.legend}>{direction?.name ?? "Yo'nalish"} guruhlari</legend>
          <label className={styles.allRow}>
            <input
              type="checkbox"
              className={styles.check}
              checked={allChecked}
              ref={(el) => {
                if (el) el.indeterminate = someChecked;
              }}
              disabled={selectable.length === 0}
              onChange={toggleAll}
            />
            <span>Hammasini tanlash</span>
          </label>
          <ul className={styles.rows}>
            {rowState.map(({ row, busy, note, checked }) => {
              const noteId = `${baseId}-${row.id}-note`;
              return (
                <li key={row.id} className={styles.item} data-busy={busy || undefined}>
                  <label className={styles.rowLabel}>
                    <input
                      type="checkbox"
                      className={styles.check}
                      checked={checked}
                      disabled={busy && !checked}
                      aria-describedby={note ? noteId : undefined}
                      onChange={() => toggle(row)}
                    />
                    <span className={styles.code}>{row.code}</span>
                    <span className={styles.meta}>{row.course}-kurs</span>
                    <span className={styles.meta}>{row.students} talaba</span>
                  </label>
                  {note && (
                    <span id={noteId} className={styles.note} data-tone={busy ? 'bad' : 'info'}>
                      {note}
                    </span>
                  )}
                </li>
              );
            })}
          </ul>
        </fieldset>
      )}

      <section className={styles.selected} aria-label="Tanlangan guruhlar">
        <div className={styles.selectedHead}>
          <h3 className={styles.selectedTitle}>
            Tanlangan guruhlar ({selected.length})
            {selected.length > 0 && (
              <span className={styles.selectedSub}> · {totalStudents} talaba</span>
            )}
          </h3>
          <Button
            type="button"
            size="xs"
            disabled={disabled || selected.length === 0}
            onClick={() => onChange([])}
          >
            Hammasini tozalash
          </Button>
        </div>
        {selected.length === 0 ? (
          <p className={styles.placeholder}>Hali guruh tanlanmagan.</p>
        ) : (
          <div className={styles.sections}>
            {groupBySection(selected).map(([key, section]) => (
              <div key={key} className={styles.section}>
                <div className={styles.sectionTitle}>{section.title}</div>
                <ul className={styles.chips}>
                  {section.items.map((g) => (
                    <li key={g.id} className={styles.chip}>
                      <span>{g.code}</span>
                      <button
                        type="button"
                        className={styles.chipRemove}
                        aria-label={`${g.code} guruhini olib tashlash`}
                        disabled={disabled}
                        onClick={() => onChange(selected.filter((x) => x.id !== g.id))}
                      >
                        ×
                      </button>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
