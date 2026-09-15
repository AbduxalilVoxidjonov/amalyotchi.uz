import type { ReactNode } from 'react';
import type { UseQueryResult } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { usePageHeader } from '@/app/layout';
import { isApiError } from '@/shared/api';
import { Breadcrumb, Button, EmptyState, type BreadcrumbItem } from '@/shared/ui';
import { ErrorState } from '../../components/PageStatus';
import styles from './HierarchyListPage.module.css';

const FACULTIES_ROOT: BreadcrumbItem[] = [{ label: 'Fakultetlar', to: '/admin/faculties' }];

export interface HierarchyListPageProps<TDetail> {
  /** Joriy obyektning o'zi (breadcrumb — 404/xato/nom shundan chiqadi). */
  detailQuery: UseQueryResult<TDetail>;
  /** Muvaffaqiyatli bo'lsa breadcrumb elementlari (oxirgisi joriy obyekt). */
  buildBreadcrumb: (detail: TDetail) => BreadcrumbItem[];
  /** Yuklanayotganda ko'rsatiladigan breadcrumb (oxirgi element "…"). */
  loadingBreadcrumb: BreadcrumbItem[];
  /** 404 bo'lsa. */
  notFoundTitle: string;
  backTo: string;
  backLabel: string;
  /** Topbar sarlavhasi — muvaffaqiyatli yuklangach joriy obyekt nomiga almashadi. */
  pageTitle?: (detail: TDetail) => string;
  /** 404/xato holatidagi breadcrumb (default — "Fakultetlar"). */
  rootBreadcrumb?: BreadcrumbItem[];
  /** Ro'yxat + amallar — detail 404/xato bo'lmasa render qilinadi. */
  children: ReactNode;
}

/**
 * Ierarxiya sahifalari (kafedralar/yo'nalishlar/guruhlar) uchun umumiy qobiq:
 * breadcrumb + "ota topilmadi" (404) / xato holatlari. `FacultyDepartmentsPage`,
 * `DepartmentDirectionsPage`, `DirectionGroupsPage` (va `rootBreadcrumb` bilan `TutorDetailPage`)
 * shu orqali takrorlanishni kamaytiradi.
 */
export function HierarchyListPage<TDetail>({
  detailQuery,
  buildBreadcrumb,
  loadingBreadcrumb,
  notFoundTitle,
  backTo,
  backLabel,
  pageTitle,
  rootBreadcrumb = FACULTIES_ROOT,
  children,
}: HierarchyListPageProps<TDetail>) {
  const is404 = isApiError(detailQuery.error) && detailQuery.error.status === 404;
  usePageHeader({ title: detailQuery.data && pageTitle ? pageTitle(detailQuery.data) : undefined });

  if (is404) {
    return (
      <div className={styles.page}>
        <Breadcrumb items={rootBreadcrumb} />
        <EmptyState
          tone="plain"
          title={notFoundTitle}
          action={
            <Button asChild>
              <Link to={backTo}>{backLabel}</Link>
            </Button>
          }
        />
      </div>
    );
  }

  if (detailQuery.isError) {
    return (
      <div className={styles.page}>
        <Breadcrumb items={rootBreadcrumb} />
        <ErrorState error={detailQuery.error} onRetry={() => void detailQuery.refetch()} />
      </div>
    );
  }

  const items = detailQuery.data ? buildBreadcrumb(detailQuery.data) : loadingBreadcrumb;

  return (
    <div className={styles.page}>
      <Breadcrumb items={items} />
      {children}
    </div>
  );
}
