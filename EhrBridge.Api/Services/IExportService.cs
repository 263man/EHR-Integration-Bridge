// EhrBridge.Api/Services/IExportService.cs
using EhrBridge.Api.DataGeneration;
using EhrBridge.Api.Data;

namespace EhrBridge.Api.Services
{
    public interface IExportService
    {
        /// <summary>
        /// Asynchronously streams all patient records from the EHR database.
        /// Uses CleanPatientRecord DTO for full demographic export.
        /// </summary>
        IAsyncEnumerable<CleanPatientRecord> StreamAllPatientRecordsAsync();

        /// <summary>
        /// Asynchronously streams incomplete demographic records from the EHR database.
        /// Uses IncompleteRecordDto for audit list export.
        /// </summary>
        IAsyncEnumerable<IncompleteRecordDto> StreamIncompleteDemographicRecordsAsync();
    }
}