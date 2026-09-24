/**
 * O'qilishi oson tasodifiy parol: o'xshash belgilarsiz (0/O/o, 1/l/I/i yo'q), kamida bitta
 * kichik harf, katta harf va raqam. `crypto.getRandomValues` — rejection sampling bilan (modulo og'ishisiz).
 */
const LOWER = 'abcdefghjkmnpqrstuvwxyz';
const UPPER = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
const DIGITS = '23456789';
const ALPHABET = LOWER + UPPER + DIGITS;

export const GENERATED_PASSWORD_LENGTH = 10;

/** O'xshash (chalkashadigan) belgilar — generator ularni hech qachon ishlatmaydi. */
export const AMBIGUOUS_CHARS = '0Oo1lIi';

function randomIndex(max: number): number {
  // 256 ning `max` ga karrali eng katta qismidan tashqaridagi baytlar tashlanadi.
  const limit = 256 - (256 % max);
  const buf = new Uint8Array(1);
  for (;;) {
    crypto.getRandomValues(buf);
    const v = buf[0]!;
    if (v < limit) return v % max;
  }
}

const pick = (chars: string) => chars[randomIndex(chars.length)]!;

export function generatePassword(length = GENERATED_PASSWORD_LENGTH): string {
  const chars = [pick(LOWER), pick(UPPER), pick(DIGITS)];
  while (chars.length < length) chars.push(pick(ALPHABET));
  // Fisher–Yates: majburiy belgilar doim boshida turmasin.
  for (let i = chars.length - 1; i > 0; i--) {
    const j = randomIndex(i + 1);
    [chars[i], chars[j]] = [chars[j]!, chars[i]!];
  }
  return chars.join('');
}
