import { useEffect, useId, useRef, useState, type ChangeEvent, type ReactNode } from 'react';
import type { UseMutationResult } from '@tanstack/react-query';
import { copyText } from '@/features/shared/student-password';
import { errorMessage } from '@/shared/api';
import { Button, Modal } from '@/shared/ui';
import type { ImportCredential, ImportResult } from '../shared/types';
import type { TemplateDownload } from '../shared/useTemplateDownload';
import styles from './ExcelImportModal.module.css';

const ACCEPT = '.xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

export interface ExcelImportModalProps {
  open: boolean;
  onClose: () => void;
  title: string;
  description: string;
  /** Ustunlar haqida qisqa izoh (shablon tugmasi ostida). */
  hint: ReactNode;
  /** Bo'sh fayl (0 qator) holatidagi maslahat. */
  emptyHint: string;
  template: TemplateDownload;
  mutation: UseMutationResult<ImportResult, Error, File>;
  /** Fayl maydoni ustidagi qo'shimcha sozlamalar (talabalar: "vaqtinchalik parol yaratish"). */
  options?: ReactNode;
}

/**
 * Umumiy "Excel import" oqimi (talabalar va korxonalar bir xil ishlatadi):
 * shablonni yuklab olish → to'ldirish → faylni tanlash → hisobot.
 * Server xato qatorlarni tashlab yuboradi va to'g'rilarini saqlaydi, shuning uchun hisobotda
 * qo'shilganlar soni ham, rad etilgan qatorlar ro'yxati ham ko'rsatiladi.
 */
export function ExcelImportModal({
  open,
  onClose,
  title,
  description,
  hint,
  emptyHint,
  template,
  mutation,
  options,
}: ExcelImportModalProps) {
  const fileId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [result, setResult] = useState<ImportResult | null>(null);

  // Har ochilishda toza holat (oldingi hisobot qolib ketmasin).
  useEffect(() => {
    if (!open) return;
    setFile(null);
    setResult(null);
    mutation.reset();
    if (inputRef.current) inputRef.current.value = '';
    // Faqat ochilish paytida; mutatsiya obyekti har renderda yangi.
  }, [open]);

  function handleFile(e: ChangeEvent<HTMLInputElement>) {
    setFile(e.target.files?.[0] ?? null);
    setResult(null);
    mutation.reset();
  }

  function handleImport() {
    if (!file) return;
    mutation.mutate(file, {
      onSuccess: (imported) => {
        setResult(imported);
        setFile(null);
        if (inputRef.current) inputRef.current.value = '';
      },
    });
  }

  function handleClose() {
    if (mutation.isPending) return;
    onClose();
  }

  const error = mutation.isError ? errorMessage(mutation.error) : undefined;

  return (
    <Modal
      open={open}
      onClose={handleClose}
      title={title}
      description={description}
      width="min(720px, 94vw)"
      footer={
        <>
          <Button type="button" onClick={handleClose} disabled={mutation.isPending}>
            Yopish
          </Button>
          <Button
            type="button"
            variant="primary"
            onClick={handleImport}
            disabled={!file || mutation.isPending}
          >
            {mutation.isPending ? 'Yuklanmoqda…' : 'Import qilish'}
          </Button>
        </>
      }
    >
      <div className={styles.body}>
        <section className={styles.step}>
          <Button type="button" onClick={template.download} disabled={template.isLoading}>
            {template.isLoading ? 'Tayyorlanmoqda…' : 'Shablonni yuklab olish'}
          </Button>
          <p className={styles.hint}>{hint}</p>
          {template.error && (
            <p role="alert" className={styles.error}>
              {template.error}
            </p>
          )}
        </section>

        {options}

        <div className={styles.field}>
          <label className={styles.label} htmlFor={fileId}>
            To‘ldirilgan fayl (.xlsx)
          </label>
          <input
            ref={inputRef}
            id={fileId}
            className={styles.file}
            type="file"
            accept={ACCEPT}
            onChange={handleFile}
            disabled={mutation.isPending}
          />
        </div>

        {error && (
          <p role="alert" className={styles.error}>
            {error}
          </p>
        )}

        {result && <ImportReport result={result} emptyHint={emptyHint} />}
      </div>
    </Modal>
  );
}

