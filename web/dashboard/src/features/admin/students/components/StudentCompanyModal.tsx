import { useState } from 'react';
import { isApiError } from '@/shared/api';
import { Alert, Button, Modal, Textarea } from '@/shared/ui';
import { ServerErrorBanner } from '../../practice-periods/components/ServerErrorBanner';
import { useSetStudentCompany } from '../hooks';
import { STUDENT_COMPANY_COMMENT_MAX, type AdminStudentDetail } from '../types';
import { CompanyPicker } from './CompanyPicker';
import styles from './StudentCompanyModal.module.css';

export const TRANSFER_WARNING =
  "Joriy ariza «Ko'chirilgan» holatiga o'tadi. Davomat va kundalik tarixi saqlanadi; bugundan yangi korxona QR/geolokatsiyasi amal qiladi.";

export interface StudentCompanyModalProps {
  studentId: string;
  studentName: string;
  /** Talabaning joriy korxonasi — bor bo'lsa "o'tkazish" rejimi, yo'q bo'lsa birinchi biriktirish. */
  currentCompany: { id: string; name: string } | null;
  onClose: () => void;
  /** Muvaffaqiyat: yangilangan profil (modal yopiladi, sahifa xabar ko'rsatadi). */
  onDone: (detail: AdminStudentDetail) => void;
}

/**
 * Talaba profilidan bitta talabani korxonaga biriktirish yoki boshqa korxonaga o'tkazish
 * (`POST /api/admin/students/{id}/company`). Joriy korxona ro'yxatda "joriy" deb belgilanadi va
 * tanlanmaydi. Server xatolari (404/409/400) modal ichida banner bo'lib chiqadi.
 * Modal faqat ochilganda mount qilinadi — holat har safar toza bo'ladi.
 */
export function StudentCompanyModal({
  studentId,
  studentName,
  currentCompany,
  onClose,
  onDone,
}: StudentCompanyModalProps) {
  const [companyId, setCompanyId] = useState<string | null>(null);
  const [comment, setComment] = useState('');
  const mutation = useSetStudentCompany(studentId);

  const transfer = currentCompany !== null;
  const title = transfer ? "Boshqa korxonaga o'tkazish" : 'Korxonaga biriktirish';
  const trimmed = comment.trim();
  const commentTooLong = trimmed.length > STUDENT_COMPANY_COMMENT_MAX;
  const apiError = isApiError(mutation.error) ? mutation.error : null;
  const commentError = commentTooLong
    ? `Izoh ${STUDENT_COMPANY_COMMENT_MAX} belgidan oshmasligi kerak.`
    : apiError?.fieldError('comment');

  function handleClose() {
    if (mutation.isPending) return;
    onClose();
  }

  function handleSubmit() {
    if (!companyId || commentTooLong) return;
    mutation.mutate(trimmed ? { companyId, comment: trimmed } : { companyId }, {
      onSuccess: onDone,
    });
  }

  function handleSelect(id: string) {
    setCompanyId(id);
    if (mutation.isError) mutation.reset();
  }

  return (
    <Modal
      open
      onClose={handleClose}
      title={title}
      description={
        transfer
          ? `${studentName} · joriy korxona: ${currentCompany.name}`
          : `${studentName} tanlangan korxonaga biriktiriladi.`
      }
      width="min(720px, 94vw)"
      footer={
        <>
          <Button type="button" onClick={handleClose} disabled={mutation.isPending}>
            Bekor qilish
          </Button>
          <Button
            type="button"
            variant="primary"
            onClick={handleSubmit}
            disabled={!companyId || commentTooLong || mutation.isPending}
          >
            {mutation.isPending ? 'Saqlanmoqda…' : transfer ? "O'tkazish" : 'Biriktirish'}
          </Button>
        </>
      }
    >
      <div className={styles.body}>
        <ServerErrorBanner
          error={mutation.error}
          title={transfer ? "O'tkazib bo'lmadi" : "Biriktirib bo'lmadi"}
        />

        {transfer && (
          <Alert title="Diqqat" aria-label="O'tkazish oqibatlari">
            <p className={styles.warning}>{TRANSFER_WARNING}</p>
          </Alert>
        )}

        <CompanyPicker
          value={companyId}
          onChange={handleSelect}
          disabled={mutation.isPending}
          currentCompanyId={currentCompany?.id ?? null}
        />

        <Textarea
          variant="form"
          label="Izoh (ixtiyoriy)"
          hint={`${trimmed.length}/${STUDENT_COMPANY_COMMENT_MAX}`}
          rows={3}
          value={comment}
          error={commentError}
          disabled={mutation.isPending}
          onChange={(e) => setComment(e.target.value)}
        />
      </div>
    </Modal>
  );
}
