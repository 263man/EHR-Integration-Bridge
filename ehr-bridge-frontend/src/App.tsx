// ehr-bridge-frontend/src/App.tsx
import './App.css';
import useAuditData from './hooks/useAuditData';

function App() {
  const { 
    data, 
    loading, 
    error, 
    reseedData, 
    runAudit, 
    downloadAllCsv, 
    downloadIncompleteCsv 
  } = useAuditData();

  // Remove initial loading state check, since the initial state now shows 0/0 and buttons.
  // if (loading) {
  //   return <div className="app-container"><h1>Loading Audit Report...</h1></div>;
  // }

  if (error) {
    return (
      <div className="app-container error">
        <h1>Error Occurred!</h1>
        <p>{error}</p>
        <p>Ensure Docker is running: <strong>docker compose up --build -d</strong></p>
      </div>
    );
  }

  return (
    <div className="app-container">
      <h1>EHR Data Audit Dashboard</h1>

      {/* --- New Control Panel --- */}
      <div className="controls-panel">
        <h2>Control Panel</h2>
        {loading && <div style={{ color: 'blue', marginBottom: '10px' }}>Processing request...</div>}
        <div style={{ display: 'flex', gap: '10px', marginBottom: '15px' }}>
            <button onClick={reseedData} disabled={loading}>
                🔁 Reseed Data (POST /control/reseed)
            </button>
            <button onClick={runAudit} disabled={loading}>
                🔎 Run Audit (GET /api/audit)
            </button>
        </div>

        <h3>Export Actions</h3>
        <div style={{ display: 'flex', gap: '10px' }}>
            {/* Download button for all records */}
            <button onClick={downloadAllCsv} disabled={loading}>
                ⬇️ Download CSV (All)
            </button>
            {/* Download button for incomplete records - disabled if no records found */}
            <button 
                onClick={downloadIncompleteCsv} 
                disabled={loading || data.incompleteRecordsFound === 0}
            >
                ⬇️ Download CSV (Incomplete)
            </button>
        </div>
      </div>
      {/* --- End Control Panel --- */}

      <hr style={{ margin: '20px 0'}} />

      <div className="summary-cards">
        <div className="card">
          <h2>Total Records Scanned</h2>
          <p className="metric">{data.totalRecordsScanned}</p>
        </div>
        <div className="card alert">
          <h2>Incomplete Records Found</h2>
          <p className="metric">{data.incompleteRecordsFound}</p>
        </div>
        {data.totalRecordsScanned > 0 && (
            <div className="card">
                <h2>Audit Last Run</h2>
                <p className="metric">{data.lastRunTimestamp}</p>
            </div>
        )}
      </div>

      <h2>Incomplete Demographics List ({data.incompleteRecords.length})</h2>

      <table className="audit-table">
        <thead>
          <tr>
            <th>Patient ID</th>
            <th>Missing Field</th>
            <th>Description</th>
          </tr>
        </thead>
        <tbody>
          {data.incompleteRecords.map((record) => (
            <tr key={`${record.patientId}-${record.field}`}>
              <td>{record.patientId}</td>
              <td>{record.field}</td>
              <td>{record.description}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default App;