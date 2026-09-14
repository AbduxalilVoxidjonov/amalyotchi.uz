import { errorMessage, isApiError } from '@/shared/api';

/**
 * Mutation xatosi uchun matn: FluentValidation 400 (`errors`) bo'lsa — maydon xabarlari,
 * aks holda `ProblemDetails.detail` / umumiy matn.
 */
export function mutationErrorMessage(error: unknown): string {
  if (isApiError(error) && error.kind === 'validation') {
    const messages = Object.values(error.fieldErrors).flat();
    if (messages.length > 0) return messages.join(' ');
  }
  return errorMessage(error);
}
