using OAuthTestApp.Models;

namespace OAuthTestApp.Services;

public interface IEHRService
{
    Task<EHRProvider> GetProviderDetails(string providerID);
    Task<string> GetPatientDataAsync(string patientID);
}
