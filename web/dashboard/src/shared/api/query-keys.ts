/**
 * TanStack Query kalitlari konvensiyasi: `[feature, entity, params?]`.
 * Har feature o'z `keys` obyektini shu shaklda e'lon qiladi, masalan:
 *
 *   export const studentKeys = {
 *     all: ['students'] as const,
 *     list: (params: ListParams) => ['students', 'list', params] as const,
 *     detail: (id: string) => ['students', 'detail', id] as const,
 *   };
 *
 * Invalidatsiya: `queryClient.invalidateQueries({ queryKey: studentKeys.all })`.
 */
export const authKeys = {
  all: ['auth'] as const,
  me: () => ['auth', 'me'] as const,
};
