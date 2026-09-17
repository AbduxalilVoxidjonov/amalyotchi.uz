import { useId, useState } from 'react';
import { errorMessage } from '@/shared/api';
import { Button, EmptyState, Input, Modal } from '@/shared/ui';
import type { Company } from '../../companies/types';
import { ErrorState, LoadingState } from '../../components/PageStatus';
import { formatTin } from '../../shared/format';
import { useDebouncedValue } from '../../shared/useDebouncedValue';
import { useAssignCompany, useCompanyPickerQuery } from '../hooks';
import type { AssignCompanyResult } from '../types';
import styles from './AssignCompanyModal.module.css';

/** Modalda ko'rsatiladigan korxonalar soni (qidiruv bilan toraytiriladi). */
const PICKER_PAGE_SIZE = 20;

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
  const uid = useId();
  const [search, setSearch] = useState('');
  const q = useDebouncedValue(search.trim(), 300);
  const [companyId, setCompanyId] = useState<string | null>(null);
  const [result, setResult] = useState<AssignCompanyResult | null>(null);

  const query = useCompanyPickerQuery({ q, page: 1, pageSize: PICKER_PAGE_SIZE }, true);
  const mutation = useAssignCompany();

  const companies: readonly Company[] = query.data?.items ?? [];

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

  let list = null;
  if (query.isPending) {
    list = <LoadingState />;
  } else if (query.isError) {
    list = <ErrorState inline error={query.error} onRetry={() => void query.refetch()} />;
  } else if (companies.length === 0) {
    list = (
      <EmptyState
        tone="plain"
        title="Korxona topilmadi"
        description="Nom yoki STIR bo'yicha boshqacha qidirib ko'ring."
      />
    );
  } else {
    list = (
      <ul className={styles.list}>
        {companies.map((c) => (
          <li key={c.id}>
            <label className={styles.option} data-disabled={!c.isActive || undefined}>
              <input
                className={styles.radio}
                type="radio"
                name={`${uid}-company`}
                value={c.id}
                checked={companyId === c.id}
                disabled={!c.isActive || mutation.isPending}
                aria-label={c.name}
                onChange={() => setCompanyId(c.id)}
              />
              <span className={styles.optionText}>
                <span className={styles.name}>{c.name}</span>
                <span className={styles.meta}>
                  <span className={styles.tin}>{formatTin(c.tin)}</span> · {c.address}
                  {!c.isActive && ' · faol emas'}
                </span>
              </span>
            </label>
          </li>
        ))}
      </ul>
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
        <Input
          variant="search"
          type="search"
          placeholder="Korxona nomi yoki STIR…"
          aria-label="Korxona qidirish"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <div className={styles.scroll}>{list}</div>

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
