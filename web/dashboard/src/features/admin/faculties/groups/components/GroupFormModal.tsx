import { useEffect, useId, useState, type ChangeEvent, type FormEvent } from 'react';
import { errorMessage, isApiError } from '@/shared/api';
import { Button, Input, Modal, Select } from '@/shared/ui';
import { useCreateGroup, useUpdateGroup } from '../hooks';
import { COURSE_OPTIONS, groupSchema, type GroupFormValues } from '../schema';
import type { GroupRow } from '../types';
import styles from './GroupFormModal.module.css';

export interface GroupFormModalProps {
  open: boolean;
  mode: 'create' | 'edit';
  /** `mode==='edit'` da forma shu qiymatlar bilan to'ldiriladi. */
  initial?: GroupRow | null;
  directionId: string;
  departmentId: string;
  facultyId: string;
  onClose: () => void;
}

const EMPTY_VALUES = { name: '', course: '' };

/** "Yangi guruh" / "Guruhni tahrirlash" — nom (masalan "412-22") + kurs (1–6). */
export function GroupFormModal({
  open,
  mode,
  initial,
  directionId,
  departmentId,
  facultyId,
  onClose,
}: GroupFormModalProps) {
  const formId = useId();
  const createGroup = useCreateGroup(directionId, departmentId, facultyId);
  const updateGroup = useUpdateGroup(directionId, departmentId, facultyId);
  const mutation = mode === 'edit' ? updateGroup : createGroup;

  const [values, setValues] = useState<{ name: string; course: string }>(EMPTY_VALUES);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<keyof GroupFormValues, string>>>(
    {},
  );

  useEffect(() => {
    if (!open) return;
    setValues(
      mode === 'edit' && initial
        ? { name: initial.code, course: String(initial.course) }
        : EMPTY_VALUES,
    );
    setFieldErrors({});
    createGroup.reset();
    updateGroup.reset();
    // Faqat ochilish/rejim/tahrirlanayotgan id o'zgarganda (mutatsiya obyektlari har render yangi).
  }, [open, mode, initial?.id]);

  function handleClose() {
    if (mutation.isPending) return;
    onClose();
  }

  function handleNameChange(e: ChangeEvent<HTMLInputElement>) {
    setValues((v) => ({ ...v, name: e.target.value }));
  }

  function handleCourseChange(e: ChangeEvent<HTMLSelectElement>) {
    setValues((v) => ({ ...v, course: e.target.value }));
  }

  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const parsed = groupSchema.safeParse(values);
    if (!parsed.success) {
      const next: Partial<Record<keyof GroupFormValues, string>> = {};
      for (const issue of parsed.error.issues) {
        const key = issue.path[0] as keyof GroupFormValues | undefined;
        if (key && !next[key]) next[key] = issue.message;
      }
      setFieldErrors(next);
      return;
    }
    setFieldErrors({});
    const body = parsed.data;
    if (mode === 'edit' && initial) {
      updateGroup.mutate({ id: initial.id, body }, { onSuccess: onClose });
    } else {
      createGroup.mutate(body, { onSuccess: onClose });
    }
  }

  const apiError = isApiError(mutation.error) ? mutation.error : undefined;
  const nameError = fieldErrors.name ?? apiError?.fieldError('name');
  const courseError = fieldErrors.course ?? apiError?.fieldError('course');
  const generalError =
    mutation.isError && !nameError && !courseError ? errorMessage(mutation.error) : undefined;

  return (
    <Modal
      open={open}
      onClose={handleClose}
      title={mode === 'edit' ? 'Guruhni tahrirlash' : 'Yangi guruh'}
      footer={
        <>
          <Button type="button" onClick={handleClose} disabled={mutation.isPending}>
            Bekor qilish
          </Button>
          <Button type="submit" form={formId} variant="primary" disabled={mutation.isPending}>
            {mutation.isPending ? 'Saqlanmoqda…' : 'Saqlash'}
          </Button>
        </>
      }
    >
      <form id={formId} className={styles.form} noValidate onSubmit={handleSubmit}>
        <Input
          id="group-name"
          label="Guruh nomi"
          variant="form"
          mono
          maxLength={20}
          placeholder="412-22"
          value={values.name}
          onChange={handleNameChange}
          error={nameError}
          disabled={mutation.isPending}
        />
        <Select
          id="group-course"
          label="Kurs"
          variant="form"
          placeholder="Tanlang"
          options={COURSE_OPTIONS}
          value={values.course}
          onChange={handleCourseChange}
          error={courseError}
          disabled={mutation.isPending}
        />
        {generalError && (
          <p role="alert" className={styles.error}>
            {generalError}
          </p>
        )}
      </form>
    </Modal>
  );
}
