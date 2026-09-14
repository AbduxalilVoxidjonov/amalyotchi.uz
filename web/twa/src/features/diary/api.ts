import { api } from '@/shared/api/client';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import type { DiaryCreate, DiaryEntryDto } from './types';

export const diaryApi = {
  list: (signal?: AbortSignal) =>
    api.get<DiaryEntryDto[]>(STUDENT_ENDPOINTS.diary, signal ? { signal } : {}),
  create: (input: DiaryCreate) => {
    // Fayllar bilan — multipart (api client FormData'ni o'zgarishsiz yuboradi).
    const form = new FormData();
    form.append('text', input.text);
    if (input.learned.trim()) form.append('learned', input.learned.trim());
    for (const f of input.files) form.append('files', f, f.name);
    return api.post<DiaryEntryDto>(STUDENT_ENDPOINTS.diary, form);
  },
};
