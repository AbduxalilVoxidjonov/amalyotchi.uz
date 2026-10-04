import { useState } from 'react';
import { Button, Modal, Textarea } from '@/shared/ui';
import { FACE_REJECT_REASON_REQUIRED } from '../types';

export interface RejectFaceModalProps {
  open: boolean;
  studentName: string;
  pending: boolean;
  /** Server xatosi (400/409) — modal ichida ko'rsatiladi. */
  error?: string | undefined;
  onSubmit: (reason: string) => void;
  onClose: () => void;
}

/** Etalon yuz rasmini rad etish — sabab majburiy (talabaga ko'rsatiladi, u qayta yuboradi). */
export function RejectFaceModal({
  open,
  studentName,
  pending,
  error,
  onSubmit,
  onClose,
}: RejectFaceModalProps) {
  const [reason, setReason] = useState('');
  const [localError, setLocalError] = useState<string | undefined>(undefined);

  const close = () => {
    setReason('');
    setLocalError(undefined);
    onClose();
  };

  const submit = () => {
    const trimmed = reason.trim();
    if (!trimmed) {
      setLocalError(FACE_REJECT_REASON_REQUIRED);
      return;
    }
    setLocalError(undefined);
    onSubmit(trimmed);
  };

  return (
    <Modal
      open={open}
      onClose={close}
      title="Yuz rasmini rad etish"
      description={`${studentName} — sabab talabaga ko'rsatiladi, u yangi rasm yuboradi.`}
      width="460px"
      footer={
        <>
          <Button type="button" onClick={close} disabled={pending}>
            Bekor qilish
          </Button>
          <Button type="button" variant="danger" onClick={submit} disabled={pending}>
            {pending ? 'Bajarilmoqda…' : 'Rad etish'}
          </Button>
        </>
      }
    >
      <Textarea
        label="Rad etish sababi"
        variant="form"
        rows={3}
        maxLength={500}
        value={reason}
        placeholder="Masalan: yuz to'liq ko'rinmaydi, yorug' joyda qayta suratga oling"
        error={localError ?? error}
        onChange={(e) => {
          setReason(e.target.value);
          if (localError) setLocalError(undefined);
        }}
      />
    </Modal>
  );
}
