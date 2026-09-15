import { useEffect, useId, useState, type ChangeEvent, type FormEvent } from 'react';
import { errorMessage, isApiError } from '@/shared/api';
import { Button, Input, Modal } from '@/shared/ui';
import styles from './FacultyFormModal.module.css';
import { useCreateFaculty, useUpdateFaculty } from '../hooks';
import { facultySchema, type FacultyFormValues } from '../schema';
import type { Faculty } from '../types';

export interface FacultyFormModalProps {
  open: boolean;
  mode: 'create' | 'edit';
  /** `mode==='edit'` da forma shu qiymatlar bilan to'ldiriladi. */
  initial?: Faculty | null;
  onClose: () => void;
}

const EMPTY_VALUES: FacultyFormValues = { name: '', code: '' };

/** "Yangi fakultet" / "Fakultetni tahrirlash" — bitta forma, `mode` bo'yicha POST yoki PUT. */
export function FacultyFormModal({ open, mode, initial, onClose }: FacultyFormModalProps) {
  const formId = useId();
  const createFaculty = useCreateFaculty();
  const updateFaculty = useUpdateFaculty();
  const mutation = mode === 'edit' ? updateFaculty : createFaculty;

  const [values, setValues] = useState<FacultyFormValues>(EMPTY_VALUES);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<keyof FacultyFormValues, string>>>(
    {},
  );

  // Har ochilishda (yoki tahrirlanayotgan fakultet almashganda) formani qayta boshlash.
  useEffect(() => {
    if (!open) return;
    setValues(
      mode === 'edit' && initial ? { name: initial.name, code: initial.code } : EMPTY_VALUES,
    );
    setFieldErrors({});
    createFaculty.reset();
    updateFaculty.reset();
    // Faqat ochilish/rejim/tahrirlanayotgan id o'zgarganda qayta ishga tushadi (mutatsiya obyektlari
    // har render'da yangi bo'ladi — ularni dep qilib qo'shsak, terish paytida forma qayta tozalanardi).
  }, [open, mode, initial?.id]);

  function handleClose() {
    if (mutation.isPending) return;
    onClose();
  }

  function handleNameChange(e: ChangeEvent<HTMLInputElement>) {
    setValues((v) => ({ ...v, name: e.target.value }));
  }

  function handleCodeChange(e: ChangeEvent<HTMLInputElement>) {
    setValues((v) => ({ ...v, code: e.target.value.toUpperCase() }));
  }

  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const parsed = facultySchema.safeParse(values);
    if (!parsed.success) {
      const next: Partial<Record<keyof FacultyFormValues, string>> = {};
      for (const issue of parsed.error.issues) {
        const key = issue.path[0] as keyof FacultyFormValues | undefined;
        if (key && !next[key]) next[key] = issue.message;
      }
      setFieldErrors(next);
      return;
    }
    setFieldErrors({});
    const body = parsed.data;
    if (mode === 'edit' && initial) {
      updateFaculty.mutate({ id: initial.id, body }, { onSuccess: onClose });
    } else {
      createFaculty.mutate(body, { onSuccess: onClose });
    }
  }

  const apiError = isApiError(mutation.error) ? mutation.error : undefined;
  const nameError = fieldErrors.name ?? apiError?.fieldError('name');
  const codeError = fieldErrors.code ?? apiError?.fieldError('code');
  // Validatsiya xatosi maydonlarda ko'rsatiladi; qolgan holatlar (404/409/tarmoq) — umumiy banner.
  const generalError =
    mutation.isError && !nameError && !codeError ? errorMessage(mutation.error) : undefined;

  return (
    <Modal
      open={open}
      onClose={handleClose}
      title={mode === 'edit' ? 'Fakultetni tahrirlash' : 'Yangi fakultet'}
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
          id="faculty-name"
          label="Nomi"
          variant="form"
          value={values.name}
          onChange={handleNameChange}
          error={nameError}
          disabled={mutation.isPending}
        />
        <Input
          id="faculty-code"
          label="Kodi"
          variant="form"
          mono
          maxLength={10}
          placeholder="AT"
          value={values.code}
          onChange={handleCodeChange}
          error={codeError}
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
