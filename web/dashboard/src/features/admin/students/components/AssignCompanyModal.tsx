import { useState } from 'react';
import { errorMessage } from '@/shared/api';
import { Button, Modal } from '@/shared/ui';
import { useAssignCompany } from '../hooks';
import type { AssignCompanyResult } from '../types';
import { CompanyPicker } from './CompanyPicker';
import styles from './AssignCompanyModal.module.css';

export interface AssignCompanyModalProps {
  /** Belgilangan talabalarning id'lari (kamida bitta). */
  studentIds: readonly string[];
  onClose: () => void;
  /** Kamida bitta talaba biriktirilgandan keyin (sahifa tanlovni tozalaydi). */
  onAssigned: () => void;
}

/**
 * "Korxonaga biriktirish" — belgilangan talabalar bitta korxonaga biriktiriladi
 * (`POST /api/admin/students/assign-company`). Korxona qidiruvdan tanlanadi; server
 * biriktirib bo'lmaydiganlarni sababi bilan qaytaradi, shuning uchun natijada hisobot chiqadi.
 * Modal faqat ochilganda mount qilinadi — holat har safar toza bo'ladi.
 */
export function AssignCompanyModal({ studentIds, onClose, onAssigned }: AssignCompanyModalProps) {
  const [companyId, setCompanyId] = useState<string | null>(null);
  const [result, setResult] = useState<AssignCompanyResult | null>(null);

  const mutation = useAssignCompany();

  function handleClose() {
    if (mutation.isPending) return;
    onClose();
  }

  function handleAssign() {
    if (!companyId) return;
    mutation.mutate(
      { studentIds: [...studentIds], companyId },
      {
        onSuccess: (assigned) => {
          setResult(assigned);
          if (assigned.assigned > 0) onAssigned();
        },
      },
    );
  }

  return (
    <Modal
      open
      onClose={handleClose}
      title="Korxonaga biriktirish"
      description="Belgilangan talabalar tanlangan korxonaga biriktiriladi. Biriktirib bo'lmaydiganlari sababi bilan ro'yxatda ko'rsatiladi."
      width="min(720px, 94vw)"
      footer={
        <>
          <span className={styles.count} aria-live="polite">
            Tanlangan: {studentIds.length} ta talaba
          </span>
          <Button type="button" onClick={handleClose} disabled={mutation.isPending}>
            Yopish
          </Button>
          <Button
            type="button"
            variant="primary"
            onClick={handleAssign}
            disabled={!companyId || mutation.isPending}
          >
            {mutation.isPending ? 'Biriktirilmoqda…' : 'Biriktirish'}
          </Button>
        </>
      }
    >
      <div className={styles.body}>
        <CompanyPicker value={companyId} onChange={setCompanyId} disabled={mutation.isPending} />

        {mutation.isError && (
          <p role="alert" className={styles.error}>
            {errorMessage(mutation.error)}
          </p>
        )}

        {result && <AssignReport result={result} />}
      </div>
    </Modal>
  );
}

function AssignReport({ result }: { result: AssignCompanyResult }) {
  return (
    <section className={styles.report} aria-label="Biriktirish natijasi">
      <p className={result.assigned > 0 ? styles.success : styles.error} role="status">
        Biriktirildi: {result.assigned} · Biriktirilmadi: {result.skipped} · Jami: {result.total}
      </p>
      {result.assigned > 0 && <p className={styles.hint}>Korxona: {result.companyName}</p>}

      {result.errors.length > 0 && (
        <div className={styles.tableWrap}>
          <table className={styles.table}>
            <caption className={styles.caption}>Biriktirilmagan talabalar</caption>
            <thead>
              <tr>
                <th scope="col">FISH</th>
                <th scope="col">Sabab</th>
              </tr>
            </thead>
            <tbody>
              {result.errors.map((row) => (
                <tr key={row.studentId}>
                  <td>{row.studentName}</td>
                  <td>{row.message}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
