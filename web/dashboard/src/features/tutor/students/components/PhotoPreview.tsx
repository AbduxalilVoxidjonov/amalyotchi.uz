import { useState } from 'react';
import { Modal } from '@/shared/ui';
import { AuthImage } from './AuthImage';
import styles from './PhotoPreview.module.css';

/** Rasm yuklanguncha/xato holatida joy egallaydigan blok balandligi. */
const PLACEHOLDER_HEIGHT: Record<'thumb' | 'card' | 'wide', number> = {
  thumb: 54,
  card: 120,
  wide: 260,
};

export interface PhotoPreviewProps {
  /** `/api/files/{id}` */
  url: string;
  /** Rasm tavsifi (a11y + modal sarlavhasi), masalan "12.10.2026 check-in rasmi". */
  label: string;
  /** thumb (jadval katagi, 54px) · card (140px) · wide (ustun kengligida — kun oynasidagi fayllar). */
  size?: 'thumb' | 'card' | 'wide';
}

/** Token bilan yuklanadigan rasm + bosilganda kattalashtirish (Modal). */
export function PhotoPreview({ url, label, size = 'thumb' }: PhotoPreviewProps) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button
        type="button"
        className={styles.trigger}
        data-size={size}
        onClick={(e) => {
          // Jadval katagida — qator bosilishi (kun oynasi) bilan birga ochilmasin.
          e.stopPropagation();
          setOpen(true);
        }}
        aria-label={`${label} — kattalashtirish`}
      >
        <AuthImage src={url} alt={label} height={PLACEHOLDER_HEIGHT[size]} />
      </button>
      <Modal open={open} onClose={() => setOpen(false)} title={label} width="640px">
        <AuthImage src={url} alt={label} height={320} className={styles.full} />
      </Modal>
    </>
  );
}
