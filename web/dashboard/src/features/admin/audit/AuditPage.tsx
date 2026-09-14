import { tableState, useListParams } from '../shared/useListParams';
import { AuditTable } from './components/AuditTable';
import { useAuditQuery } from './hooks';

/** Admin · Audit jurnali (SPEC-SCREENS §9.8). Container. */
export function AuditPage() {
  const list = useListParams();
  const query = useAuditQuery(list.params);
  return (
    <AuditTable
      {...tableState(list, query)}
      // TODO: eksport va amal filtri dizaynda yo'q (❓) — hozircha no-op.
      onExport={() => undefined}
      onFilter={() => undefined}
    />
  );
}

export default AuditPage;
