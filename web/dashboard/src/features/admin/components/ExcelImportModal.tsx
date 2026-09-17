import { useEffect, useId, useRef, useState, type ChangeEvent, type ReactNode } from 'react';
import type { UseMutationResult } from '@tanstack/react-query';
import { errorMessage } from '@/shared/api';
import { Button, Modal } from '@/shared/ui';
import type { ImportResult } from '../shared/types';
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
