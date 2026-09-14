import { tableState, useListParams } from '../shared/useListParams';
import { GroupsTable } from './components/GroupsTable';
import { useGroupsQuery } from './hooks';

/** Admin · Guruhlar (SPEC-SCREENS §9.4). Container. */
export function GroupsPage() {
  const list = useListParams();
  const query = useGroupsQuery(list.params);
  return (
    <GroupsTable
      {...tableState(list, query)}
      // TODO: forma/amallar dizaynda yo'q (❓) — hozircha no-op.
      onCreate={() => undefined}
      onMoveCourse={() => undefined}
      onArchive={() => undefined}
    />
  );
}

export default GroupsPage;
