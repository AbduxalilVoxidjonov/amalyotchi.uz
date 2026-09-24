import { describe, expect, it } from 'vitest';
import { passwordResetSchema } from '@/features/shared/password/schema';
import { studentCredentialsText } from './credentials';
import { AMBIGUOUS_CHARS, GENERATED_PASSWORD_LENGTH, generatePassword } from './generatePassword';

describe('generatePassword', () => {
  it("10 belgi, o'xshash belgilarsiz, harf+katta harf+raqam bor va validatsiyadan o'tadi", () => {
    for (let i = 0; i < 200; i++) {
      const p = generatePassword();
      expect(p).toHaveLength(GENERATED_PASSWORD_LENGTH);
      for (const ch of AMBIGUOUS_CHARS) expect(p).not.toContain(ch);
      expect(p).toMatch(/[a-z]/);
      expect(p).toMatch(/[A-Z]/);
      expect(p).toMatch(/[2-9]/);
      expect(p).toMatch(/^[A-Za-z2-9]+$/);
      expect(passwordResetSchema.safeParse({ password: p, confirm: p }).success).toBe(true);
    }
  });

  it('har chaqiriqda boshqacha', () => {
    const set = new Set(Array.from({ length: 50 }, () => generatePassword()));
    expect(set.size).toBe(50);
  });
});

describe('studentCredentialsText', () => {
  it("TWA manzili bo'lsa Kirish qo'shiladi, bo'lmasa tushiriladi", () => {
    expect(studentCredentialsText('341030', 'Abc234defg', 'https://app.example.uz')).toBe(
      'HEMIS ID: 341030 · Parol: Abc234defg · Kirish: https://app.example.uz',
    );
    expect(studentCredentialsText('341030', 'Abc234defg', null)).toBe(
      'HEMIS ID: 341030 · Parol: Abc234defg',
    );
  });
});
