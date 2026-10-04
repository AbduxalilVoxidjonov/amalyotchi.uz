import { useEffect, useState } from 'react';
import { copyText } from '@/features/shared/student-password/copyText';
import { Button, Modal } from '@/shared/ui';
import styles from '../Admins.module.css';

export interface TemporaryPasswordModalProps {
  /** null — yopiq. Parol faqat shu oynada bir marta ko'rsatiladi (serverda saqlanmaydi). */
  shown: { adminName: string; login: string; password: string } | null;
  title: string;
  onClose: () => void;
}

/** Yaratish yoki parol tiklashdan keyin vaqtinchalik parolni bir marta ko'rsatish (nusxalash bilan). */
export function TemporaryPasswordModal({ shown, title, onClose }: TemporaryPasswordModalProps) {
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    setCopied(false);
  }, [shown?.password]);

  useEffect(() => {
    if (!copied) return undefined;
    const t = setTimeout(() => setCopied(false), 2000);
    return () => clearTimeout(t);
  }, [copied]);

  return (
    <Modal
      open={shown !== null}
      onClose={onClose}
      title={title}
      description={
        shown ? `${shown.adminName} (login: ${shown.login}) uchun vaqtinchalik parol.` : undefined
      }
      width="420px"
      footer={
        <Button type="button" variant="primary" onClick={onClose}>
          Tayyor
        </Button>
      }
    >
      {shown && (
        <div className={styles.done}>
          <p className={styles.secret} data-testid="temporary-password">
            {shown.password}
          </p>
          <div className={styles.tools}>
            <Button
              type="button"
              size="xs"
              onClick={() => void copyText(shown.password).then((ok) => setCopied(ok))}
            >
              Nusxalash
            </Button>
            {copied && (
              <span role="status" className={styles.copied}>
                Nusxalandi
              </span>
            )}
          </div>
          <p className={styles.note}>
            Parol faqat hozir ko'rsatiladi — oynani yopgach uni qayta ko'rib bo'lmaydi. Adminga
            xavfsiz yo'l bilan bering; kirgach parolni Sozlamalarda almashtirish tavsiya etiladi.
          </p>
        </div>
      )}
    </Modal>
  );
}
