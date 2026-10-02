import { useEffect, useId, useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { errorMessage, isApiError } from '@/shared/api';
import { Button, Input, Modal, Select } from '@/shared/ui';
import { formatPhone } from '../../shared/format';
import { useCreateStudent, useStudentGroupOptions } from '../hooks';
import {
  firstIssues,
  normalizeUzPhone,
  studentCreateSchema,
  type StudentCreateFormValues,
} from '../schema';
import type { Student, StudentFilters, StudentGroupOption } from '../types';
import type { StudentFilterValues } from '../useStudentListParams';
import styles from './StudentFormModal.module.css';

type FieldKey = keyof StudentCreateFormValues;

/** Fokus tartibi — birinchi xatoli maydonga o'tish uchun. */
const FIELD_ORDER: readonly FieldKey[] = ['fullName', 'hemisId', 'groupId', 'phoneNumber'];

const EMPTY_VALUES: StudentCreateFormValues = {
  fullName: '',
  hemisId: '',
  groupId: '',
  phoneNumber: '',
};

const STUDENT_LOGIN_NOTE =
  "Talaba Telegram orqali kiradi. Brauzerdan kirishi uchun talaba sahifasida parol o'rnatishingiz mumkin.";

/** "412-22 · 3-kurs · Kompyuter injiniringi · Axborot texnologiyalari" */
function groupOptionLabel(g: StudentGroupOption): string {
  return [g.name, g.course != null ? `${g.course}-kurs` : null, g.directionName, g.facultyName]
    .filter(Boolean)
    .join(' · ');
}

export interface StudentFormModalProps {
  onClose: () => void;
  /** Talabalar sahifasidagi joriy filtrlar — guruh tanlovi shular bilan toraytirilib ochiladi. */
  initialScope: StudentFilterValues;
  /** Fakultet/yo'nalish/kurs variantlari (`GET /students/filters`). */
  filterOptions: StudentFilters | undefined;
  /** Talaba yaratildi. `keepOpen` — "Saqlash va yana qo'shish" (modal ochiq qoladi). */
  onCreated: (student: Student, keepOpen: boolean) => void;
}

interface FormErrors {
  fields: Partial<Record<FieldKey, string>>;
  general: string | undefined;
}

/**
 * Server xatosini maydonlarga taqsimlaydi: 400 `errors` (camelCase) — tegishli maydon ostida;
 * 409 (HEMIS ID yoki telefon band) — `detail` mos maydon ostida, aniqlanmasa — umumiy xabar.
 */
function serverErrors(error: unknown): FormErrors {
  if (!error) return { fields: {}, general: undefined };
  if (!isApiError(error)) return { fields: {}, general: errorMessage(error) };
  const fields: Partial<Record<FieldKey, string>> = {};
  for (const key of FIELD_ORDER) {
    const message = error.fieldError(key);
    if (message) fields[key] = message;
  }
  if (error.status === 409) {
    const detail = error.message;
    if (/hemis/i.test(detail)) fields.hemisId ??= detail;
    else if (/telefon/i.test(detail)) fields.phoneNumber ??= detail;
  }
  const hasField = Object.keys(fields).length > 0;
  return { fields, general: hasField ? undefined : errorMessage(error) };
}

/** Sahifa filtridagi yo'nalish tanlangan fakultetga tegishli bo'lmasa — tashlanadi. */
function initialScopeFor(scope: StudentFilterValues, options: StudentFilters | undefined) {
  const direction = options?.directions.find((d) => d.id === scope.directionId);
  const directionOk =
    !scope.directionId || !scope.facultyId || !direction || direction.facultyId === scope.facultyId;
  return directionOk ? scope : { ...scope, directionId: '' };
}

/**
 * "Talaba qo'shish" — bitta talabani yaratish (`POST /api/admin/students`). Guruh fakultet →
 * yo'nalish → kurs bilan toraytirilib tanlanadi (faqat faol guruhlar). Parol so'ralmaydi.
 * Har ochilishda yangidan mount qilinadi (sahifada shartli render) — holat o'z-o'zidan tozalanadi.
 */
export function StudentFormModal({
  onClose,
  initialScope,
  filterOptions,
  onCreated,
}: StudentFormModalProps) {
  const formId = useId();
  const createStudent = useCreateStudent();
  const [scope, setScope] = useState<StudentFilterValues>(() =>
    initialScopeFor(initialScope, filterOptions),
  );
  const [values, setValues] = useState<StudentCreateFormValues>(EMPTY_VALUES);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<FieldKey, string>>>({});
  const [lastAdded, setLastAdded] = useState<Student | null>(null);
  // Fokus render'dan keyin o'tkaziladi: so'rov paytida maydonlar disabled — darhol `focus()` ishlamaydi.
  const [focusRequest, setFocusRequest] = useState<{ key: FieldKey } | null>(null);

  const inputs = useRef<Partial<Record<FieldKey, HTMLInputElement | HTMLSelectElement | null>>>({});
  const bind = (key: FieldKey) => (el: HTMLInputElement | HTMLSelectElement | null) => {
    inputs.current[key] = el;
  };

  const course = scope.course ? Number(scope.course) : undefined;
  const groups = useStudentGroupOptions(
    {
      ...(scope.facultyId ? { facultyId: scope.facultyId } : {}),
      ...(scope.directionId ? { directionId: scope.directionId } : {}),
      ...(course !== undefined ? { course } : {}),
    },
    true,
  );

  const pending = createStudent.isPending;
  const server = serverErrors(createStudent.error);
  const shown = (key: FieldKey) => fieldErrors[key] ?? server.fields[key];

  useEffect(() => {
    if (focusRequest) inputs.current[focusRequest.key]?.focus();
  }, [focusRequest]);

  function focusFirst(errors: Partial<Record<FieldKey, string>>) {
    const key = FIELD_ORDER.find((k) => errors[k]);
    if (key) setFocusRequest({ key });
  }

  function handleClose() {
    if (pending) return;
    onClose();
  }

  function field(key: Exclude<FieldKey, 'groupId'>) {
    return (e: ChangeEvent<HTMLInputElement>) =>
      setValues((v) => ({ ...v, [key]: e.target.value }));
  }

  /** Toraytirish o'zgarsa — tanlangan guruh endi ro'yxatda bo'lmasligi mumkin, shuning uchun tozalanadi. */
  function changeScope(next: Partial<StudentFilterValues>) {
    setScope((s) => ({ ...s, ...next }));
    setValues((v) => ({ ...v, groupId: '' }));
  }

  function handleFacultyChange(facultyId: string) {
    const direction = filterOptions?.directions.find((d) => d.id === scope.directionId);
    const keepDirection = !facultyId || !direction || direction.facultyId === facultyId;
    changeScope(keepDirection ? { facultyId } : { facultyId, directionId: '' });
  }

  function submit(keepOpen: boolean) {
    if (pending) return;
    const parsed = studentCreateSchema.safeParse(values);
    if (!parsed.success) {
      const errors = firstIssues<FieldKey>(parsed.error.issues);
      setFieldErrors(errors);
      focusFirst(errors);
      return;
    }
    setFieldErrors({});
    setLastAdded(null);
    createStudent.mutate(parsed.data, {
      onSuccess: (student) => {
        onCreated(student, keepOpen);
        if (!keepOpen) return;
        // Guruh va toraytirish saqlanadi — odatda bir guruhga ketma-ket bir nechta talaba qo'shiladi.
        setValues((v) => ({ ...EMPTY_VALUES, groupId: v.groupId }));
        setLastAdded(student);
        createStudent.reset();
        setFocusRequest({ key: 'fullName' });
      },
      onError: (error) => focusFirst(serverErrors(error).fields),
    });
  }

  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    submit(false);
  }

  /** Telefon to'g'ri bo'lsa — o'qilishi oson shaklga keltiriladi ("+998 90 123-45-67"). */
  function handlePhoneBlur() {
    const normalized = normalizeUzPhone(values.phoneNumber);
    if (normalized) setValues((v) => ({ ...v, phoneNumber: formatPhone(normalized) }));
  }

  const directions = (filterOptions?.directions ?? []).filter(
    (d) => !scope.facultyId || d.facultyId === scope.facultyId,
  );
  const groupItems = groups.data ?? [];
  const groupPlaceholder = groups.isPending
    ? 'Yuklanmoqda…'
    : groups.isError
      ? "Guruhlarni yuklab bo'lmadi"
      : groupItems.length === 0
        ? "Mos faol guruh yo'q"
        : 'Guruhni tanlang';
  const spinner = <span className={styles.spinner} aria-hidden="true" />;

  return (
    <Modal
      open
      onClose={handleClose}
      title="Talaba qo'shish"
      width="560px"
      footer={
        <>
          <Button type="button" onClick={handleClose} disabled={pending}>
            Bekor qilish
          </Button>
          <Button type="button" onClick={() => submit(true)} disabled={pending}>
            Saqlash va yana qo'shish
          </Button>
          <Button
            type="submit"
            form={formId}
            variant="primary"
            disabled={pending}
            aria-busy={pending || undefined}
            {...(pending ? { leading: spinner } : {})}
          >
            {pending ? 'Saqlanmoqda…' : 'Saqlash'}
          </Button>
        </>
      }
    >
      <form id={formId} className={styles.form} noValidate onSubmit={handleSubmit}>
        {lastAdded && (
          <p className={styles.success} role="status">
            «{lastAdded.fullName}» qo'shildi.{' '}
            <Link to={`/admin/students/${lastAdded.id}`}>Talaba sahifasini ochish</Link>
          </p>
        )}
        {server.general && (
          <p role="alert" className={styles.error}>
            {server.general}
          </p>
        )}
        <Input
          ref={bind('fullName')}
          id="student-full-name"
          label="FISH"
          variant="form"
          autoComplete="off"
          value={values.fullName}
          onChange={field('fullName')}
          error={shown('fullName')}
          disabled={pending}
          aria-required="true"
        />
        <Input
          ref={bind('hemisId')}
          id="student-hemis-id"
          label="HEMIS ID"
          variant="form"
          mono
          inputMode="numeric"
          autoComplete="off"
          maxLength={20}
          hint="5–20 ta raqam."
          value={values.hemisId}
          onChange={field('hemisId')}
          error={shown('hemisId')}
          disabled={pending}
          aria-required="true"
        />
        <fieldset className={styles.fieldset} disabled={pending}>
          <legend className={styles.legend}>Guruhni toraytirish (ixtiyoriy)</legend>
          <div className={styles.scope}>
            <Select
              label="Fakultet"
              variant="form"
              value={scope.facultyId}
              disabled={!filterOptions}
              onChange={(e) => handleFacultyChange(e.target.value)}
            >
              <option value="">Barchasi</option>
              {filterOptions?.faculties.map((f) => (
                <option key={f.id} value={f.id}>
                  {f.name}
                </option>
              ))}
            </Select>
            <Select
              label="Yo'nalish"
              variant="form"
              value={scope.directionId}
              disabled={!filterOptions}
              onChange={(e) => changeScope({ directionId: e.target.value })}
            >
              <option value="">Barchasi</option>
              {directions.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </Select>
            <Select
              label="Kurs"
              variant="form"
              value={scope.course}
              disabled={!filterOptions}
              onChange={(e) => changeScope({ course: e.target.value })}
            >
              <option value="">Barchasi</option>
              {filterOptions?.courses.map((c) => (
                <option key={c} value={String(c)}>
                  {c}-kurs
                </option>
              ))}
            </Select>
          </div>
        </fieldset>
        <Select
          ref={bind('groupId')}
          id="student-group"
          label="Guruh"
          variant="form"
          value={values.groupId}
          placeholder={groupPlaceholder}
          disabled={pending || groups.isPending}
          onChange={(e) => setValues((v) => ({ ...v, groupId: e.target.value }))}
          error={shown('groupId')}
          aria-required="true"
        >
          {groupItems.map((g) => (
            <option key={g.id} value={g.id}>
              {groupOptionLabel(g)}
            </option>
          ))}
        </Select>
        <Input
          ref={bind('phoneNumber')}
          id="student-phone"
          label="Telefon"
          variant="form"
          mono
          type="tel"
          autoComplete="off"
          placeholder="+998 90 123-45-67"
          hint="Ixtiyoriy."
          value={values.phoneNumber}
          onChange={field('phoneNumber')}
          onBlur={handlePhoneBlur}
          error={shown('phoneNumber')}
          disabled={pending}
        />
        <p className={styles.note}>{STUDENT_LOGIN_NOTE}</p>
      </form>
    </Modal>
  );
}
