import { Button, Select } from '@/shared/ui';
import type { StudentFilters } from '../types';
import type { StudentFilterValues } from '../useStudentListParams';
import styles from './StudentFiltersBar.module.css';

export interface StudentFiltersBarProps {
  /** Variantlar (`GET /students/filters`); yuklanmaguncha `undefined`. */
  options: StudentFilters | undefined;
  isLoading: boolean;
  /** Variantlar yuklanmadi — select'lar yashiriladi (ro'yxat ishlashda davom etadi). */
  isError: boolean;
  values: StudentFilterValues;
  onFacultyChange: (facultyId: string) => void;
  onDirectionChange: (directionId: string) => void;
  onCourseChange: (course: string) => void;
  /** Faol filtr bor — "Filtrlarni tozalash" tugmasi chiqadi. */
  hasFilters: boolean;
  onClear: () => void;
  /** Filtrlangan jami soni (`Paged.total`); yuklanmaguncha `undefined`. */
  total: number | undefined;
}

/** Qidiruv yonidagi filtrlar: Fakultet · Yo'nalish (fakultetga bog'liq) · Kurs + tozalash + jami. */
export function StudentFiltersBar({
  options,
  isLoading,
  isError,
  values,
  onFacultyChange,
  onDirectionChange,
  onCourseChange,
  hasFilters,
  onClear,
  total,
}: StudentFiltersBarProps) {
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
            wrapperClassName={styles.course}
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
