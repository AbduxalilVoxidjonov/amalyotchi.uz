import { useEffect, useRef } from 'react';
import { Checkbox } from '@/shared/ui';
import styles from './SelectBox.module.css';

export interface SelectBoxProps {
  checked: boolean;
  indeterminate?: boolean;
  /** Matnli label yo'q (jadval ustuni) — nom `aria-label` orqali beriladi. */
  label: string;
  onChange: (checked: boolean) => void;
}

/**
 * Jadvaldagi belgilash katagi (talabalar, xabar oluvchilar). `indeterminate` — sahifadagi qatorlarning
 * bir qismi belgilanganda sarlavha katagi uchun.
 */
export function SelectBox({ checked, indeterminate = false, label, onChange }: SelectBoxProps) {
  const ref = useRef<HTMLInputElement>(null);
  // `indeterminate` — faqat DOM xossasi, atribut orqali berib bo'lmaydi.
  useEffect(() => {
    if (ref.current) ref.current.indeterminate = indeterminate;
  }, [indeterminate]);

  return (
    <Checkbox
      ref={ref}
      wrapperClassName={styles.check}
      label=""
      aria-label={label}
      checked={checked}
      onChange={(e) => onChange(e.target.checked)}
    />
  );
}

/**
 * Qatordagi katakcha o'rami (`data-row-click-ignore`): katakcha atrofidagi bo'sh joy bosilganda ham
 * qator navigatsiyasi bo'lmaydi.
 */
export function SelectCell(props: SelectBoxProps) {
  return (
    <div className={styles.checkCell} data-row-click-ignore>
      <SelectBox {...props} />
    </div>
  );
}

/**
 * Belgilash ustuni kengligi. Katak padding'i: chapda `--row-pad-x` (birinchi ustun), o'ngda 12px
 * (`--col-gap`). Trek = padding + 15px katakcha — aks holda kontent qutisi katakchadan tor bo'lib,
 * qatorlarda u `cellText` ning `overflow: hidden` i bilan kesiladi.
 */
export const SELECT_COLUMN_WIDTH = 'calc(var(--row-pad-x) + 15px + 12px)';
