import './App.css';
import useAuditData from './hooks/useAuditData';

function App() {
  const { data, loading, error } = useAuditData();

  if (loading) {
    return <div className="app-container"><h1>Loading Audit Report...</h1></div>;
  }

  if (error) {
    return (
      <div className="app-container error">
        <h1>Error Fetching Data!</h1>
        <p>{error}</p>
        <p>Ensure Docker is running: <strong>docker compose up --build -d</strong></p>
      </div>
    );
  }

  return (
    <div className="app-container">
      <h1>EHR Data Audit Dashboard</h1>

      <div className="summary-cards">
        <div className="card">
          <h2>Total Records Scanned</h2>
          <p className="metric">{data.totalRecordsScanned}</p>
        </div>
        <div className="card alert">
          <h2>Incomplete Records Found</h2>
          <p className="metric">{data.incompleteRecordsFound}</p>
        </div>
      </div>

      <h2>Incomplete Demographics List ({data.incompleteRecords.length})</h2>

      <table className="audit-table">
        <thead>
          <tr>
            <th>Patient ID</th>
            <th>Field</th>
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
