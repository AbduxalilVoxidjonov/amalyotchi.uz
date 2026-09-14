import { api } from '@/shared/api/client';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import type { LeaveRequestCreate, LeaveRequestDto } from './types';

export const leaveApi = {
  list: (signal?: AbortSignal) =>
    api.get<LeaveRequestDto[]>(STUDENT_ENDPOINTS.leaveRequests, signal ? { signal } : {}),
  create: (body: LeaveRequestCreate) =>
    api.post<LeaveRequestDto>(STUDENT_ENDPOINTS.leaveRequests, body),
};
