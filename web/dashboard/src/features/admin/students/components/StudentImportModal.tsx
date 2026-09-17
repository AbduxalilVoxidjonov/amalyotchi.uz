import { useEffect, useId, useRef, useState, type ChangeEvent } from 'react';
import { errorMessage } from '@/shared/api';
import { Button, Modal } from '@/shared/ui';
import { useImportStudents, useStudentImportTemplate } from '../hooks';
import type { StudentImportResult } from '../types';
import styles from './StudentImportModal.module.css';

export interface StudentImportModalProps {
  open: boolean;
  onClose: () => void;
}

const ACCEPT = '.xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

/**
 * "Excel import" oqimi: shablonni yuklab olish → to'ldirish → yuklash → hisobot.
 * Server xato qatorlarni tashlab yuboradi va to'g'rilarini saqlaydi, shuning uchun hisobotda
 * qo'shilganlar soni ham, rad etilgan qatorlar ro'yxati ham ko'rsatiladi.
 */
export function StudentImportModal({ open, onClose }: StudentImportModalProps) {
  const fileId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [result, setResult] = useState<StudentImportResult | null>(null);

  const template = useStudentImportTemplate();
  const importStudents = useImportStudents();

  // Har ochilishda toza holat (oldingi hisobot qolib ketmasin).
  useEffect(() => {
    if (!open) return;
    setFile(null);
    setResult(null);
    importStudents.reset();
    if (inputRef.current) inputRef.current.value = '';
    // Faqat ochilish paytida; mutatsiya obyekti har renderda yangi.
  }, [open]);

  function handleFile(e: ChangeEvent<HTMLInputElement>) {
    setFile(e.target.files?.[0] ?? null);
    setResult(null);
    importStudents.reset();
  }

  function handleImport() {
    if (!file) return;
    importStudents.mutate(file, {
      onSuccess: (imported) => {
        setResult(imported);
        setFile(null);
        if (inputRef.current) inputRef.current.value = '';
      },
    });
  }

  function handleClose() {
    if (importStudents.isPending) return;
    onClose();
  }

  const error = importStudents.isError ? errorMessage(importStudents.error) : undefined;

  return (
    <Modal
      open={open}
      onClose={handleClose}
      title="Talabalarni Excel orqali qo'shish"
      description="Shablonni yuklab oling, to'ldiring va shu yerga yuklang. Xato qatorlar qabul qilinmaydi — ular ro'yxat bo'lib chiqadi, to'g'rilari saqlanadi."
      width="min(720px, 94vw)"
      footer={
        <>
          <Button type="button" onClick={handleClose} disabled={importStudents.isPending}>
            Yopish
          </Button>
          <Button
            type="button"
            variant="primary"
            onClick={handleImport}
            disabled={!file || importStudents.isPending}
          >
            {importStudents.isPending ? 'Yuklanmoqda…' : 'Import qilish'}
          </Button>
        </>
      }
    >
      <div className={styles.body}>
        <section className={styles.step}>
          <Button type="button" onClick={template.download} disabled={template.isLoading}>
            {template.isLoading ? 'Tayyorlanmoqda…' : 'Shablonni yuklab olish'}
          </Button>
          <p className={styles.hint}>
            Ustunlar: <b>FISH*</b>, <b>HEMIS ID*</b>, <b>Guruh*</b>, Telefon. Sarlavha nomlarini
            o‘zgartirmang — fayl ustun nomlari bo‘yicha o‘qiladi, tartibi muhim emas. Guruhlar
            ro‘yxati shablonning «Guruhlar» varag‘ida.
          </p>
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
            disabled={importStudents.isPending}
          />
        </div>

        {error && (
          <p role="alert" className={styles.error}>
            {error}
          </p>
        )}

        {result && <ImportReport result={result} />}
      </div>
    </Modal>
  );
}

function ImportReport({ result }: { result: StudentImportResult }) {
  return (
    <section className={styles.report} aria-label="Import natijasi">
      <p className={result.created > 0 ? styles.success : styles.error} role="status">
        Qo‘shildi: {result.created} · Qabul qilinmadi: {result.failed} · Jami: {result.totalRows}
      </p>

      {result.totalRows === 0 && (
        <p className={styles.hint}>Faylda talaba qatori topilmadi — shablonni to‘ldirganingizni tekshiring.</p>
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
