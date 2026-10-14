import { useState } from 'react';
import { isApiError } from '@/shared/api';
import {
  Badge,
  Button,
  Card,
  CardBody,
  CardHeader,
  FactGrid,
  Input,
  Modal,
  type FactItem,
} from '@/shared/ui';
import { fmtDateOnly } from '../../shared/fmt';
import { ServerErrorBanner } from '../../practice-periods/components/ServerErrorBanner';
import { useSetStudentWorkHours } from '../hooks';
import type { AdminStudentDetail, StudentWorkHours } from '../types';
import {
  addDays,
  formatHoursRange,
  hhmm,
  isWorkHoursPending,
  validateWorkHours,
  type WorkHoursErrors,
} from '../workHours';
import styles from './StudentWorkHoursCard.module.css';

/** Bugun admin belgilagan vaqt amaldami: kutilayotgan o'zgarish bo'lsa — davr vaqtidan farq qilsa. */
function customToday(wh: StudentWorkHours, pending: boolean): boolean {
  const periodRange = formatHoursRange(wh.periodStart, wh.periodEnd);
  return pending
    ? Boolean(periodRange) && formatHoursRange(wh.todayStart, wh.todayEnd) !== periodRange
    : wh.start !== null;
}

export interface StudentWorkHoursCardProps {
  workHours: StudentWorkHours;
  /** Toshkent bo'yicha bugun ("2026-10-14"). */
  today: string;
  /** Berilsa — sarlavhada "O'zgartirish" tugmasi. */
  onEdit?: (() => void) | undefined;
}

/**
 * Admin · talaba profilidagi "Ish vaqti" kartasi (v3.32): bugun amaldagi kelish–ketish vaqti, manba
 * (admin belgilagan / davr vaqti), davr vaqti va ertadan kuchga kiradigan o'zgarish. Ish vaqtini FAQAT
 * admin belgilaydi — talaba ilovada faqat ko'radi. Check-in oynasi, kechikish chegarasi va auto-close
 * shu vaqtdan hisoblanadi.
 */
export function StudentWorkHoursCard({ workHours: wh, today, onEdit }: StudentWorkHoursCardProps) {
  const pending = isWorkHoursPending(wh, today);
  const periodRange = formatHoursRange(wh.periodStart, wh.periodEnd);
  const custom = customToday(wh, pending);

  const items: FactItem[] = [
    { k: 'Bugun', v: formatHoursRange(wh.todayStart, wh.todayEnd) ?? '—' },
    {
      k: 'Manba',
      v: custom ? (
        <Badge status="info">Admin belgilagan</Badge>
      ) : (
        <Badge status="neu">Davr vaqti</Badge>
      ),
    },
    {
      k: 'Davr vaqti',
      v: periodRange ?? <span className={styles.none}>Davr biriktirilmagan</span>,
    },
  ];

  return (
    <Card as="section" aria-label="Ish vaqti">
      <CardHeader
        title="Ish vaqti"
        subtitle="Check-in oynasi, kechikish va avtomatik yopilish shu vaqtdan hisoblanadi"
        actions={
          onEdit && (
            <Button size="sm" onClick={onEdit}>
              O'zgartirish
            </Button>
          )
        }
      />
      <CardBody>
        <FactGrid variant="detail" min={180} items={items} />
        {pending && wh.effectiveFrom && (
          <p className={styles.pending} role="note">
            {wh.start && wh.end
              ? `${fmtDateOnly(wh.effectiveFrom)} dan yangi vaqt: ${formatHoursRange(wh.start, wh.end)}.`
              : `${fmtDateOnly(wh.effectiveFrom)} dan davr vaqtiga qaytadi${periodRange ? ` (${periodRange})` : ''}.`}
          </p>
        )}
      </CardBody>
    </Card>
  );
}

export interface StudentWorkHoursModalProps {
  studentId: string;
  studentName: string;
  workHours: StudentWorkHours;
  today: string;
  onClose: () => void;
  /** Muvaffaqiyat: yangilangan profil va sahifa ko'rsatadigan xabar. */
  onDone: (detail: AdminStudentDetail, message: string) => void;
}

