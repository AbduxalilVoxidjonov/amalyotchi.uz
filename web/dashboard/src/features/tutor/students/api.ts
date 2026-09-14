import { api } from '@/shared/api';
import type { TutorStudent } from './types';

/** Tyutor · Talabalarim — `TutorStudentsController`: GET /api/tutor/students → TutorStudent[] */
export const studentsApi = {
  list: () => api.get<TutorStudent[]>('/api/tutor/students'),
};
