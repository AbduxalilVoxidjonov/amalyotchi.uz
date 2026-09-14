import { useId, useRef, useState, type FormEvent } from 'react';
import { Button, Card, Chip, Input, Textarea } from '@/shared/ui';
import { isApiError } from '@/shared/api/client';
import { parseUserDate } from '@/shared/lib/format';
import { LEAVE_REASON_MIN, type LeaveRequestCreate } from '../types';
import styles from './LeaveRequestForm.module.css';

export interface LeaveRequestFormProps {
  pending: boolean;
  error: unknown;
  onSubmit: (input: LeaveRequestCreate) => Promise<unknown>;
}

/**
 * SPEC-SCREENS §15 forma. ❓ Sana — dizayndagi kabi mono matn `14.10.2026` (native `type=date`
 * placeholder ko'rsatmaydi); `parseUserDate` bilan tekshiriladi va ISO ga o'giriladi.
 */
export function LeaveRequestForm({ pending, error, onSubmit }: LeaveRequestFormProps) {
  const headingId = useId();
  const fileRef = useRef<HTMLInputElement>(null);
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [reason, setReason] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [submitted, setSubmitted] = useState(false);
  const [sent, setSent] = useState(false);

  const fromIso = parseUserDate(from);
  const toIso = parseUserDate(to);
  const clientErrors = {
    dateFrom: !from
      ? 'Boshlanish sanasi majburiy.'
      : !fromIso
        ? 'Sana KK.OO.YYYY ko‘rinishida.'
        : '',
    dateTo: !to
      ? 'Tugash sanasi majburiy.'
      : !toIso
        ? 'Sana KK.OO.YYYY ko‘rinishida.'
        : fromIso && toIso < fromIso
          ? 'Tugash sanasi boshlanishdan oldin bo‘lishi mumkin emas.'
          : '',
    reason:
      reason.trim().length < LEAVE_REASON_MIN
        ? `Sababni kamida ${LEAVE_REASON_MIN} belgi bilan yozing.`
        : '',
  };
  const apiErr = isApiError(error) ? error : null;
  const fieldError = (name: keyof typeof clientErrors) =>
    (submitted && clientErrors[name]) || apiErr?.fieldError(name) || undefined;
  const formError = apiErr && apiErr.kind !== 'validation' ? apiErr.message : null;

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setSubmitted(true);
    setSent(false);
    if (clientErrors.dateFrom || clientErrors.dateTo || clientErrors.reason || !fromIso || !toIso)
      return;
    try {
      await onSubmit({
        dateFrom: fromIso,
        dateTo: toIso,
        reason: reason.trim(),
        ...(file ? { attachmentName: file.name } : {}),
      });
      setFrom('');
      setTo('');
      setReason('');
      setFile(null);
      setSubmitted(false);
      setSent(true);
    } catch {
      /* `error` prop orqali */
    }
  }

  return (
    <Card padded="form" as="section" aria-labelledby={headingId}>
      <h2 id={headingId} className={styles.title}>
        Yangi ruxsat so'rovi
      </h2>
      <form onSubmit={handleSubmit} noValidate>
        <div className={styles.dates}>
          <Input
            variant="form"
            mono
            label="Boshlanish sanasi"
            placeholder="14.10.2026"
            inputMode="numeric"
            autoComplete="off"
            value={from}
            onChange={(e) => setFrom(e.target.value)}
            error={fieldError('dateFrom')}
          />
          <Input
            variant="form"
            mono
            label="Tugash sanasi"
            placeholder="14.10.2026"
            inputMode="numeric"
            autoComplete="off"
            value={to}
            onChange={(e) => setTo(e.target.value)}
            error={fieldError('dateTo')}
          />
        </div>
        <Textarea
          variant="form"
          label="Sabab"
          placeholder="Sababni yozing"
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          error={fieldError('reason')}
          wrapperClassName={styles.reason}
        />

        <div className={styles.row}>
          <input
            ref={fileRef}
            type="file"
            hidden
            accept="image/*,.pdf"
            aria-label="Tasdiqlovchi hujjat tanlash"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          />
          {file ? (
            <Chip>
              {file.name}
              <button
                type="button"
                className={styles.removeFile}
                aria-label="Hujjatni olib tashlash"
                onClick={() => setFile(null)}
              >
                ×
              </button>
            </Chip>
          ) : (
            <Button variant="dashed" radius="md2" onClick={() => fileRef.current?.click()}>
              Tasdiqlovchi hujjat
            </Button>
          )}
          <Button
            type="submit"
            variant="primary"
            radius="md2"
            className={styles.submit}
            disabled={pending}
            aria-busy={pending || undefined}
          >
            {pending ? 'Yuborilmoqda…' : 'Tyutorga yuborish'}
          </Button>
        </div>

        {formError && (
          <p className={styles.formError} role="alert">
            {formError}
          </p>
        )}
        {sent && !error && (
          <p className={styles.success} role="status">
            So'rov tyutorga yuborildi.
          </p>
        )}
        <p className={styles.note}>
          Tasdiqlangan ruxsat kuni "Sababli" bo'ladi va davomat foiziga zarar yetkazmaydi.
        </p>
      </form>
    </Card>
  );
}
