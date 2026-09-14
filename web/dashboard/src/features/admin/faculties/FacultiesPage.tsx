import { tableState, useListParams } from '../shared/useListParams';
import { FacultiesTable } from './components/FacultiesTable';
import { useFacultiesQuery } from './hooks';

/** Admin · Fakultetlar (SPEC-SCREENS §9.3). Container. */
export function FacultiesPage() {
  const list = useListParams();
  const query = useFacultiesQuery(list.params);
  return (
    <FacultiesTable
      {...tableState(list, query)}
      // TODO: "Yangi fakultet" formasi va Excel eksporti — dizaynda yo'q (❓), keyingi bosqich.
      onCreate={() => undefined}
      onExport={() => undefined}
    />
  );
}

export default FacultiesPage;
