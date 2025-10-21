// /workspaces/EHR-Integration-Bridge/ehr-bridge-frontend/src/hooks/useAuditData.ts
import { useState, useCallback } from 'react';

// FIX: Change hardcoded URLs to relative paths so the Vite proxy handles the connection in Gitpod/Codespaces.
const API_BASE = '/api';
const EXPORT_BASE = '/export';

export interface IncompleteRecord {
  patientId: number;
  field: string;
  description: string;
}

export interface AuditData {
  totalRecordsScanned: number;
  incompleteRecordsFound: number;
  incompleteRecords: IncompleteRecord[];
  // NOTE: Adding a timestamp field for better UI feedback consistency.
  lastRunTimestamp: string; 
}

const initialData: AuditData = {
  totalRecordsScanned: 0,
  incompleteRecordsFound: 0,
  incompleteRecords: [],
  lastRunTimestamp: 'N/A',
};

// Refactored to expose control methods instead of immediate data fetch
export const useAuditData = () => {
  const [data, setData] = useState<AuditData>(initialData);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const fetchAuditData = useCallback(async () => {
    setLoading(true);
    setError(null);
    
    try {
      // Use relative path
      const response = await fetch(`${API_BASE}/audit`);
      
      if (response.ok === false) {
        throw new Error(`HTTP error! Status: ${response.status}`);
      }
      
      const auditData: AuditData = await response.json();
      
      setData({
          ...auditData,
          lastRunTimestamp: new Date().toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit', second: '2-digit' }),
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
      // Use relative path
      const response = await fetch(`${API_BASE}/control/reseed`, { method: 'POST' });
      
      if (response.ok === false) {
        throw new Error(`Reseed API failed: ${response.status} - ${response.statusText}`);
      }
      
      alert('Data reseeded successfully (1,000 records). Run Audit next.');
      setData(initialData); // Reset audit display
    } catch (err) {
      console.error('Reseed error:', err);
      setError((err as Error).message || 'An unknown error occurred during reseed.');
    } finally {
      setLoading(false);
    }
  }, []);

  // Utility to trigger browser download
  const downloadCsv = useCallback((endpoint: string, fileName: string) => {
    // This simple technique uses the browser to handle the streaming GET request
    // and subsequent file save prompt, relying on the API's Content-Disposition header.
    // Use relative path
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
    downloadIncompleteCsv 
  };
};

export default useAuditData;