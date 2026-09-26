import { useId, useState } from 'react';
import { EmptyState, Input } from '@/shared/ui';
import type { Company } from '../../companies/types';
import { ErrorState, LoadingState } from '../../components/PageStatus';
import { formatTin } from '../../shared/format';
import { useDebouncedValue } from '../../shared/useDebouncedValue';
import { useCompanyPickerQuery } from '../hooks';
import styles from './CompanyPicker.module.css';

/** Ro'yxatda ko'rsatiladigan korxonalar soni (qidiruv bilan toraytiriladi). */
const PICKER_PAGE_SIZE = 20;

export interface CompanyPickerProps {
  /** Tanlangan korxona id'si (hali tanlanmagan — null). */
  value: string | null;
  onChange: (companyId: string) => void;
  /** So'rov ketayotganda tanlovni qulflash. */
  disabled?: boolean;
  /** Talabaning joriy korxonasi — "joriy" deb belgilanadi va tanlab bo'lmaydi. */
  currentCompanyId?: string | null;
}

/**
 * Korxona qidirish + tanlash (radio ro'yxat, `GET /api/admin/companies?q=`).
 * Faol bo'lmagan korxonalar va `currentCompanyId` tanlanmaydi. Ommaviy biriktirish modali va
 * talaba profilidagi biriktirish/o'tkazish modali umumiy ishlatadi.
 */
export function CompanyPicker({
  value,
  onChange,
  disabled = false,
  currentCompanyId = null,
}: CompanyPickerProps) {
  const uid = useId();
  const [search, setSearch] = useState('');
  const q = useDebouncedValue(search.trim(), 300);
  const query = useCompanyPickerQuery({ q, page: 1, pageSize: PICKER_PAGE_SIZE }, true);
  const companies: readonly Company[] = query.data?.items ?? [];

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
        {companies.map((c) => {
          const isCurrent = c.id === currentCompanyId;
          const unavailable = !c.isActive || isCurrent;
          return (
            <li key={c.id}>
              <label className={styles.option} data-disabled={unavailable || undefined}>
                <input
                  className={styles.radio}
                  type="radio"
                  name={`${uid}-company`}
                  value={c.id}
                  checked={value === c.id}
                  disabled={unavailable || disabled}
                  aria-label={c.name}
                  onChange={() => onChange(c.id)}
                />
                <span className={styles.optionText}>
                  <span className={styles.name}>{c.name}</span>
                  <span className={styles.meta}>
                    <span className={styles.tin}>{formatTin(c.tin)}</span> · {c.address}
                    {!c.isActive && ' · faol emas'}
                    {isCurrent && ' · joriy korxona'}
                  </span>
                </span>
              </label>
            </li>
          );
        })}
      </ul>
    );
  }

  return (
    <>
      <Input
        variant="search"
        type="search"
        placeholder="Korxona nomi yoki STIR…"
        aria-label="Korxona qidirish"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
      />
      <div className={styles.scroll}>{list}</div>
    </>
  );
}
