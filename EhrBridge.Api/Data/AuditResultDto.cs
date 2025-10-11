using System.Collections.Generic;

namespace EhrBridge.Api.Data;

// This DTO contains the full response for the API /api/audit endpoint.
public class AuditResultDto
{
    // Key Metrics for the dashboard view.
    public int TotalRecordsScanned { get; set; }
    public int IncompleteRecordsFound { get; set; }
    
    // The list of patient records requiring administrative follow-up.
    public List<IncompleteRecordDto> IncompleteRecords { get; set; } = new List<IncompleteRecordDto>();
}
