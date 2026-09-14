import { useEffect, useState } from 'react';

/** Qiymat `delay` ms davomida o'zgarmasa — yangilanadi (qidiruv inputi uchun). */
export function useDebouncedValue<T>(value: T, delay = 300): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(t);
  }, [value, delay]);
  return debounced;
}
