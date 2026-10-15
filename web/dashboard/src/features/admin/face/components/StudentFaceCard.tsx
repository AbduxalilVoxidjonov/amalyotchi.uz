import { useState } from 'react';
import { Button, ConfirmDialog, EmptyState, FactGrid, type FactItem } from '@/shared/ui';
import { mutationErrorMessage } from '../../shared/errors';
import { fmtDate, fmtTime } from '../../shared/fmt';
import { PhotoPreview } from '../../student-profile/components/PhotoPreview';
import { useFaceAction } from '../hooks';
import type { StudentFace } from '../types';
import { FaceStatusBadge } from './FaceStatusBadge';
import { RejectFaceModal } from './RejectFaceModal';
import styles from './StudentFaceCard.module.css';

const fmtDateTime = (iso: string | null) => (iso ? `${fmtDate(iso)} ${fmtTime(iso)}` : '—');

/** Holatga qarab adminga qisqa izoh (v3.34 siyosati: talaba etalonni o'zi almashtira olmaydi). */
const STATUS_NOTE: Record<StudentFace['status'], string> = {
  none: 'Talaba yuzini ilovaga birinchi kirishda topshiradi.',
  pending:
    'Talaba rasmni yubordi — tasdiqlang yoki rad eting. Shu vaqtgacha check-in selfisi shu rasm bilan solishtiriladi.',
  approved: "Talaba etalonni o'zi almashtira olmaydi — kerak bo'lsa «Qayta olish».",
  rejected: 'Talaba keyingi kirishda yuzini qayta topshiradi.',
};

export interface StudentFacePanelProps {
  studentId: string;
  studentName: string;
  face: StudentFace;
  /**
   * Tasdiqlash/rad etish/qayta olish amallari (`/api/admin/students/:id/face/*`). false — faqat
   * ko'rish uchun.
   */
  canManage: boolean;
}

/**
 * Talaba profili · "Ilova" kartasidagi "Yuz etaloni" bo'limi (kontrakt §6.33, v3.34 siyosati):
 * etalon rasm, holat, yuborilgan/ko'rib chiqilgan vaqt, rad sababi. Talaba etalonni ilovaga birinchi
 * kirishda MAJBURIY topshiradi va o'zi almashtira olmaydi. `pending` — Tasdiqlash / Rad etish (sabab
 * majburiy) / Qayta olish; `approved` — Rad etish / Qayta olish (reset: etalon o'chiriladi, talaba
 * keyingi kirishda yuzini qayta topshiradi).
 */
export function StudentFacePanel({
  studentId,
  studentName,
  face,
  canManage,
}: StudentFacePanelProps) {
  const action = useFaceAction();
  const [rejectOpen, setRejectOpen] = useState(false);
  const [resetOpen, setResetOpen] = useState(false);
  const [inlineError, setInlineError] = useState<string | null>(null);

  const facts: FactItem[] = [
    { k: 'Holat', v: <FaceStatusBadge status={face.status} /> },
    { k: 'Yuborilgan', v: fmtDateTime(face.submittedAt) },
    { k: "Ko'rib chiqilgan", v: fmtDateTime(face.reviewedAt) },
    { k: 'Davomatda solishtirish', v: face.required ? 'Yoqilgan' : "O'chirilgan (sozlama)" },
  ];

  const approve = () => {
    setInlineError(null);
    action.mutate(
      { kind: 'approve', studentId },
      { onError: (e) => setInlineError(mutationErrorMessage(e)) },
    );
  };

  const openReject = () => {
    action.reset();
    setInlineError(null);
    setRejectOpen(true);
  };

  const openReset = () => {
    action.reset();
    setInlineError(null);
    setResetOpen(true);
  };

  const hasEnrollment = face.status !== 'none';

  return (
    <section className={styles.panel} aria-label="Yuz etaloni">
      <h3 className={styles.title}>Yuz etaloni</h3>
      {!hasEnrollment && !face.photoUrl ? (
        <EmptyState
          title="Etalon yuz rasmi topshirilmagan"
          description={`${STATUS_NOTE.none}${
            face.required ? '' : ' Davomatda solishtirish hozir sozlamada o‘chirilgan.'
          }`}
        />
      ) : (
        <div className={styles.layout}>
          <div className={styles.photo}>
            {face.photoUrl ? (
              <PhotoPreview
                url={face.photoUrl}
                label={`${studentName} etalon yuz rasmi`}
                size="tile"
              />
            ) : (
              <div className={styles.noPhoto}>Rasm yo'q</div>
            )}
          </div>
          <div className={styles.info}>
            <FactGrid variant="detail" min={170} items={facts} />
            {face.rejectReason && (
              <p className={styles.reason}>
                <span className={styles.reasonLabel}>Rad etish sababi: </span>
                {face.rejectReason}
              </p>
            )}
            <p className={styles.note}>{STATUS_NOTE[face.status]}</p>
            {inlineError && (
              <p className={styles.error} role="alert">
                {inlineError}
              </p>
            )}
            {canManage && (face.status === 'pending' || face.status === 'approved') && (
              <div className={styles.actions}>
                {face.status === 'pending' && (
                  <Button size="sm" variant="primary" onClick={approve} disabled={action.isPending}>
                    Tasdiqlash
                  </Button>
                )}
                <Button size="sm" variant="danger" onClick={openReject} disabled={action.isPending}>
                  Rad etish
                </Button>
                <Button size="sm" onClick={openReset} disabled={action.isPending}>
                  Qayta olish
                </Button>
              </div>
            )}
          </div>
        </div>
      )}

      {canManage && (
        <>
          <RejectFaceModal
            open={rejectOpen}
            studentName={studentName}
            pending={action.isPending}
            error={rejectOpen && action.isError ? mutationErrorMessage(action.error) : undefined}
            onSubmit={(reason) =>
              action.mutate(
                { kind: 'reject', studentId, reason },
                { onSuccess: () => setRejectOpen(false) },
              )
            }
            onClose={() => setRejectOpen(false)}
          />
          <ConfirmDialog
            open={resetOpen}
            title="Yuz etalonini qayta olish"
            description={`${studentName} ning etalon rasmi o'chiriladi. Talaba keyingi kirishda yuzini qayta topshiradi; shu vaqtgacha (solishtirish yoqilgan bo'lsa) davomat belgilay olmaydi.`}
            confirmLabel="Qayta olish"
            cancelLabel="Qaytish"
            danger
            isLoading={action.isPending}
            error={resetOpen && action.isError ? mutationErrorMessage(action.error) : undefined}
            onConfirm={() =>
              action.mutate({ kind: 'reset', studentId }, { onSuccess: () => setResetOpen(false) })
            }
            onCancel={() => setResetOpen(false)}
          />
        </>
      )}
    </section>
  );
}