function ImportReport({ result, emptyHint }: { result: ImportResult; emptyHint: string }) {
  return (
    <section className={styles.report} aria-label="Import natijasi">
      <p className={result.created > 0 ? styles.success : styles.error} role="status">
        Qo‘shildi: {result.created} · Qabul qilinmadi: {result.failed} · Jami: {result.totalRows}
      </p>

      {result.totalRows === 0 && <p className={styles.hint}>{emptyHint}</p>}

      {result.credentials && result.credentials.length > 0 && (
        <CredentialsReport credentials={result.credentials} />
      )}

      {result.errors.length > 0 && (
        <div className={styles.tableWrap}>
          <table className={styles.table}>
            <caption className={styles.caption}>Qabul qilinmagan qatorlar</caption>
            <thead>
              <tr>
                <th scope="col">Qator</th>
                <th scope="col">Ustun</th>
                <th scope="col">Qiymat</th>
                <th scope="col">Xato</th>
              </tr>
            </thead>
            <tbody>
              {result.errors.map((row, index) => (
                <tr key={`${row.row}-${row.column}-${index}`}>
                  <td className={styles.num}>{row.row}</td>
                  <td>{row.column}</td>
                  <td className={styles.value}>{row.value ?? '—'}</td>
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

/** CSV maydoni: vergul/qo'shtirnoq/yangi qator bo'lsa qo'shtirnoqqa olinadi. */
const csvCell = (v: string) => (/[",\n;]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v);

/**
 * v3.34 — import paytida yaratilgan vaqtinchalik parollar (faqat shu javobda — serverda xesh).
 * Admin jadvalni nusxalaydi yoki CSV yuklab oladi va talabalarga tarqatadi.
 */
function CredentialsReport({ credentials }: { credentials: ImportCredential[] }) {
  const [copied, setCopied] = useState(false);
  const header = ['FISH', 'HEMIS ID', 'Parol'];
  const rows = credentials.map((c) => [c.fullName, c.hemisId, c.password]);

  async function copyAll() {
    const tsv = [header, ...rows].map((r) => r.join('\t')).join('\n');
    setCopied(await copyText(tsv));
  }

  function downloadCsv() {
    // BOM — Excel o'zbekcha harflarni to'g'ri ochishi uchun.
    const csv = '\uFEFF' + [header, ...rows].map((r) => r.map(csvCell).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = 'talabalar-parollar.csv';
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 0);
  }

  return (
    <div className={styles.credentials}>
      <p className={styles.warn} role="note">
        Ilovaga kirish parollari faqat hozir ko‘rsatiladi — oynani yopishdan oldin nusxalang yoki
        yuklab oling. Talaba birinchi kirishda parolini almashtiradi va yuzini topshiradi.
      </p>
      <div className={styles.tools}>
        <Button type="button" size="xs" onClick={() => void copyAll()}>
          Jadvalni nusxalash
        </Button>
        <Button type="button" size="xs" onClick={downloadCsv}>
          CSV yuklab olish
        </Button>
        {copied && (
          <span role="status" className={styles.success}>
            Nusxalandi
          </span>
        )}
      </div>
      <div className={styles.tableWrap}>
        <table className={styles.table}>
          <caption className={styles.caption}>Ilovaga kirish ma‘lumotlari</caption>
          <thead>
            <tr>
              <th scope="col">Qator</th>
              <th scope="col">FISH</th>
              <th scope="col">HEMIS ID</th>
              <th scope="col">Parol</th>
            </tr>
          </thead>
          <tbody>
            {credentials.map((c) => (
              <tr key={`${c.row}-${c.hemisId}`}>
                <td className={styles.num}>{c.row}</td>
                <td>{c.fullName}</td>
                <td className={styles.value}>{c.hemisId}</td>
                <td className={styles.value}>{c.password}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
