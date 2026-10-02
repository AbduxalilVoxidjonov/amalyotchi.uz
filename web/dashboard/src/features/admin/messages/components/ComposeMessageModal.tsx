import { useEffect, useId, useRef, useState } from 'react';
import { errorMessage } from '@/shared/api';
import { Button, Checkbox, Modal, Textarea } from '@/shared/ui';
import { formatCount } from '../../shared/format';
import { needsConfirm, targetCount, toAudience, type ComposeTarget } from '../audience';
import { useSendMessage } from '../hooks';
import { MESSAGE_TEXT_MAX, type MessageSummary } from '../types';
import styles from './ComposeMessageModal.module.css';

export interface ComposeMessageModalProps {
  target: ComposeTarget;
  onClose: () => void;
  /** Xabar navbatga qo'yildi (201). */
  onSent: (summary: MessageSummary) => void;
}

/** "Kimga" qatori — rejimga qarab. */
function recipientLabel(target: ComposeTarget): string {
  switch (target.kind) {
    case 'single':
      return target.recipient.fullName;
    case 'selected':
      return `Tanlangan: ${formatCount(target.userIds.length)} ta`;
    case 'filter':
      return `Filtr bo'yicha: ${target.description} — taxminan ${formatCount(target.estimate)} ta talaba`;
    case 'all':
      return `Barcha ulanganlar — ${formatCount(target.estimate)} ta`;
  }
}

/**
 * Xabar yozish oynasi (bitta komponent — bitta talaba / tanlanganlar / filtr / hammaga).
 * Matn (≤ 4000, bo'sh bo'lsa yuborilmaydi), "Ilovani ochish" tugmasi (sukut — yoqiq), Telegram
 * pufagiga o'xshash oldindan ko'rish (oddiy matn, `pre-wrap`). Ommaviy yoki 20 dan ko'p kishiga
 * yuborishda ikkinchi qadam — tasdiq.
 */
export function ComposeMessageModal({ target, onClose, onSent }: ComposeMessageModalProps) {
  const send = useSendMessage();
  const [text, setText] = useState('');
  const [attachAppButton, setAttachAppButton] = useState(true);
  const [step, setStep] = useState<'compose' | 'confirm'>('compose');
  const confirmRef = useRef<HTMLButtonElement>(null);
  const previewId = useId();

  const count = targetCount(target);
  const trimmed = text.trim();
  const canSend = trimmed.length > 0 && text.length <= MESSAGE_TEXT_MAX && !send.isPending;

  // Tasdiq qadamiga o'tganda textarea yo'qoladi — fokus tasdiq tugmasiga (klaviatura bilan davom etish).
  useEffect(() => {
    if (step === 'confirm') confirmRef.current?.focus();
  }, [step]);

  function submit() {
    if (!canSend) return;
    send.mutate(
      { text: trimmed, attachAppButton, audience: toAudience(target) },
      { onSuccess: onSent },
    );
  }

  function handlePrimary() {
    if (!canSend) return;
    if (step === 'compose' && needsConfirm(target)) {
      send.reset();
      setStep('confirm');
      return;
    }
    submit();
  }

  const error = send.isError ? (
    <p role="alert" className={styles.error}>
      {errorMessage(send.error)}
    </p>
  ) : null;

  const footer =
    step === 'confirm' ? (
      <>
        <Button type="button" onClick={() => setStep('compose')} disabled={send.isPending}>
          Orqaga
        </Button>
        <Button
          ref={confirmRef}
          type="button"
          variant="primary"
          onClick={submit}
          disabled={!canSend}
        >
          {send.isPending ? 'Yuborilmoqda…' : 'Ha, yuborish'}
        </Button>
      </>
    ) : (
      <>
        <Button type="button" onClick={onClose} disabled={send.isPending}>
          Bekor qilish
        </Button>
        <Button type="button" variant="primary" onClick={handlePrimary} disabled={!canSend}>
          {send.isPending ? 'Yuborilmoqda…' : 'Yuborish'}
        </Button>
      </>
    );

  return (
    <Modal
      open
      onClose={() => {
        if (!send.isPending) onClose();
      }}
      title={step === 'confirm' ? 'Yuborishni tasdiqlang' : 'Xabar yozish'}
      width="600px"
      footer={footer}
    >
      {step === 'confirm' ? (
        <div className={styles.body}>
          <p className={styles.confirm}>
            <strong>{formatCount(count)} ta</strong> talabaga yuboriladi. Davom etasizmi?
          </p>
          <p className={styles.confirmTarget}>{recipientLabel(target)}</p>
          {error}
        </div>
      ) : (
        <div className={styles.body}>
          <div className={styles.to}>
            <span className={styles.toLabel}>Kimga</span>
            <span className={styles.toValue} data-testid="compose-target">
              {recipientLabel(target)}
            </span>
          </div>

          <Textarea
            label="Xabar matni"
            hint={`${text.length}/${MESSAGE_TEXT_MAX}`}
            variant="form"
            rows={6}
            maxLength={MESSAGE_TEXT_MAX}
            value={text}
            placeholder="Talabalarga yuboriladigan matn…"
            onChange={(e) => setText(e.target.value)}
          />

          <Checkbox
            label="Ilovani ochish tugmasini qo'shish"
            checked={attachAppButton}
            onChange={(e) => setAttachAppButton(e.target.checked)}
          />

          <section className={styles.preview} aria-labelledby={`${previewId}-title`}>
            <h3 id={`${previewId}-title`} className={styles.previewTitle}>
              Oldindan ko'rish
            </h3>
            <div className={styles.chat}>
              <div className={styles.bubble}>
                {trimmed ? (
                  <p className={styles.bubbleText} data-testid="compose-preview">
                    {text}
                  </p>
                ) : (
                  <p className={styles.bubblePlaceholder}>Matn shu yerda ko'rinadi</p>
                )}
                {attachAppButton && (
                  <span className={styles.appButton} aria-hidden="true">
                    Ilovani ochish
                  </span>
                )}
              </div>
            </div>
          </section>
          {error}
        </div>
      )}
    </Modal>
  );
}
