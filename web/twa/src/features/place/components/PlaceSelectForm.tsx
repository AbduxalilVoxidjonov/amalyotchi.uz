import { useId, useState, type FormEvent } from 'react';
import { Button, Card, Input } from '@/shared/ui';
import { errorMessage, isApiError } from '@/shared/api/client';
import { formatPhone } from '@/shared/lib/format';
import { useCompanyLookup, useSubmitPlace } from '../hooks';
import {
  TIN_DIGITS,
  TIN_FORMAT_MESSAGE,
  formatTin,
  onlyTinDigits,
  type CompanyLookupDto,
} from '../types';
import styles from './PlaceSelectForm.module.css';

export interface PlaceSelectFormProps {
  /** `revisionNeeded`/`rejected` dan keyin qayta yuborish — sarlavha boshqacha. */
  resubmit?: boolean;
}

const person = (name: string | null, phone: string | null) =>
  name ? (phone ? `${name} · ${formatPhone(phone)}` : name) : '—';

/**
 * Amaliyot joyini STIR orqali tanlash. Talaba korxona ma'lumotini QO'LDA kiritmaydi:
 * `GET /api/companies/lookup` topib beradi, `POST /api/student/place` esa faqat `{ tin }` yuboradi.
 */
export function PlaceSelectForm({ resubmit = false }: PlaceSelectFormProps) {
  const headingId = useId();
  const [tin, setTin] = useState('');
  const [found, setFound] = useState<CompanyLookupDto | null>(null);
  const [formatError, setFormatError] = useState('');

  const lookup = useCompanyLookup();
  const submit = useSubmitPlace();

  const lookupError = lookup.error ? errorMessage(lookup.error) : null;
  const submitApiError = isApiError(submit.error) ? submit.error : null;
  const submitError = submit.error
    ? (submitApiError?.fieldError('tin') ?? errorMessage(submit.error))
    : null;

  function handleTinChange(value: string) {
    setTin(onlyTinDigits(value));
    setFormatError('');
    setFound(null);
    lookup.reset();
    submit.reset();
  }

  async function handleLookup(e: FormEvent) {
    e.preventDefault();
    setFound(null);
    submit.reset();
    if (tin.length !== TIN_DIGITS) {
      lookup.reset();
      setFormatError(TIN_FORMAT_MESSAGE);
      return;
    }
    setFormatError('');
    try {
      setFound(await lookup.mutateAsync(tin));
    } catch {
      /* xabar `lookup.error` orqali ko'rsatiladi */
    }
  }

  async function handleSubmit() {
    try {
      await submit.mutateAsync(tin);
    } catch {
      /* xabar `submit.error` orqali ko'rsatiladi */
    }
  }

  const facts = found
    ? [
        { k: 'Korxona', v: found.name },
        { k: 'STIR', v: formatTin(found.tin) },
        { k: 'Faoliyat turi', v: found.activity },
        { k: 'Manzil', v: found.address },
        { k: 'Rahbar', v: person(found.supervisorName, found.supervisorPhone) },
        { k: 'Mentor', v: person(found.mentorName, found.mentorPhone) },
        { k: 'Koordinata', v: `${found.lat.toFixed(4)}, ${found.lng.toFixed(4)}` },
        { k: 'Geofence radiusi', v: `${found.radiusM} m` },
      ]
    : [];

  return (
    <Card padded="form" as="section" aria-labelledby={headingId}>
      <h2 id={headingId} className={styles.title}>
        {resubmit ? 'Amaliyot joyini qayta tanlash' : 'Amaliyot joyini tanlash'}
      </h2>
      <p className={styles.lead}>
        Korxona ma'lumotini o'zingiz yozmaysiz — STIR raqamini kiriting, qolgani tizimdan topiladi.
      </p>

      <form onSubmit={handleLookup} noValidate>
        <div className={styles.row}>
          <Input
            variant="form"
            mono
            label="Korxona STIR raqami"
            placeholder="123456789"
            inputMode="numeric"
            autoComplete="off"
            maxLength={TIN_DIGITS}
            value={tin}
            onChange={(e) => handleTinChange(e.target.value)}
            error={formatError || undefined}
            wrapperClassName={styles.tinField}
          />
          <Button
            type="submit"
            radius="md2"
            className={styles.search}
            disabled={lookup.isPending}
            aria-busy={lookup.isPending || undefined}
          >
            {lookup.isPending ? 'Qidirilmoqda…' : 'Qidirish'}
          </Button>
        </div>
      </form>

      {lookupError && !found && (
        <p className={styles.formError} role="alert">
          {lookupError}
        </p>
      )}

      {found && (
        <div className={styles.result} aria-label="Topilgan korxona">
          <dl className={styles.fields}>
            {facts.map((f) => (
              <div key={f.k} className={styles.field}>
                <dt className={styles.k}>{f.k}</dt>
                <dd className={styles.v}>{f.v}</dd>
              </div>
            ))}
          </dl>
          <div className={styles.resultFoot}>
            <Button
              variant="primary"
              radius="md2"
              block
              disabled={submit.isPending}
              aria-busy={submit.isPending || undefined}
              onClick={() => void handleSubmit()}
            >
              {submit.isPending ? 'Yuborilmoqda…' : 'Tasdiqlash va yuborish'}
            </Button>
            {submitError && (
              <p className={styles.formError} role="alert">
                {submitError}
              </p>
            )}
            {submit.isSuccess && (
              <p className={styles.success} role="status">
                Ariza tyutorga yuborildi — ko'rib chiqilmoqda.
              </p>
            )}
          </div>
        </div>
      )}

      <p className={styles.note}>
        STIR topilmasa, korxona hali tizimga kiritilmagan — tyutoringizga murojaat qiling.
      </p>
    </Card>
  );
}
