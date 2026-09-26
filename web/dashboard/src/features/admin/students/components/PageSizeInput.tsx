import { useId, useState, type KeyboardEvent } from 'react';
import { clampPageSize, MAX_STUDENT_PAGE_SIZE, MIN_STUDENT_PAGE_SIZE } from '../pageSize';
import styles from './PageSizeInput.module.css';

/** Tayyor variantlar (`<datalist>`) — istalgan boshqa son ham yozilishi mumkin. */
const PRESETS = [20, 50, 100, 200];

export interface PageSizeInputProps {
  value: number;
  onChange: (size: number) => void;
}

/**
 * "Sahifada: [n] ta" — qiymat faqat Enter yoki blur'da qo'llanadi (har harfda so'rov yuborilmaydi).
 * Noto'g'ri qiymat 1..500 ga qisiladi va maydon to'g'rilangan qiymatni ko'rsatadi.
 */
export function PageSizeInput({ value, onChange }: PageSizeInputProps) {
  const id = useId();
  const listId = `${id}-presets`;
  const [draft, setDraft] = useState(String(value));

  // Tashqaridan (URL, orqaga/oldinga) o'zgarsa — maydon moslanadi.
  const [prevValue, setPrevValue] = useState(value);
  if (prevValue !== value) {
    setPrevValue(value);
    setDraft(String(value));
  }

  function commit() {
    const next = clampPageSize(draft) ?? value;
    setDraft(String(next));
    if (next !== value) onChange(next);
  }

  function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter') {
      e.preventDefault();
      commit();
    } else if (e.key === 'Escape') {
      setDraft(String(value));
    }
  }

  return (
    <span className={styles.root}>
      <label htmlFor={id}>Sahifada:</label>
      <input
        id={id}
        className={styles.input}
        type="number"
        inputMode="numeric"
        min={MIN_STUDENT_PAGE_SIZE}
        max={MAX_STUDENT_PAGE_SIZE}
        step={1}
        list={listId}
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onKeyDown={handleKeyDown}
        onBlur={commit}
      />
      <datalist id={listId}>
        {PRESETS.map((n) => (
          <option key={n} value={n} />
        ))}
      </datalist>
      <span aria-hidden="true">ta</span>
    </span>
  );
}
