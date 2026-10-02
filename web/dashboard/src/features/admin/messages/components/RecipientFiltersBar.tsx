import { Button, Select } from '@/shared/ui';
import type { StudentFilters } from '../../students/types';
import type { RecipientGroupOption } from '../types';
import type { RecipientFilterValues } from '../useRecipientListParams';
import styles from './RecipientFiltersBar.module.css';

export interface RecipientFiltersBarProps {
  /** Fakultet/yo'nalish/kurs variantlari (`GET /students/filters`); yuklanmaguncha `undefined`. */
  options: StudentFilters | undefined;
  isLoading: boolean;
  /** Variantlar yuklanmadi — select'lar yashiriladi (ro'yxat ishlashda davom etadi). */
  isError: boolean;
  /** Guruh variantlari (`GET /messages/recipients/groups`) — yuqoridagi filtrlarga bog'liq. */
  groups: readonly RecipientGroupOption[] | undefined;
  groupsLoading: boolean;
  values: RecipientFilterValues;
  onFacultyChange: (facultyId: string) => void;
  onDirectionChange: (directionId: string) => void;
  onCourseChange: (course: string) => void;
  onGroupChange: (groupId: string) => void;
  hasFilters: boolean;
  onClear: () => void;
  /** Filtrlangan jami soni (`Paged.total`); yuklanmaguncha `undefined`. */
  total: number | undefined;
}

/** Qidiruv yonidagi filtrlar: Fakultet → Yo'nalish → Kurs → Guruh (bog'liq) + tozalash + jami. */
export function RecipientFiltersBar({
  options,
  isLoading,
  isError,
  groups,
  groupsLoading,
  values,
  onFacultyChange,
  onDirectionChange,
  onCourseChange,
  onGroupChange,
  hasFilters,
  onClear,
  total,
}: RecipientFiltersBarProps) {
  const disabled = isLoading || !options;
  const directions = (options?.directions ?? []).filter(
    (d) => !values.facultyId || d.facultyId === values.facultyId,
  );

  return (
    <>
      {!isError && (
        <>
          <Select
            aria-label="Fakultet"
            variant="search"
            wrapperClassName={styles.filter}
            value={values.facultyId}
            disabled={disabled}
            onChange={(e) => onFacultyChange(e.target.value)}
          >
            <option value="">Fakultet: barchasi</option>
            {options?.faculties.map((f) => (
              <option key={f.id} value={f.id}>
                {f.name}
              </option>
            ))}
          </Select>
          <Select
            aria-label="Yo'nalish"
            variant="search"
            wrapperClassName={styles.filter}
            value={values.directionId}
            disabled={disabled}
            onChange={(e) => onDirectionChange(e.target.value)}
          >
            <option value="">Yo'nalish: barchasi</option>
            {directions.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </Select>
          <Select
            aria-label="Kurs"
            variant="search"
            wrapperClassName={styles.short}
            value={values.course}
            disabled={disabled}
            onChange={(e) => onCourseChange(e.target.value)}
          >
            <option value="">Kurs: barchasi</option>
            {options?.courses.map((c) => (
              <option key={c} value={String(c)}>
                {c}-kurs
              </option>
            ))}
          </Select>
        </>
      )}
      <Select
        aria-label="Guruh"
        variant="search"
        wrapperClassName={styles.short}
        value={values.groupId}
        disabled={groupsLoading && !groups}
        onChange={(e) => onGroupChange(e.target.value)}
      >
        <option value="">Guruh: barchasi</option>
        {groups?.map((g) => (
          <option key={g.id} value={g.id}>
            {g.name}
          </option>
        ))}
      </Select>
      {hasFilters && (
        <Button size="xs" onClick={onClear}>
          Filtrlarni tozalash
        </Button>
      )}
      {total !== undefined && (
        <span className={styles.total} aria-live="polite">
          Jami: {total} ta
        </span>
      )}
    </>
  );
}
