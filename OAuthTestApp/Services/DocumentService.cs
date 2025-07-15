using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OAuthTestApp.Models;
using OAuthTestApp;
using Microsoft.Extensions.Options;

namespace OAuthTestApp.Services;

public class DocumentService
{
    private readonly PatientService _patientService;
    private readonly IEHRService _ehrService;
    private readonly DMSettings _dmSettings;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(PatientService patientService, IEHRService ehrService, IOptions<DMSettings> options, ILogger<DocumentService> logger)
    {
        _patientService = patientService;
        _ehrService = ehrService;
        _dmSettings = options.Value;
        _logger = logger;
    }

    public async Task<string> GetDocumentsAsync(string patientID, string providerID, string accessToken)
    {
        var providerDetails = await _ehrService.GetProviderDetails(providerID);
        var userName = providerDetails.LoginName;
        var account = _dmSettings.DMAccount;
        var secret = _dmSettings.DMSecretApiKey;

        var jsonPtData = await _ehrService.GetPatientDataAsync(patientID);
        
        var hexUserName = StrToHex(RC4Encrypt(userName, secret));
        var hexAccount = StrToHex(RC4Encrypt(account, secret));
        var hexSecret = StrToHex(RC4Encrypt(secret, secret));
        var hexPatient = StrToHex(RC4Encrypt(jsonPtData, secret));

        var baseUrl = _dmSettings.DMBaseUrl;
        var authUrl = _dmSettings.DMAuthUrl;

        var url = baseUrl + authUrl + "?" + "u=" + hexUserName + "&a=" + hexAccount + "&d=" + hexPatient;
        
        return url;
    }

    private static string RC4Encrypt(string p_str, string p_key)
    {
        int[] s = new int[256];
        int j = 0;
        int x;
        StringBuilder res = new StringBuilder();

        for (int i = 0; i < 256; i++)
        {
            s[i] = i;
        }

        for (int i = 0; i < 256; i++)
        {
            j = (j + s[i] + p_key[i % p_key.Length]) % 256;
            x = s[i];
            s[i] = s[j];
            s[j] = x;
        }

        int i_index = 0;
        j = 0;
        for (int y = 0; y < p_str.Length; y++)
        {
            i_index = (i_index + 1) % 256;
            j = (j + s[i_index]) % 256;
            x = s[i_index];
            s[i_index] = s[j];
            s[j] = x;
            res.Append((char)(p_str[y] ^ s[(s[i_index] + s[j]) % 256]));
        }

        return res.ToString();
    }

    private static string StrToHex(string s)
    {
        StringBuilder hexResult = new StringBuilder();
        
        foreach (char c in s)
        {
            string hexChar = ((int)c).ToString("X2");
            hexResult.Append(hexChar);
        }

        return hexResult.ToString();
    }
}

