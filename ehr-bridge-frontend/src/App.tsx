// ehr-bridge-frontend/src/App.tsx
import './App.css';
import useAuditData from './hooks/useAuditData';

const DataQualityChart = ({ complete, incomplete }: { complete: number, incomplete: number }) => {
  const total = complete + incomplete;
  const completePercent = total > 0 ? ((complete / total) * 100).toFixed(1) : 0;

  return (
    <div className="chart-container">
      <h3>Data Completeness Ratio</h3>
      <div className="chart-placeholder">
        <p>
          **[Doughnut Chart Placeholder]**
          <br />
          Complete: {complete} ({completePercent}%)
          <br />
          Incomplete: {incomplete}
        </p>
        <p className="chart-tip">
          **Plan:** This space will hold a `react-chartjs-2` Doughnut chart to visually display the ratio of **Complete vs. Incomplete** records upon the next committed change.
        </p>
      </div>
    </div>
  );
};

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

  const completeRecords = data ? data.totalRecordsScanned - data.incompleteRecordsFound : 0;

  return (
    <div className="app-container">
      {/* ✅ Always show title */}
      <h1 className="app-title">EHR Data Audit Dashboard</h1>

      {/* ✅ Always show control panel */}
      <div className="controls-panel">
        <h2>Control Panel</h2>
        {loading && (
          <div style={{ color: 'var(--text-color)', marginBottom: '10px' }}>
            Processing request...
          </div>
        )}

        <div className="control-group-wrapper">
          <div className="control-section">
            <h3>Data and Audit Control</h3>
            <div className="control-group">
              <button onClick={reseedData} disabled={loading} className="btn-primary">
                🔁 Reseed Data (POST /control/reseed)
              </button>
              <button onClick={runAudit} disabled={loading} className="btn-primary">
                🔎 Run Audit (GET /api/audit)
              </button>
            </div>
          </div>

          <div className="control-section">
            <h3>Export Actions</h3>
            <div className="control-group">
              <button onClick={downloadAllCsv} disabled={loading || !data} className="btn-export">
                ⬇️ Download CSV (All)
              </button>
              <button
                onClick={downloadIncompleteCsv}
                disabled={loading || !data || data.incompleteRecordsFound === 0}
                className="btn-export"
              >
                ⬇️ Download CSV (Incomplete)
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* ✅ Show error only if present */}
      {error && (
        <div className="error">
          <h2>Error Occurred!</h2>
          <p>{error}</p>
          <p>
            Ensure Docker is running: <strong>docker compose up --build -d</strong>
          </p>
        </div>
      )}

      {/* ✅ Wrap data section in a scrollable container */}
      <div className="dashboard-scrollable-area">
        {data && !error && (
          <>
            <hr />

            <div className="dashboard-visualisation-area">
              <div className="metrics-group">
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

              <div className="chart-group">
                <DataQualityChart
                  complete={completeRecords}
                  incomplete={data.incompleteRecordsFound}
                />
              </div>
            </div>

            <h2>Incomplete Demographics List ({data.incompleteRecords.length})</h2>

            <div className="audit-table-wrapper">
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
          </>
        )}
      </div>
    </div>
  );
}

export default App;
