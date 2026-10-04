import { useState } from 'react';
import {
  Button,
  Card,
  CardBody,
  CardHeader,
  ConfirmDialog,
  EmptyState,
  FactGrid,
  type FactItem,
} from '@/shared/ui';
import { mutationErrorMessage } from '../../errors';
import { fmtDate, fmtTime } from '../../format';
import { PhotoPreview } from '../../students/components/PhotoPreview';
import { useFaceAction } from '../hooks';
import type { StudentFace } from '../types';
import { FaceStatusBadge } from './FaceStatusBadge';
import { RejectFaceModal } from './RejectFaceModal';
import styles from './StudentFaceCard.module.css';

const fmtDateTime = (iso: string | null) => (iso ? `${fmtDate(iso)} ${fmtTime(iso)}` : '—');

export interface StudentFaceCardProps {
  studentId: string;
  studentName: string;
  face: StudentFace;
  /**
   * Tasdiqlash/rad etish/bekor qilish amallari — faqat tyutor profilida (endpoint'lar
   * `/api/tutor/students/:id/face/*`). Admin profilida karta faqat ko'rish uchun.
   */
  canManage: boolean;
}

/**
 * Talaba profili · "Yuz" kartasi (kontrakt §6.33): etalon rasm, holat, yuborilgan/ko'rib chiqilgan
 * vaqt, rad sababi. `pending` — Tasdiqlash / Rad etish (sabab majburiy); `approved` — Bekor qilish
 * (tasdiq dialogi bilan; talaba yangi rasm yuborishi kerak bo'ladi).
 */
export function StudentFaceCard({ studentId, studentName, face, canManage }: StudentFaceCardProps) {
  const action = useFaceAction();
  const [rejectOpen, setRejectOpen] = useState(false);
  const [resetOpen, setResetOpen] = useState(false);
  const [inlineError, setInlineError] = useState<string | null>(null);

  const facts: FactItem[] = [
    { k: 'Holat', v: <FaceStatusBadge status={face.status} /> },
    { k: 'Yuborilgan', v: fmtDateTime(face.submittedAt) },
    { k: "Ko'rib chiqilgan", v: fmtDateTime(face.reviewedAt) },
    { k: 'Check-in tekshiruvi', v: face.required ? 'Yoqilgan' : "O'chirilgan" },
  ];

  const approve = () => {
    setInlineError(null);
    action.mutate(
      { kind: 'approve', studentId },
      { onError: (e) => setInlineError(mutationErrorMessage(e)) },
    );
  };

  return (
    <Card as="section" aria-label="Yuz">
      <CardHeader
        title="Yuz"
        subtitle="Etalon rasm — check-in selfilari shu bilan solishtiriladi"
      />
      <CardBody>
        {face.status === 'none' && !face.photoUrl ? (
          <EmptyState
            title="Etalon yuz rasmi yuborilmagan"
            description={
              face.required
                ? 'Talaba ilovaga kirganda avval yuz rasmini yuboradi.'
                : 'Yuzni tekshirish sozlamada o‘chirilgan.'
            }
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
              {inlineError && (
                <p className={styles.error} role="alert">
                  {inlineError}
                </p>
              )}
              {canManage && face.status === 'pending' && (
                <div className={styles.actions}>
                  <Button size="sm" variant="primary" onClick={approve} disabled={action.isPending}>
                    Tasdiqlash
                  </Button>
                  <Button
                    size="sm"
                    variant="danger"
                    onClick={() => {
                      action.reset();
                      setInlineError(null);
                      setRejectOpen(true);
                    }}
                    disabled={action.isPending}
                  >
                    Rad etish
                  </Button>
                </div>
              )}
              {canManage && face.status === 'approved' && (
                <div className={styles.actions}>
                  <Button
                    size="sm"
                    onClick={() => {
                      action.reset();
                      setResetOpen(true);
                    }}
                    disabled={action.isPending}
                  >
                    Bekor qilish
                  </Button>
                </div>
              )}
            </div>
          </div>
        )}
      </CardBody>

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
            title="Yuz rasmini bekor qilish"
            description={`${studentName} uchun tasdiqlangan etalon rasm o'chiriladi. Talaba keyingi kirishda yangi rasm yuborishi kerak bo'ladi.`}
            confirmLabel="Bekor qilish"
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
    </Card>
  );
}
