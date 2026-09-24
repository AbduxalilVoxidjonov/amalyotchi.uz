/**
 * Mock: qaysi talabaning paroli bor (`hasPassword`). Admin (`s1`…) va tyutor (`s-341030`…) mock
 * id'lari alohida. Boshlang'ich holat: bittasi bor, qolganlari yo'q (ikkala belgini ko'rsatish uchun).
 * Faqat mock'lar va testlar uchun — importsiz modul (tutor/admin mock'lari bilan aylanma import bo'lmasin).
 */
const INITIAL = ['s1', 's-341030'];

const withPassword = new Set<string>(INITIAL);

export function hasMockStudentPassword(studentId: string): boolean {
  return withPassword.has(studentId);
}

export function setMockStudentPassword(studentId: string) {
  withPassword.add(studentId);
}

export function resetStudentPasswordMock() {
  withPassword.clear();
  for (const id of INITIAL) withPassword.add(id);
}
