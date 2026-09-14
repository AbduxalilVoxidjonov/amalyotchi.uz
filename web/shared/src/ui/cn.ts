export type ClassValue =
  string | number | null | undefined | false | ClassValue[] | Record<string, boolean | undefined>;

/** Class nomlarini shartli birlashtirish (clsx'ning kichik varianti). */
export function cn(...inputs: ClassValue[]): string {
  const out: string[] = [];
  for (const input of inputs) {
    if (!input) continue;
    if (typeof input === 'string' || typeof input === 'number') {
      out.push(String(input));
    } else if (Array.isArray(input)) {
      const nested = cn(...input);
      if (nested) out.push(nested);
    } else {
      for (const [key, on] of Object.entries(input)) if (on) out.push(key);
    }
  }
  return out.join(' ');
}
