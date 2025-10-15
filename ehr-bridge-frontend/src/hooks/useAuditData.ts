import { useState, useEffect } from 'react';

export interface IncompleteRecord {
  patientId: number;
  field: string;
  description: string;
}

export interface AuditData {
  totalRecordsScanned: number;
  incompleteRecordsFound: number;
  incompleteRecords: IncompleteRecord[];
}

const initialData: AuditData = {
  totalRecordsScanned: 0,
  incompleteRecordsFound: 0,
  incompleteRecords: [],
};

const useAuditData = () => {
  const [data, setData] = useState<AuditData>(initialData);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetch('/api/Audit')
      .then(response => {
        if (!response.ok) {
          throw new Error(`HTTP error! Status: ${response.status}`);
        }
        return response.json();
      })
      .then((auditData: AuditData) => {
        setData(auditData);
        setLoading(false);
      })
      .catch((err) => {
        console.error('Fetch error:', err);
        setError(err.message);
        setLoading(false);
      });
  }, []);

  return { data, loading, error };
};

export default useAuditData;