/**
 * Ish vaqtini belgilash modali (`PUT /api/admin/students/{id}/work-hours`). Klient validatsiyasi server
 * qoidalari bilan bir xil; server 400 `errors.Start|End` maydon ostida, boshqa xatolar banner bo'lib chiqadi.
 * "Davr vaqtiga qaytarish" — `{ start: null, end: null }`. Modal faqat ochilganda mount qilinadi.
 */
export function StudentWorkHoursModal({
  studentId,
  studentName,
  workHours: wh,
  today,
  onClose,
  onDone,
}: StudentWorkHoursModalProps) {
  const mutation = useSetStudentWorkHours(studentId);
  const [values, setValues] = useState({
    start: hhmm(wh.start ?? wh.periodStart ?? wh.todayStart),
    end: hhmm(wh.end ?? wh.periodEnd ?? wh.todayEnd),
  });
  const [errors, setErrors] = useState<WorkHoursErrors>({});
  const tomorrow = fmtDateOnly(addDays(today, 1));

  const resetting = mutation.isPending && mutation.variables?.start === null;
  const apiError = isApiError(mutation.error) ? mutation.error : null;
  const serverStart = apiError?.fieldError('start');
  const serverEnd = apiError?.fieldError('end');

  function set(key: 'start' | 'end', value: string) {
    setValues((v) => ({ ...v, [key]: value }));
    setErrors((e) => ({ ...e, [key]: undefined }));
    if (mutation.isError) mutation.reset();
  }

  function handleClose() {
    if (mutation.isPending) return;
    onClose();
  }

  function send(body: { start: string | null; end: string | null }, message: string) {
    mutation.mutate(body, {
      onSuccess: (detail) => {
        const from = detail.workHours?.effectiveFrom;
        onDone(
          detail,
          `${message} O'zgarish ertadan (${from ? fmtDateOnly(from) : tomorrow}) kuchga kiradi.`,
        );
      },
    });
  }

  function handleSubmit() {
    const next = validateWorkHours(values.start, values.end);
    setErrors(next);
    if (Object.keys(next).length > 0) return;
    send({ start: hhmm(values.start), end: hhmm(values.end) }, 'Ish vaqti saqlandi.');
  }

  return (
    <Modal
      open
      onClose={handleClose}
      title="Ish vaqtini belgilash"
      description={`${studentName} · davr vaqti: ${formatHoursRange(wh.periodStart, wh.periodEnd) ?? '—'}`}
      width="min(520px, 94vw)"
      footer={
        <>
          {wh.start !== null && (
            <Button
              type="button"
              disabled={mutation.isPending}
              onClick={() => send({ start: null, end: null }, 'Davr vaqtiga qaytarildi.')}
            >
              {resetting ? 'Qaytarilmoqda…' : 'Davr vaqtiga qaytarish'}
            </Button>
          )}
          <Button type="button" onClick={handleClose} disabled={mutation.isPending}>
            Bekor qilish
          </Button>
          <Button
            type="button"
            variant="primary"
            onClick={handleSubmit}
            disabled={mutation.isPending}
          >
            {mutation.isPending && !resetting ? 'Saqlanmoqda…' : 'Saqlash'}
          </Button>
        </>
      }
    >
      <div className={styles.body}>
        <ServerErrorBanner
          error={serverStart || serverEnd ? null : mutation.error}
          title="Saqlab bo'lmadi"
        />
        <div className={styles.fields}>
          <Input
            name="start"
            type="time"
            label="Kelish"
            variant="form"
            mono
            step={60}
            value={values.start}
            onChange={(e) => set('start', e.target.value)}
            error={errors.start ?? serverStart}
            disabled={mutation.isPending}
          />
          <Input
            name="end"
            type="time"
            label="Ketish"
            variant="form"
            mono
            step={60}
            value={values.end}
            onChange={(e) => set('end', e.target.value)}
            error={errors.end ?? serverEnd}
            disabled={mutation.isPending}
          />
        </div>
        <p className={styles.note}>
          O'zgarish ertadan ({tomorrow}) kuchga kiradi — bugungi check-in oynasi o'zgarmaydi. Ish
          vaqti kamida 1 soat.
        </p>
      </div>
    </Modal>
  );
}
