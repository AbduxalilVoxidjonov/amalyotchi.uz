import { useCallback, useState } from 'react';
import { downloadAuthFile } from '@/shared/files';

export interface TemplateDownload {
  download: () => void;
  isLoading: boolean;
  /** Yuklab olinmasa — tugma yonida ko'rsatiladigan xabar. */
  error: string | null;
}

/**
 * "Shablon" tugmasi: `.xlsx` ni Bearer token bilan olib, brauzerga saqlatadi.
 * Endpoint `AdminOnly` bo'lgani uchun oddiy `<a href>` 401 oladi.
 */
export function useTemplateDownload(url: string, fileName: string): TemplateDownload {
  const [isLoading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const download = useCallback(() => {
    setLoading(true);
    setError(null);
    downloadAuthFile(url, fileName)
      .catch(() => setError("Shablonni yuklab bo'lmadi. Qaytadan urinib ko'ring."))
      .finally(() => setLoading(false));
  }, [url, fileName]);

  return { download, isLoading, error };
}
