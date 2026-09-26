/**
 * Mijoz tekshiruvi — backend `ChangePasswordCommandValidator` + `TutorValidationRules`:
 * joriy parol bo'sh emas; yangi parol 8–128 belgi va joriydan farqli. Takror — faqat frontend.
 */
export const PASSWORD_MIN_LENGTH = 8;
export const PASSWORD_MAX_LENGTH = 128;

export interface PasswordFormValues {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

export type PasswordFormErrors = Partial<Record<keyof PasswordFormValues, string>>;

export const EMPTY_PASSWORD_FORM: PasswordFormValues = {
  currentPassword: '',
  newPassword: '',
  confirmPassword: '',
};

export function validatePasswordForm(v: PasswordFormValues): PasswordFormErrors {
  const errors: PasswordFormErrors = {};
  if (!v.currentPassword) errors.currentPassword = 'Joriy parolni kiriting.';

  if (!v.newPassword) errors.newPassword = 'Yangi parolni kiriting.';
  else if (v.newPassword.length < PASSWORD_MIN_LENGTH || v.newPassword.length > PASSWORD_MAX_LENGTH)
    errors.newPassword = `Parol ${PASSWORD_MIN_LENGTH}–${PASSWORD_MAX_LENGTH} ta belgidan iborat bo'lishi kerak.`;
  else if (v.currentPassword && v.newPassword === v.currentPassword)
    errors.newPassword = 'Yangi parol joriy paroldan farq qilishi kerak.';

  if (!v.confirmPassword) errors.confirmPassword = 'Yangi parolni takrorlang.';
  else if (v.newPassword && v.confirmPassword !== v.newPassword)
    errors.confirmPassword = 'Parollar mos kelmadi.';

  return errors;
}
