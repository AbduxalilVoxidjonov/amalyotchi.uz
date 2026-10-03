import { api } from '@/shared/api/client';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import type { StudentProfileDto, StudentWorkHoursDto, UpdateWorkHoursRequest } from './types';

export const profileApi = {
  get: (signal?: AbortSignal) =>
    api.get<StudentProfileDto>(STUDENT_ENDPOINTS.profile, signal ? { signal } : {}),
  updateWorkHours: (body: UpdateWorkHoursRequest) =>
    api.put<StudentWorkHoursDto>(STUDENT_ENDPOINTS.profileWorkHours, body),
};
