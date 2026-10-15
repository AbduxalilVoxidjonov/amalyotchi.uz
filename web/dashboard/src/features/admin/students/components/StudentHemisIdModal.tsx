import { useId, useState, type FormEvent } from 'react';
import { errorMessage, isApiError } from '@/shared/api';
import { Button, Input, Modal } from '@/shared/ui';
import { useSetStudentHemisId } from '../hooks';
import { STUDENT_FORM_MESSAGES, studentCreateSchema } from '../schema';
import styles from './StudentFormModal.module.css';

const hemisSchema = studentCreateSchema.shape.hemisId;

export interface StudentHemisIdModalProps {
  student: { id: string; name: string; hemisId: string };
  onClose: () => void;
  onDone: (hemisId: string) => void;
}

/**
 * v3.34 — "HEMIS ID ni o'zgartirish" (`PUT /api/admin/students/{id}/hemis-id`): talabaning ilova
 * logini. Qoidalar yaratish bilan bir xil (5–20 raqam); boshqa talaba yoki admin logini band → 409.
 * Parol o'zgarmaydi. Faqat ochilganda mount qilinadi — holat har safar toza.
 */
export function StudentHemisIdModal({ student, onClose, onDone }: StudentHemisIdModalProps) {
  const formId = useId();
  const mutation = useSetStudentHemisId();
  const [value, setValue] = useState(student.hemisId);
  const [clientError, setClientError] = useState<string | undefined>();

  function handleClose() {
    if (!mutation.isPending) onClose();
  }

  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const parsed = hemisSchema.safeParse(value);
    if (!parsed.success) {
      setClientError(parsed.error.issues[0]?.message ?? STUDENT_FORM_MESSAGES.hemisFormat);
      return;
    }
    setClientError(undefined);
    const hemisId = parsed.data;
    if (hemisId === student.hemisId) {
      onClose();
      return;
    }
    mutation.mutate({ id: student.id, hemisId }, { onSuccess: () => onDone(hemisId) });
  }

  const apiError = isApiError(mutation.error) ? mutation.error : undefined;
  const fieldError =
    clientError ??
    apiError?.fieldError('hemisId') ??
    (apiError?.status === 409 ? apiError.message : undefined);
  const generalError = mutation.isError && !fieldError ? errorMessage(mutation.error) : undefined;

  return (
    <Modal
      open
      onClose={handleClose}
      title="HEMIS ID ni o'zgartirish"
      description={`${student.name} ilovaga shu HEMIS ID bilan kiradi. Parol o'zgarmaydi.`}
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
          id={`${formId}-hemis`}
          label="HEMIS ID"
          variant="form"
          mono
          inputMode="numeric"
          autoComplete="off"
          maxLength={20}
          hint="5–20 ta raqam."
          value={value}
          onChange={(e) => {
            setValue(e.target.value);
            setClientError(undefined);
          }}
          error={fieldError}
          disabled={mutation.isPending}
          aria-required="true"
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
