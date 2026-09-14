import { useQuery } from '@tanstack/react-query';
import { tutorKeys } from '../query-keys';
import { studentsApi } from './api';

export function useMyStudentsQuery() {
  return useQuery({ queryKey: tutorKeys.students(), queryFn: studentsApi.list });
}
