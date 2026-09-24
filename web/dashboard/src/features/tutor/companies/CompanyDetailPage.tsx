import { Link, useParams } from 'react-router-dom';
import { isApiError } from '@/shared/api';
import { Button, EmptyState } from '@/shared/ui';
import { CheckInQrCard } from '../../shared/checkin-qr';
import { QueryState } from '../components/QueryState';
import { CompanyDetailView } from './components/CompanyDetailView';
import { CompanyStudentsTable } from './components/CompanyStudentsTable';
import styles from './CompanyDetailPage.module.css';
import { useTutorCompanyQuery, useTutorCompanyStudentsQuery } from './hooks';

const BACK_TO = '/tutor/companies';

/**
 * Tyutor · Korxona sahifasi (`/tutor/companies/:companyId`): korxona kartasi + lokatsiya,
 * STIR nazorati, amaliyot davrlari, check-in QR kodi va ko'lamdagi talabalar jadvali.
 * Tyutor bo'limida breadcrumb yo'q — oddiy orqaga qaytish havolasi.
 */
export function CompanyDetailPage() {
  const { companyId = '' } = useParams<{ companyId: string }>();
  const companyQuery = useTutorCompanyQuery(companyId);
  const studentsQuery = useTutorCompanyStudentsQuery(companyId);
  const is404 = isApiError(companyQuery.error) && companyQuery.error.status === 404;

  return (
    <div className={styles.page}>
      <Link className={styles.back} to={BACK_TO}>
        ← Korxonalar
      </Link>

      {is404 ? (
        <EmptyState
          tone="plain"
          title="Korxona topilmadi."
          description="Bu korxona ko'lamingizda emas yoki o'chirilgan."
          action={
            <Button asChild>
              <Link to={BACK_TO}>Korxonalarga qaytish</Link>
            </Button>
          }
        />
      ) : (
        <QueryState
          status={companyQuery.status}
          data={companyQuery.data}
          error={companyQuery.error}
          refetch={companyQuery.refetch}
        >
          {(company) => (
            <>
              <CompanyDetailView company={company} />
              <CheckInQrCard area="tutor" companyId={company.id} />
              <QueryState
                status={studentsQuery.status}
                data={studentsQuery.data}
                error={studentsQuery.error}
                refetch={studentsQuery.refetch}
                isEmpty={(rows) => rows.length === 0}
                empty={
                  <EmptyState
                    title="Talabalar yo'q"
                    description="Ko'lamingizda bu korxonaga biriktirilgan talaba yo'q."
                  />
                }
              >
                {(rows) => <CompanyStudentsTable rows={rows} />}
              </QueryState>
            </>
          )}
        </QueryState>
      )}
    </div>
  );
}

export default CompanyDetailPage;
