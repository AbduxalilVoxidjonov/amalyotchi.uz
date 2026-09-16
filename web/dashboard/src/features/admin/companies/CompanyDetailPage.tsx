import { useParams } from 'react-router-dom';
import type { BreadcrumbItem } from '@/shared/ui';
import { LoadingState } from '../components/PageStatus';
import { HierarchyListPage } from '../faculties/components/HierarchyListPage';
import { CompanyDetailView } from './components/CompanyDetailView';
import { CompanyStudentsTable } from './components/CompanyStudentsTable';
import styles from './CompanyDetailPage.module.css';
import { useCompanyQuery, useCompanyStudentsQuery } from './hooks';

const ROOT: BreadcrumbItem[] = [{ label: 'Korxonalar', to: '/admin/companies' }];

/**
 * Admin · Korxona sahifasi (`/admin/companies/:companyId`): korxona kartasi + lokatsiya,
 * STIR nazorati, amaliyot davrlari kesimi va shu korxonadagi talabalar jadvali.
 * Breadcrumb/404/xato qobig'i — `HierarchyListPage` (tyutor detali bilan bir xil naqsh).
 */
export function CompanyDetailPage() {
  const { companyId = '' } = useParams<{ companyId: string }>();
  const companyQuery = useCompanyQuery(companyId);
  const studentsQuery = useCompanyStudentsQuery(companyId);
  const company = companyQuery.data;

  return (
    <HierarchyListPage
      detailQuery={companyQuery}
      rootBreadcrumb={ROOT}
      buildBreadcrumb={(c) => [...ROOT, { label: c.name }]}
      loadingBreadcrumb={[...ROOT, { label: '…' }]}
      notFoundTitle="Korxona topilmadi."
      backTo="/admin/companies"
      backLabel="Korxonalarga qaytish"
      pageTitle={(c) => c.name}
    >
      {company ? (
        <div className={styles.stack}>
          <CompanyDetailView company={company} />
          <CompanyStudentsTable
            rows={studentsQuery.data ?? []}
            isLoading={studentsQuery.isPending}
            error={studentsQuery.error}
            onRetry={() => void studentsQuery.refetch()}
          />
        </div>
      ) : (
        <LoadingState />
      )}
    </HierarchyListPage>
  );
}

export default CompanyDetailPage;
