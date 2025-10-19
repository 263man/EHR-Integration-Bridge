namespace EhrBridge.Api.Services;

public interface IControlService
{
    // The core re-seed operation: TRUNCATE existing patient data and insert 1,000 new records.
    Task ReSeedDemoDataAsync();
}
