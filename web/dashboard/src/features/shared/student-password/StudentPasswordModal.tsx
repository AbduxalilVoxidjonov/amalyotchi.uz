import { useEffect, useId, useState, type ChangeEvent, type FormEvent } from 'react';
import {
  firstIssues,
  passwordResetSchema,
  type PasswordResetFormValues,
} from '@/features/shared/password/schema';
import { errorMessage, isApiError } from '@/shared/api';
import { getTwaUrl } from '@/shared/lib/env';
import { Button, Input, Modal } from '@/shared/ui';
import type { StudentPasswordArea } from './api';
import { copyText } from './copyText';
import { studentCredentialsText } from './credentials';
import { generatePassword } from './generatePassword';
import { useSetStudentPassword } from './hooks';
import styles from './StudentPasswordModal.module.css';

export interface StudentPasswordModalProps {
  open: boolean;
  area: StudentPasswordArea;
  student: { id: string; name: string; hemisId: string };
  onClose: () => void;
}

type FieldKey = keyof PasswordResetFormValues;

const EMPTY_VALUES: PasswordResetFormValues = { password: '', confirm: '' };

/**
 * "Parol o'rnatish" — talabaga admin/tyutor parol beradi (`POST /api/{area}/students/{id}/password`).
 * Validatsiya tyutor parol tiklash modali bilan umumiy (`features/shared/password/schema`).
 * 204 dan keyin ikkinchi qadam: talabaga beriladigan "HEMIS ID · Parol · Kirish" qatori nusxalash bilan.
 */
export function StudentPasswordModal({ open, area, student, onClose }: StudentPasswordModalProps) {
  const formId = useId();
  const mutation = useSetStudentPassword(area);
  const [values, setValues] = useState<PasswordResetFormValues>(EMPTY_VALUES);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<FieldKey, string>>>({});
  const [visible, setVisible] = useState(false);
  const [copied, setCopied] = useState<'password' | 'credentials' | null>(null);
  /** O'rnatilgan parol (muvaffaqiyat qadami); null — forma qadami. */
  const [savedPassword, setSavedPassword] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setValues(EMPTY_VALUES);
    setFieldErrors({});
    setVisible(false);
    setCopied(null);
    setSavedPassword(null);
    mutation.reset();
    // Faqat ochilganda (mutatsiya obyekti har render yangi).
  }, [open]);

  useEffect(() => {
    if (!copied) return undefined;
    const t = setTimeout(() => setCopied(null), 2000);
    return () => clearTimeout(t);
  }, [copied]);

  function handleClose() {
    if (mutation.isPending) return;
    onClose();
  }

  function field(key: FieldKey) {
    return (e: ChangeEvent<HTMLInputElement>) => {
      setValues((v) => ({ ...v, [key]: e.target.value }));
      setCopied(null);
    };
  }

  function handleGenerate() {
    const password = generatePassword();
    setValues({ password, confirm: password });
    setFieldErrors({});
    // Yaratilgan parolni admin ko'rishi va talabaga aytishi kerak.
    setVisible(true);
    setCopied(null);
  }

  async function handleCopy(kind: 'password' | 'credentials', text: string) {
    if (await copyText(text)) setCopied(kind);
  }

  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const parsed = passwordResetSchema.safeParse(values);
    if (!parsed.success) {
      setFieldErrors(firstIssues<FieldKey>(parsed.error.issues));
      return;
    }
    setFieldErrors({});
    const password = parsed.data.password;
    mutation.mutate(
      { id: student.id, password },
      {
        onSuccess: () => {
          setCopied(null);
          setSavedPassword(password);
        },
      },
    );
  }

  if (savedPassword !== null) {
    const credentials = studentCredentialsText(student.hemisId, savedPassword, getTwaUrl());
    return (
      <Modal
        open={open}
        onClose={handleClose}
        title="Parol o'rnatildi"
        description={`${student.name} uchun kirish ma'lumotini talabaga bering.`}
        width="440px"
        footer={
          <Button type="button" variant="primary" onClick={handleClose}>
            Tayyor
          </Button>
        }
      >
        <div className={styles.done}>
          <p className={styles.credentials} data-testid="student-credentials">
            {credentials}
          </p>
          <div className={styles.tools}>
            <Button
              type="button"
              size="xs"
              onClick={() => void handleCopy('credentials', credentials)}
            >
              Nusxalash
            </Button>
            {copied === 'credentials' && (
              <span role="status" className={styles.copied}>
                Nusxalandi
              </span>
            )}
          </div>
          <p className={styles.note}>Talaba birinchi kirishda parolni o'zgartiradi.</p>
        </div>
      </Modal>
    );
  }

  const apiError = isApiError(mutation.error) ? mutation.error : undefined;
  const passwordError = fieldErrors.password ?? apiError?.fieldError('password');
  const confirmError = fieldErrors.confirm;
  const generalError =
    mutation.isError && !passwordError ? errorMessage(mutation.error) : undefined;
  const inputType = visible ? 'text' : 'password';

  return (
    <Modal
      open={open}
      onClose={handleClose}
      title="Parol o'rnatish"
      description={`${student.name} (HEMIS ${student.hemisId}) uchun parol o'rnatiladi. Eski parol bo'lsa, ishlamay qoladi.`}
      width="440px"
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
          id={`${formId}-password`}
          label="Yangi parol"
          variant="form"
          type={inputType}
          mono={visible}
          autoComplete="new-password"
          hint="Kamida 8 ta belgi."
          value={values.password}
          onChange={field('password')}
          error={passwordError}
          disabled={mutation.isPending}
        />
        <Input
          id={`${formId}-confirm`}
          label="Parolni tasdiqlang"
          variant="form"
          type={inputType}
          mono={visible}
          autoComplete="new-password"
          value={values.confirm}
          onChange={field('confirm')}
          error={confirmError}
          disabled={mutation.isPending}
        />
        <div className={styles.tools}>
          <Button type="button" size="xs" onClick={handleGenerate} disabled={mutation.isPending}>
            Parol yaratish
          </Button>
          <Button
            type="button"
            size="xs"
            onClick={() => void handleCopy('password', values.password)}
            disabled={mutation.isPending || values.password === ''}
          >
            Nusxalash
          </Button>
          <Button
            type="button"
            size="xs"
            aria-pressed={visible}
            onClick={() => setVisible((v) => !v)}
          >
            {visible ? 'Yashirish' : "Ko'rsatish"}
          </Button>
          {copied === 'password' && (
            <span role="status" className={styles.copied}>
              Nusxalandi
            </span>
          )}
        </div>
        {generalError && (
          <p role="alert" className={styles.error}>
            {generalError}
          </p>
        )}
      </form>
    </Modal>
  );
}
