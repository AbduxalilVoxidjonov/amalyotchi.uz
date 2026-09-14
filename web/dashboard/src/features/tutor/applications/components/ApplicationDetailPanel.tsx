import {
  Avatar,
  Badge,
  Button,
  Card,
  Eyebrow,
  FactGrid,
  FileBox,
  MapPlaceholder,
  Textarea,
} from '@/shared/ui';
import { fmtBytes, fmtPhone, fmtTin } from '../../format';
import {
  APPLICATION_STATUS_LABEL,
  CHECKLIST_ITEMS,
  type ApplicationDecision,
  type ApplicationDetail,
} from '../types';
import { Checklist } from './Checklist';
import { RadiusStepper } from './RadiusStepper';
import styles from './ApplicationDetailPanel.module.css';

export interface ApplicationDetailPanelProps {
  detail: ApplicationDetail;
  radiusM: number;
  onRadiusChange: (next: number) => void;
  checked: ReadonlySet<number>;
  onToggleCheck: (index: number) => void;
  comment: string;
  onCommentChange: (next: string) => void;
  onDecision: (decision: ApplicationDecision) => void;
  pending: boolean;
  error?: string | undefined;
}

/** `companyDetails` → FactGrid kataklari (Korxona · STIR · Faoliyat turi · Manzil · Rahbar · Mentor). */
function companyFacts(detail: ApplicationDetail): { k: string; v: string }[] {
  const c = detail.companyDetails;
  const person = (name: string | null, phone: string | null) =>
    name ? (phone ? `${name} · ${fmtPhone(phone)}` : name) : '—';
  return [
    { k: 'Korxona', v: c.name },
    { k: 'STIR', v: fmtTin(c.tin) },
    { k: 'Faoliyat turi', v: c.activity },
    { k: 'Manzil', v: c.address },
    { k: 'Rahbar', v: person(c.supervisorName, c.supervisorPhone) },
    { k: 'Mentor', v: person(c.mentorName, c.mentorPhone) },
  ];
}

/** SPEC-SCREENS §4 — o'ng panel (presentation). */
export function ApplicationDetailPanel({
  detail,
  radiusM,
  onRadiusChange,
  checked,
  onToggleCheck,
  comment,
  onCommentChange,
  onDecision,
  pending,
  error,
}: ApplicationDetailPanelProps) {
  const s = APPLICATION_STATUS_LABEL[detail.status];
  // Hal qilingan ariza (409) — faqat ko'rish. Tuzatishga qaytarilgan ariza talaba qayta yuborguncha kutadi.
  const decided = detail.status !== 'submitted';
  const coords = `${detail.coords.lat.toFixed(4)}, ${detail.coords.lng.toFixed(4)}`;
  const contract = detail.contract;
  return (
    <Card as="article" aria-label={`Ariza: ${detail.name}`}>
      <header className={styles.head}>
        <div className={styles.headLeft}>
          <Avatar name={detail.name} variant="detail" />
          <div>
            <h2 className={styles.name}>{detail.name}</h2>
            <div className={styles.meta}>
              {detail.group} · HEMIS {detail.hemisId} · {detail.course}-kurs
              {detail.revisionCount > 0 && ` · ${detail.revisionCount} marta qaytarilgan`}
            </div>
          </div>
        </div>
        <Badge status={s.kind} size="md">
          {s.label}
        </Badge>
      </header>

      {/* min 200: 6 katak → 3 ustun (2 qator to'liq), bo'sh kataklar qolmaydi */}
      <FactGrid variant="detail" min={200} items={companyFacts(detail)} className={styles.facts} />

      <div className={styles.columns}>
        <section>
          <Eyebrow>Geofence nuqtasi</Eyebrow>
          <MapPlaceholder
            title="Xarita (o'rniga real karta qo'yiladi)"
            coords={coords}
            height={186}
          />
          <RadiusStepper value={radiusM} onChange={onRadiusChange} />
        </section>
        <section>
          <Eyebrow>Tekshirish ro'yxati</Eyebrow>
          <Checklist items={CHECKLIST_ITEMS} checked={checked} onToggle={onToggleCheck} />
          {contract ? (
            <FileBox
              className={styles.file}
              name={contract.name}
              meta={
                <>
                  {contract.pages !== null && `${contract.pages} bet · `}
                  {fmtBytes(contract.sizeBytes)} ·{' '}
                  <a href={contract.url} target="_blank" rel="noreferrer">
                    brauzerda ochish
                  </a>
                </>
              }
            />
          ) : (
            <FileBox className={styles.file} name="Shartnoma yuklanmagan" meta="fayl yo'q" />
          )}
        </section>
      </div>

      <div className={styles.comment}>
        <Textarea
          id={`app-comment-${detail.id}`}
          variant="form"
          label="Izoh"
          hint={
            detail.comment
              ? `Oldingi izoh: ${detail.comment}`
              : 'Qaytarish / rad etishda majburiy — talabaga ko‘rsatiladi'
          }
          rows={2}
          value={comment}
          disabled={pending || decided}
          onChange={(e) => onCommentChange(e.target.value)}
        />
      </div>

      <footer className={styles.footer}>
        <Button
          variant="primary"
          disabled={pending || decided}
          onClick={() => onDecision('approve')}
        >
          Tasdiqlash
        </Button>
        <Button disabled={pending || decided} onClick={() => onDecision('return')}>
          Tuzatishga qaytarish
        </Button>
        <Button variant="danger" disabled={pending || decided} onClick={() => onDecision('reject')}>
          Rad etish
        </Button>
        <span
          className={styles.note}
          role={error ? 'alert' : undefined}
          data-error={error ? 'true' : undefined}
        >
          {error ?? 'Har bir qaror audit jurnaliga yoziladi'}
        </span>
      </footer>
    </Card>
  );
}
