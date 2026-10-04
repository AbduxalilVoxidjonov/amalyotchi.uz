import { Checkbox } from '@/shared/ui';
import styles from './Checklist.module.css';

export interface ChecklistProps {
  items: readonly string[];
  checked: ReadonlySet<number>;
  onToggle: (index: number) => void;
}

/** SPEC-SCREENS §4 — tekshirish ro'yxati (7 punkt), holat lokal. */
export function Checklist({ items, checked, onToggle }: ChecklistProps) {
  return (
    <div className={styles.list}>
      {items.map((label, i) => (
        <Checkbox
          key={label}
          label={label}
          checked={checked.has(i)}
          onChange={() => onToggle(i)}
          wrapperClassName={styles.item}
        />
      ))}
    </div>
  );
}
