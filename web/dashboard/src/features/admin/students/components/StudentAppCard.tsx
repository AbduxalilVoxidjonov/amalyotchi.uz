import { useState } from 'react';
import { StudentPasswordModal } from '@/features/shared/student-password';
import { Badge, Button, Card, CardBody, CardHeader, FactGrid, type FactItem } from '@/shared/ui';
import { StudentFacePanel } from '../../face/components/StudentFaceCard';
import { fmtDate, fmtTime } from '../../shared/fmt';
import type { AdminStudentDetail } from '../types';
import { StudentHemisIdModal } from './StudentHemisIdModal';
import styles from './StudentAppCard.module.css';

type PasswordState = 'none' | 'temporary' | 'own' | 'set';

const PASSWORD_LABEL: Record<
  PasswordState,
  { label: string; kind: 'ok' | 'late' | 'bad' | 'neu' }
> = {
  none: { label: "Parol yo'q", kind: 'bad' },
  temporary: { label: 'Vaqtinchalik', kind: 'late' },
  own: { label: 'Talaba o‘zgartirgan', kind: 'ok' },
  // Eski server `mustChangePassword` yubormaydi — faqat "o'rnatilgan".
  set: { label: "Parol o'rnatilgan", kind: 'ok' },
};

const PASSWORD_NOTE: Record<PasswordState, string> = {
  none: 'Talaba ilovaga kira olmaydi — vaqtinchalik parol bering.',
  temporary: 'Talaba birinchi kirishda parolini almashtiradi.',
  own: 'Talaba parolini unutsa — yangi vaqtinchalik parol bering (eski parol ishlamay qoladi).',
  set: 'Talaba parolini unutsa — yangi vaqtinchalik parol bering (eski parol ishlamay qoladi).',
};

function passwordState(detail: AdminStudentDetail): PasswordState {
  if (!detail.hasPassword) return 'none';
  if (detail.mustChangePassword === undefined) return 'set';
  return detail.mustChangePassword ? 'temporary' : 'own';
}

/**
 * Talaba profili · "Ilova" kartasi (v3.34): mobil ilovaga kirish bilan bog'liq hamma narsa bir
 * joyda — login (HEMIS ID, o'zgartirish mumkin), parol holati (yo'q / vaqtinchalik / talaba o'zgartirgan) va "Parol
 * berish" (`POST /api/admin/students/{id}/password`), oxirgi kirish, yuz etaloni (rasm, holat,
 * tasdiqlash / rad etish / qayta olish). Talaba yuzini ilovaga birinchi kirishda majburiy topshiradi.
 */
export function StudentAppCard({
  detail,
  onMessage,
}: {
  detail: AdminStudentDetail;
  /** Muvaffaqiyat xabari (sahifa flash'i). */
  onMessage?: (message: string) => void;
}) {
  const [passwordOpen, setPasswordOpen] = useState(false);
  const [hemisOpen, setHemisOpen] = useState(false);
  const state = passwordState(detail);
  const password = PASSWORD_LABEL[state];

  const items: FactItem[] = [
    { k: 'Login (HEMIS ID)', v: <span className={styles.login}>{detail.hemisId}</span> },
    { k: 'Parol', v: <Badge status={password.kind}>{password.label}</Badge> },
  ];
  if (detail.lastLoginAt !== undefined) {
    items.push({
      k: 'Oxirgi kirish',
      v: detail.lastLoginAt ? (
        `${fmtDate(detail.lastLoginAt)} ${fmtTime(detail.lastLoginAt)}`
      ) : (
        <span className={styles.never}>Hali kirmagan</span>
      ),
    });
  }

  return (
    <Card as="section" aria-label="Ilova">
      <CardHeader title="Ilova" subtitle="Talaba mobil ilovaga HEMIS ID va parol bilan kiradi" />
      <CardBody>
        <div className={styles.body}>
          <section className={styles.access} aria-label="Ilovaga kirish">
            <h3 className={styles.title}>Kirish</h3>
            <FactGrid variant="detail" min={170} items={items} />
            <p className={state === 'none' ? styles.warn : styles.note}>{PASSWORD_NOTE[state]}</p>
            <div className={styles.actions}>
              <Button
                size="sm"
                variant={state === 'none' ? 'primary' : 'secondary'}
                onClick={() => setPasswordOpen(true)}
              >
                {state === 'none' ? 'Parol berish' : 'Parolni tiklash'}
              </Button>
              <Button size="sm" onClick={() => setHemisOpen(true)}>
                HEMIS ID ni o'zgartirish
              </Button>
            </div>
          </section>
          {detail.face && (
            <>
              <hr className={styles.divider} />
              <StudentFacePanel
                studentId={detail.id}
                studentName={detail.name}
                face={detail.face}
                canManage
              />
            </>
          )}
        </div>
      </CardBody>
      <StudentPasswordModal
        open={passwordOpen}
        student={detail}
        onClose={() => setPasswordOpen(false)}
      />
      {hemisOpen && (
        <StudentHemisIdModal
          student={detail}
          onClose={() => setHemisOpen(false)}
          onDone={(hemisId) => {
            setHemisOpen(false);
            onMessage?.(
              `HEMIS ID o'zgartirildi: ${hemisId}. Talaba ilovaga endi shu login bilan kiradi.`,
            );
          }}
        />
      )}
    </Card>
  );
}
