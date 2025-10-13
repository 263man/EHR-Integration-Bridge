import { useState, useEffect } from 'react';

// Define the shape of a single incomplete record for TypeScript
interface IncompleteRecord {
  patientId: string;
    field: string;
      description: string;
      }

      // Define the shape of the entire audit response
      interface AuditData {
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
                              // The Vite proxy handles routing '/api/Audit' to 'http://localhost:8080/api/Audit'
                                  fetch('/api/Audit')
                                        .then(response => {
                                                if (!response.ok) {
                                                          throw new Error(`HTTP error! status: ${response.status}`);
                                                                  }
                                                                          return response.json();
                                                                                })
                                                                                      .then((auditData: AuditData) => {
                                                                                              setData(auditData);
                                                                                                      setLoading(false);
                                                                                                            })
                                                                                                                  .catch((err) => {
                                                                                                                          console.error("Fetch error:", err);
                                                                                                                                  setError(err.message);
                                                                                                                                          setLoading(false);
                                                                                                                                                });
                                                                                                                                                  }, []);

                                                                                                                                                    return { data, loading, error };
                                                                                                                                                    };

                                                                                                                                                    export default useAuditData;
                                                                                                                                                    