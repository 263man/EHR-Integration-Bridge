import './App.css';
import useAuditData from './hooks/useAuditData';
import { useMemo } from 'react';

import {
  Chart as ChartJS,
  ArcElement,
  Tooltip,
  Legend,
} from 'chart.js';
import { Doughnut } from 'react-chartjs-2';

import type {
  ChartOptions,
  ChartData,
  TooltipItem,
} from 'chart.js';

ChartJS.register(ArcElement, Tooltip, Legend);

const DataQualityChart = ({ complete, incomplete }: { complete: number, incomplete: number }) => {
  const total = complete + incomplete;
  const completePercent = total > 0 ? ((complete / total) * 100).toFixed(1) : '0';

  const data: ChartData<'doughnut'> = useMemo(() => ({
    labels: ['Complete', 'Incomplete'],
    datasets: [
      {
        data: [complete, incomplete],
        backgroundColor: ['#4caf50', '#f44336'],
        borderColor: ['#ffffff', '#ffffff'],
        borderWidth: 2,
      },
    ],
  }), [complete, incomplete]);

  const options: ChartOptions<'doughnut'> = useMemo(() => ({
    cutout: '60%',
    plugins: {
      legend: {
        position: 'bottom',
        labels: {
          color: '#343a40',
          font: {
            size: 14,
            weight: 'bold',
          },
        },
      },
      tooltip: {
        callbacks: {
          label: (context: TooltipItem<'doughnut'>) => {
            const value = context.raw as number;
            const label = context.label || '';
            const percentage = total > 0 ? ((value / total) * 100).toFixed(1) : '0';
            return `${label}: ${value} (${percentage}%)`;
          },
        },
      },
    },
    maintainAspectRatio: false,
  }), [total]);

  return (
    <div className="chart-container">
      <h3>Data Completeness Ratio</h3>
      <div style={{ height: '300px' }}>
        <Doughnut data={data} options={options} />
      </div>
      <p style={{ textAlign: 'center', marginTop: '10px' }}>
        {complete} complete / {incomplete} incomplete ({completePercent}% complete)
      </p>
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
      <h1 className="app-title">EHR Data Audit Dashboard</h1>

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
                🔁 Reseed Data
              </button>
              <button onClick={runAudit} disabled={loading} className="btn-primary">
                🔎 Run Audit
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

      {error && (
        <div className="error">
          <h2>Error Occurred!</h2>
          <p>{error}</p>
          <p>
            Ensure Docker is running: <strong>docker compose up --build -d</strong>
          </p>
        </div>
      )}

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
