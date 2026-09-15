import { useEffect, useId, useState, type ChangeEvent, type FormEvent } from 'react';
import { errorMessage, isApiError } from '@/shared/api';
import { Button, Input, Modal } from '@/shared/ui';
import { useResetTutorPassword } from '../hooks';
import { firstIssues, passwordResetSchema, type PasswordResetFormValues } from '../schema';
import styles from './TutorForms.module.css';

export interface PasswordResetModalProps {
  open: boolean;
  tutorId: string;
  tutorName: string;
  onClose: () => void;
  /** 204 dan keyin (sahifa "Parol yangilandi." xabarini ko'rsatadi). */
  onSuccess: () => void;
}

type FieldKey = keyof PasswordResetFormValues;

const EMPTY_VALUES: PasswordResetFormValues = { password: '', confirm: '' };

/** "Parolni tiklash" — yangi parol + tasdiq (min 8), `POST /tutors/{id}/password`. */
export function PasswordResetModal({
  open,
  tutorId,
  tutorName,
  onClose,
  onSuccess,
}: PasswordResetModalProps) {
  const formId = useId();
  const mutation = useResetTutorPassword();
  const [values, setValues] = useState<PasswordResetFormValues>(EMPTY_VALUES);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<FieldKey, string>>>({});

  useEffect(() => {
    if (!open) return;
    setValues(EMPTY_VALUES);
    setFieldErrors({});
    mutation.reset();
    // Faqat ochilganda (mutatsiya obyekti har render yangi).
  }, [open]);

  function handleClose() {
    if (mutation.isPending) return;
    onClose();
  }

  function field(key: FieldKey) {
    return (e: ChangeEvent<HTMLInputElement>) =>
      setValues((v) => ({ ...v, [key]: e.target.value }));
  }

  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const parsed = passwordResetSchema.safeParse(values);
    if (!parsed.success) {
      setFieldErrors(firstIssues<FieldKey>(parsed.error.issues));
      return;
    }
    setFieldErrors({});
    mutation.mutate(
      { id: tutorId, password: parsed.data.password },
      {
        onSuccess: () => {
          onSuccess();
          onClose();
        },
      },
    );
  }

  const apiError = isApiError(mutation.error) ? mutation.error : undefined;
  const passwordError = fieldErrors.password ?? apiError?.fieldError('password');
  const confirmError = fieldErrors.confirm;
  const generalError =
    mutation.isError && !passwordError ? errorMessage(mutation.error) : undefined;

  return (
    <Modal
      open={open}
      onClose={handleClose}
      title="Parolni tiklash"
      description={`${tutorName} uchun yangi parol o'rnatiladi. Eski parol ishlamay qoladi.`}
      width="400px"
      footer={
        <>
          <Button type="button" onClick={handleClose} disabled={mutation.isPending}>
            Bekor qilish
          </Button>
          <Button type="submit" form={formId} variant="primary" disabled={mutation.isPending}>
            {mutation.isPending ? 'Saqlanmoqda…' : "O'rnatish"}
          </Button>
        </>
      }
    >
      <form id={formId} className={styles.form} noValidate onSubmit={handleSubmit}>
        <Input
          id="tutor-new-password"
          label="Yangi parol"
          variant="form"
          type="password"
          autoComplete="new-password"
          hint="Kamida 8 ta belgi."
          value={values.password}
          onChange={field('password')}
          error={passwordError}
          disabled={mutation.isPending}
        />
        <Input
          id="tutor-new-password-confirm"
          label="Parolni tasdiqlang"
          variant="form"
          type="password"
          autoComplete="new-password"
          value={values.confirm}
          onChange={field('confirm')}
          error={confirmError}
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
