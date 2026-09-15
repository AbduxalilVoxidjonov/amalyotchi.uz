import { useEffect, useId, useState, type ChangeEvent, type FormEvent } from 'react';
import { errorMessage, isApiError } from '@/shared/api';
import { Button, Input, Modal } from '@/shared/ui';
import { nameCodeSchema, type NameCodeFormValues } from '../../shared/nameCodeSchema';
import styles from './EntityFormModal.module.css';

export interface EntityFormInitial {
  id: string;
  name: string;
  code: string;
}

export interface EntityFormModalProps {
  open: boolean;
  mode: 'create' | 'edit';
  /** `mode==='edit'` da forma shu qiymatlar bilan to'ldiriladi. */
  initial?: EntityFormInitial | null;
  titleCreate: string;
  titleEdit: string;
  nameLabel?: string;
  codeLabel?: string;
  codePlaceholder?: string;
  /** Default 20 (kafedra/yo'nalish kodi 2–20). */
  codeMaxLength?: number;
  isPending: boolean;
  isError: boolean;
  error: unknown;
  onSubmit: (values: NameCodeFormValues) => void;
  onClose: () => void;
}

const EMPTY_VALUES: NameCodeFormValues = { name: '', code: '' };

/**
 * Kafedra/yo'nalish uchun umumlashtirilgan "Yangi … / … ni tahrirlash" forma
 * (`FacultyFormModal` bilan bir xil UX, mutatsiyalar tashqaridan keladi — turli endpoint/ota id).
 */
export function EntityFormModal({
  open,
  mode,
  initial,
  titleCreate,
  titleEdit,
  nameLabel = 'Nomi',
  codeLabel = 'Kodi',
  codePlaceholder,
  codeMaxLength = 20,
  isPending,
  isError,
  error,
  onSubmit,
  onClose,
}: EntityFormModalProps) {
  const formId = useId();
  const [values, setValues] = useState<NameCodeFormValues>(EMPTY_VALUES);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<keyof NameCodeFormValues, string>>>(
    {},
  );

  // Har ochilishda (yoki tahrirlanayotgan yozuv almashganda) formani qayta boshlash.
  useEffect(() => {
    if (!open) return;
    setValues(
      mode === 'edit' && initial ? { name: initial.name, code: initial.code } : EMPTY_VALUES,
    );
    setFieldErrors({});
  }, [open, mode, initial?.id]);

  function handleClose() {
    if (isPending) return;
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
    const parsed = nameCodeSchema.safeParse(values);
    if (!parsed.success) {
      const next: Partial<Record<keyof NameCodeFormValues, string>> = {};
      for (const issue of parsed.error.issues) {
        const key = issue.path[0] as keyof NameCodeFormValues | undefined;
        if (key && !next[key]) next[key] = issue.message;
      }
      setFieldErrors(next);
      return;
    }
    setFieldErrors({});
    onSubmit(parsed.data);
  }

  const apiError = isApiError(error) ? error : undefined;
  const nameError = fieldErrors.name ?? apiError?.fieldError('name');
  const codeError = fieldErrors.code ?? apiError?.fieldError('code');
  // Validatsiya xatosi maydonlarda ko'rsatiladi; qolgan holatlar (404/409/tarmoq) — umumiy banner.
  const generalError = isError && !nameError && !codeError ? errorMessage(error) : undefined;

  return (
    <Modal
      open={open}
      onClose={handleClose}
      title={mode === 'edit' ? titleEdit : titleCreate}
      footer={
        <>
          <Button type="button" onClick={handleClose} disabled={isPending}>
            Bekor qilish
          </Button>
          <Button type="submit" form={formId} variant="primary" disabled={isPending}>
            {isPending ? 'Saqlanmoqda…' : 'Saqlash'}
          </Button>
        </>
      }
    >
      <form id={formId} className={styles.form} noValidate onSubmit={handleSubmit}>
        <Input
          id="entity-name"
          label={nameLabel}
          variant="form"
          value={values.name}
          onChange={handleNameChange}
          error={nameError}
          disabled={isPending}
        />
        <Input
          id="entity-code"
          label={codeLabel}
          variant="form"
          mono
          maxLength={codeMaxLength}
          placeholder={codePlaceholder}
          value={values.code}
          onChange={handleCodeChange}
          error={codeError}
          disabled={isPending}
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
