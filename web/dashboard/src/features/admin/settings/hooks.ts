import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminKeys } from '../shared/keys';
import { settingsApi } from './api';
import type { SettingsUpdate } from './types';

export function useSettingsQuery() {
  return useQuery({ queryKey: adminKeys.settings(), queryFn: settingsApi.get });
}

export function useUpdateSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: ['admin', 'settings', 'update'],
    mutationFn: (body: SettingsUpdate) => settingsApi.update(body),
    onSuccess: (data) => {
      queryClient.setQueryData(adminKeys.settings(), data);
      void queryClient.invalidateQueries({ queryKey: adminKeys.settings() });
    },
  });
}
