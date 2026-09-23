import { useId, useRef, useState, type FormEvent } from 'react';
import { Badge, Button, Chip, ChipRow, Input, Textarea } from '@/shared/ui';
import { isApiError } from '@/shared/api/client';
import {
  DIARY_FILE_ACCEPT,
  DIARY_MAX_FILES,
  DIARY_MIN_CHARS,
  DIARY_PDF_REQUIRED_MESSAGE,
  isPdfFile,
  type DiaryCreate,
  type DiaryFileDto,
} from '../types';
import styles from './DiaryForm.module.css';

export interface DiaryFormProps {
  /** Kartochka sarlavhasi: "Bugungi kundalik" (bosh ekran) · "Yangi yozuv" (kundaligim). */
  title: string;
  minChars?: number;
  maxFiles?: number;
  /** Sozlama `diaryPdfRequired` — true bo'lsa kamida bitta PDF shart. */
  pdfRequired?: boolean;
  /** Qayta yozilayotgan (bugungi `rewrite`) yozuvning mavjud fayllari — ulardagi PDF ham hisob. */
  existingFiles?: readonly DiaryFileDto[];
  /** Bugun allaqachon yuborilgan — sarlavha yonida badge. */
  submittedToday?: boolean;
  pending: boolean;
  /** Mutation xatosi (ApiError → maydon xatolari ham ajratiladi). */
  error: unknown;
  onSubmit: (input: DiaryCreate) => Promise<unknown>;
}

/** SPEC-SCREENS §8 o'ng section (yuqori qismi) — kundalik yozish formasi (presentation + lokal holat). */
export function DiaryForm({
  title,
  minChars = DIARY_MIN_CHARS,
  maxFiles = DIARY_MAX_FILES,
  pdfRequired = false,
  existingFiles = [],
  submittedToday = false,
  pending,
  error,
  onSubmit,
}: DiaryFormProps) {
  const headingId = useId();
  const fileRef = useRef<HTMLInputElement>(null);
  const [text, setText] = useState('');
  const [learned, setLearned] = useState('');
  const [files, setFiles] = useState<File[]>([]);
  const [touched, setTouched] = useState(false);
  const [sent, setSent] = useState(false);
  const [attempted, setAttempted] = useState(false);

  const length = text.trim().length;
  const tooShort = length < minChars;
  const tooManyFiles = files.length > maxFiles;
  const hasPdf = files.some(isPdfFile) || existingFiles.some(isPdfFile);
  const pdfMissing = pdfRequired && !hasPdf;
  const apiErr = isApiError(error) ? error : null;
  const textError =
    (touched && tooShort && `Kamida ${minChars} belgi yozing (hozir ${length}).`) ||
    apiErr?.fieldError('text') ||
    undefined;
  const filesError =
    (tooManyFiles && `Ko'pi bilan ${maxFiles} ta fayl.`) ||
    (attempted && pdfMissing && DIARY_PDF_REQUIRED_MESSAGE) ||
    apiErr?.fieldError('files') ||
    undefined;
  const formError = apiErr && apiErr.kind !== 'validation' ? apiErr.message : null;

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setTouched(true);
    setAttempted(true);
    setSent(false);
    if (tooShort || tooManyFiles || pdfMissing) return;
    try {
      await onSubmit({ text: text.trim(), learned, files });
      setText('');
      setLearned('');
      setFiles([]);
      setTouched(false);
      setAttempted(false);
      setSent(true);
    } catch {
      /* xato `error` prop orqali ko'rsatiladi */
    }
  }

  function addFiles(list: FileList | null) {
    if (!list) return;
    setFiles((prev) => [...prev, ...Array.from(list)].slice(0, maxFiles + 1));
    if (fileRef.current) fileRef.current.value = '';
  }

  return (
    <form className={styles.form} onSubmit={handleSubmit} aria-labelledby={headingId} noValidate>
      <div className={styles.head}>
        <h2 id={headingId} className={styles.title}>
          {title}
        </h2>
        <div className={styles.headRight}>
          {submittedToday && (
            <Badge status="ok" size="sm">
              Bugun yuborilgan
            </Badge>
          )}
          <span className={styles.count} aria-live="polite">
            {length} / {minChars} belgi
          </span>
        </div>
      </div>

      <Textarea
        variant="diary"
        aria-label="Kundalik matni"
        placeholder={`Bugun bajarilgan ishlar — kamida ${minChars} belgi`}
        value={text}
        onChange={(e) => setText(e.target.value)}
        onBlur={() => setTouched(true)}
        error={textError}
        wrapperClassName={styles.textarea}
        required
      />
      <Input
        variant="diary"
        aria-label="O'rganilgan yangilik"
        placeholder="O'rganilgan yangilik (ixtiyoriy)"
        value={learned}
        onChange={(e) => setLearned(e.target.value)}
        wrapperClassName={styles.learned}
      />

      {files.length > 0 && (
        <ChipRow className={styles.files}>
          {files.map((f, i) => (
            <Chip key={`${f.name}-${i}`}>
              {f.name}
              <button
                type="button"
                className={styles.removeFile}
                aria-label={`${f.name} ni olib tashlash`}
                onClick={() => setFiles((prev) => prev.filter((_, j) => j !== i))}
              >
                ×
              </button>
            </Chip>
          ))}
        </ChipRow>
      )}
      {filesError && (
        <p className={styles.fieldError} role="alert">
          {filesError}
        </p>
      )}

      <div className={styles.row}>
        <input
          ref={fileRef}
          type="file"
          multiple
          hidden
          accept={DIARY_FILE_ACCEPT}
          onChange={(e) => addFiles(e.target.files)}
          aria-label="Fayl tanlash"
        />
        <Button
          variant="dashed"
          radius="md2"
          onClick={() => fileRef.current?.click()}
          disabled={files.length >= maxFiles}
        >
          Fayl qo'shish ({files.length}–{maxFiles})
        </Button>
        {pdfRequired && (
          <Badge status={hasPdf ? 'ok' : 'late'} size="sm" aria-live="polite">
            {hasPdf ? 'PDF biriktirilgan' : 'PDF hisobot majburiy'}
          </Badge>
        )}
        <Button
          type="submit"
          variant="primary"
          radius="md2"
          className={styles.submit}
          disabled={pending}
          aria-busy={pending || undefined}
        >
          {pending ? 'Yuborilmoqda…' : 'Yuborish'}
        </Button>
      </div>

      {formError && (
        <p className={styles.formError} role="alert">
          {formError}
        </p>
      )}
      {sent && !error && (
        <p className={styles.success} role="status">
          Kundalik yuborildi.
        </p>
      )}
    </form>
  );
}
