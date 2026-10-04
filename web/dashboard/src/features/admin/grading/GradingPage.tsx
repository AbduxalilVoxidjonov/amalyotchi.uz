import { useMemo, useState } from 'react';
import { QueryState } from '../components/QueryState';
import { mutationErrorMessage } from '../errors';
import { GradingTable } from './components/GradingTable';
import { useGradingQuery, useGradingUpdate } from './hooks';

/** Tyutor · Baholash (SPEC-SCREENS §9.2) — container. */
export function GradingPage() {
  const query = useGradingQuery();
  const update = useGradingUpdate();
  const [search, setSearch] = useState('');

  const rows = useMemo(() => {
    const q = search.trim().toLowerCase();
    const all = query.data ?? [];
    return q ? all.filter((r) => r.name.toLowerCase().includes(q)) : all;
  }, [query.data, search]);

  // Tavsiya bilan farq qiladigan (yoki hali qo'yilmagan) qatorlar.
  const toAccept = rows.filter(
    (r) =>
      r.tutorPoints !== r.recommended.tutorPoints ||
      r.referencePoints !== r.recommended.referencePoints,
  );

  return (
    <QueryState status={query.status} data={query.data} error={query.error} refetch={query.refetch}>
      {() => (
        <GradingTable
          rows={rows}
          search={search}
          onSearch={setSearch}
          canAccept={toAccept.length > 0 && !update.isPending}
          error={update.isError ? mutationErrorMessage(update.error) : undefined}
          onAcceptRecommended={() =>
            update.mutate(
              toAccept.map((r) => ({
                studentId: r.studentId,
                body: {
                  tutorPoints: r.recommended.tutorPoints,
                  referencePoints: r.recommended.referencePoints,
                },
              })),
            )
          }
        />
      )}
    </QueryState>
  );
}

export default GradingPage;
