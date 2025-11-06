import { useState, useCallback } from 'react';

// ✅ Use full backend URLs for production
const API_BASE = 'https://ehrbridgeapi.kepekepe.com/api';
const EXPORT_BASE = 'https://ehrbridgeapi.kepekepe.com/export';

// CONSTANT: Expected number of records after a successful reseed operation
const SEEDED_RECORD_COUNT = 1000;

export interface IncompleteRecord {
  patientId: number;
  field: string;
  description: string;
}

export interface AuditData {
  totalRecordsScanned: number;
  incompleteRecordsFound: number;
  incompleteRecords: IncompleteRecord[];
  lastRunTimestamp: string;
}

const initialData: AuditData = {
  totalRecordsScanned: 0,
  incompleteRecordsFound: 0,
  incompleteRecords: [],
  lastRunTimestamp: 'N/A',
};

// State reflecting the seeded database before the first audit run
const reseedSuccessData: AuditData = {
  totalRecordsScanned: SEEDED_RECORD_COUNT,
  incompleteRecordsFound: 0,
  incompleteRecords: [],
  lastRunTimestamp: 'Audit Pending',
};

export const useAuditData = () => {
  const [data, setData] = useState<AuditData>(initialData);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const fetchAuditData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${API_BASE}/audit`);
      if (!response.ok) throw new Error(`HTTP error! Status: ${response.status}`);

      const auditData: AuditData = await response.json();
      setData({
        ...auditData,
        lastRunTimestamp: new Date().toLocaleTimeString('en-GB', {
          hour: '2-digit',
          minute: '2-digit',
          second: '2-digit',
        }),
      });
    } catch (err) {
      console.error('Fetch error:', err);
      setError((err as Error).message || 'An unknown error occurred during audit.');
    } finally {
      setLoading(false);
    }
  }, []);

  const reseedData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${API_BASE}/control/reseed`, { method: 'POST' });
      if (!response.ok)
        throw new Error(`Reseed API failed: ${response.status} - ${response.statusText}`);

      setData(reseedSuccessData);
    } catch (err) {
      console.error('Reseed error:', err);
      setError((err as Error).message || 'An unknown error occurred during reseed.');
    } finally {
      setLoading(false);
    }
  }, []);

  // Utility to trigger browser download
  const downloadCsv = useCallback((endpoint: string, fileName: string) => {
    const url = `${EXPORT_BASE}/${endpoint}`;
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', fileName);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    console.log(`Triggered download for ${fileName} from ${url}`);
  }, []);

  const downloadAllCsv = useCallback(() => {
    downloadCsv('all', 'Full_Patient_Export.csv');
  }, [downloadCsv]);

  const downloadIncompleteCsv = useCallback(() => {
    downloadCsv('incomplete', 'Incomplete_Demographics_Audit_List.csv');
  }, [downloadCsv]);

  return {
    data,
    loading,
    error,
    runAudit: fetchAuditData,
    reseedData,
    downloadAllCsv,
    downloadIncompleteCsv,
  };
};

export default useAuditData;
