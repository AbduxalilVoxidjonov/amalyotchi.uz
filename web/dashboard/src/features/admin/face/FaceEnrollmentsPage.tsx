import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Button, Card, EmptyState, Pill, PillGroup } from '@/shared/ui';
import { QueryState } from '../components/QueryState';
import { mutationErrorMessage } from '../errors';
import { fmtDate, fmtTime } from '../format';
import { PhotoPreview } from '../students/components/PhotoPreview';
import { FaceStatusBadge } from './components/FaceStatusBadge';
import { RejectFaceModal } from './components/RejectFaceModal';
import { useFaceAction, useFaceEnrollmentsQuery } from './hooks';
import { FACE_TABS, isFaceTab, type FaceEnrollment, type FaceEnrollmentTab } from './types';
import styles from './FaceEnrollmentsPage.module.css';

const EMPTY: Record<FaceEnrollmentTab, { title: string; description: string }> = {
  pending: {
    title: "Tekshiruvni kutayotgan yuz rasmi yo'q",
    description: 'Talaba etalon selfi yuborganda shu yerda ko‘rinadi.',
  },
  approved: { title: "Tasdiqlangan yuz rasmlari yo'q", description: '' },
  rejected: { title: "Rad etilgan yuz rasmlari yo'q", description: '' },
};

const fmtDateTime = (iso: string | null) => (iso ? `${fmtDate(iso)} ${fmtTime(iso)}` : '—');

/**
 * Tyutor · Yuz tasdiqlash (`/tutor/face`, kontrakt §6.33) — talabalarning etalon yuz rasmlari.
 * Tab (`?status=`) bo'yicha ro'yxat; "Kutilmoqda" da — Tasdiqlash va Rad etish (sabab majburiy).
 * Tasdiqlangan rasm keyingi check-in selfilari bilan solishtiriladi.
 */
export function FaceEnrollmentsPage() {
  const [params, setParams] = useSearchParams();
  const raw = params.get('status');
  const tab: FaceEnrollmentTab = isFaceTab(raw) ? raw : 'pending';
  const list = useFaceEnrollmentsQuery({ status: tab });
  const action = useFaceAction();
  const [rejecting, setRejecting] = useState<FaceEnrollment | null>(null);
  /** Qaysi qatorda amal bajarilmoqda (tugmalar faqat shu qatorda bloklanadi). */
  const [busyId, setBusyId] = useState<string | null>(null);
  const [rowError, setRowError] = useState<{ id: string; message: string } | null>(null);

  const selectTab = (next: FaceEnrollmentTab) => {
    const sp = new URLSearchParams(params);
    if (next === 'pending') sp.delete('status');
    else sp.set('status', next);
    setParams(sp);
    setRowError(null);
  };

  const approve = (item: FaceEnrollment) => {
    setBusyId(item.studentId);
    setRowError(null);
    action.mutate(
      { kind: 'approve', studentId: item.studentId },
      {
        onError: (e) => setRowError({ id: item.studentId, message: mutationErrorMessage(e) }),
        onSettled: () => setBusyId(null),
      },
    );
  };

  const reject = (reason: string) => {
    if (!rejecting) return;
    const id = rejecting.studentId;
    setBusyId(id);
    action.mutate(
      { kind: 'reject', studentId: id, reason },
      {
        onSuccess: () => setRejecting(null),
        onSettled: () => setBusyId(null),
      },
    );
  };

  return (
    <div className={styles.page}>
      <PillGroup role="tablist" aria-label="Yuz rasmi holati">
        {FACE_TABS.map((t) => (
          <Pill
            key={t.value}
            role="tab"
            shape="tab"
            active={tab === t.value}
            count={tab === t.value ? (list.data?.items.length ?? '') : ''}
            onClick={() => selectTab(t.value)}
          >
            {t.label}
          </Pill>
        ))}
      </PillGroup>

      <QueryState
        status={list.status}
        data={list.data}
        error={list.error}
        refetch={list.refetch}
        isEmpty={(d) => d.items.length === 0}
        empty={
          <EmptyState title={EMPTY[tab].title} description={EMPTY[tab].description || undefined} />
        }
      >
        {(data) => (
          <ul className={styles.grid} aria-label="Yuz rasmlari ro'yxati">
            {data.items.map((item) => {
              const busy = busyId === item.studentId;
              return (
                <li key={item.studentId}>
                  <Card
                    as="article"
                    className={styles.card}
                    aria-label={`Yuz rasmi: ${item.fullName}`}
                  >
                    <div className={styles.photo}>
                      {item.photoUrl ? (
                        <PhotoPreview
                          url={item.photoUrl}
                          label={`${item.fullName} etalon yuz rasmi`}
                          size="tile"
                        />
                      ) : (
                        <div className={styles.noPhoto}>Rasm yo'q</div>
                      )}
                    </div>
                    <div className={styles.body}>
                      <div className={styles.head}>
                        <Link
                          to={`/tutor/students/${encodeURIComponent(item.studentId)}`}
                          className={styles.name}
                        >
                          {item.fullName}
                        </Link>
                        <FaceStatusBadge status={item.status} />
                      </div>
                      <div className={styles.meta}>
                        {item.group} · HEMIS {item.hemisId}
                      </div>
                      <div className={styles.meta}>Yuborilgan: {fmtDateTime(item.submittedAt)}</div>
                      {item.reviewedAt && (
                        <div className={styles.meta}>
                          Ko'rib chiqilgan: {fmtDateTime(item.reviewedAt)}
                        </div>
                      )}
                      {item.rejectReason && (
                        <p className={styles.reason}>
                          <span className={styles.reasonLabel}>Sabab: </span>
                          {item.rejectReason}
                        </p>
                      )}
                      {rowError?.id === item.studentId && (
                        <p className={styles.error} role="alert">
                          {rowError.message}
                        </p>
                      )}
                      {item.status === 'pending' && (
                        <div className={styles.actions}>
                          <Button
                            size="sm"
                            variant="primary"
                            onClick={() => approve(item)}
                            disabled={busy}
                          >
                            Tasdiqlash
                          </Button>
                          <Button
                            size="sm"
                            variant="danger"
                            onClick={() => {
                              action.reset();
                              setRejecting(item);
                            }}
                            disabled={busy}
                          >
                            Rad etish
                          </Button>
                        </div>
                      )}
                    </div>
                  </Card>
                </li>
              );
            })}
          </ul>
        )}
      </QueryState>

      <RejectFaceModal
        // key — har talaba uchun sabab maydoni toza ochiladi.
        key={rejecting?.studentId ?? 'none'}
        open={rejecting !== null}
        studentName={rejecting?.fullName ?? ''}
        pending={action.isPending}
        error={rejecting && action.isError ? mutationErrorMessage(action.error) : undefined}
        onSubmit={reject}
        onClose={() => setRejecting(null)}
      />
    </div>
  );
}

export default FaceEnrollmentsPage;
