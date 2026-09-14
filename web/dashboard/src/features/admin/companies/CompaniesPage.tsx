import { tableState, useListParams } from '../shared/useListParams';
import { CompaniesTable } from './components/CompaniesTable';
import { useCompaniesQuery } from './hooks';

/** Admin · Korxonalar (SPEC-SCREENS §9.7). Container. */
export function CompaniesPage() {
  const list = useListParams();
  const query = useCompaniesQuery(list.params);
  return (
    <CompaniesTable
      {...tableState(list, query)}
      // TODO: eksport va "Shubhali to'planishlar" filtri dizaynda yo'q (❓) — hozircha no-op.
      onExport={() => undefined}
      onShowSuspicious={() => undefined}
    />
  );
}

export default CompaniesPage;
