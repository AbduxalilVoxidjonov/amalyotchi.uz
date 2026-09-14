import { tableState, useListParams } from '../shared/useListParams';
import { TutorsTable } from './components/TutorsTable';
import { useTutorsQuery } from './hooks';

/** Admin · Tyutorlar (SPEC-SCREENS §9.5). Container. */
export function TutorsPage() {
  const list = useListParams();
  const query = useTutorsQuery(list.params);
  return (
    <TutorsTable
      {...tableState(list, query)}
      // TODO: forma/havola yuborish dizaynda yo'q (❓) — hozircha no-op.
      onCreate={() => undefined}
      onSendInvite={() => undefined}
    />
  );
}

export default TutorsPage;
