using EhrBridge.Api.Data;
using System.Threading;
using System.Threading.Tasks;

namespace EhrBridge.Api.Services;

public interface IAuditService
{
    Task<AuditResultDto> RunDataQualityAuditAsync(CancellationToken cancellationToken = default);
}
