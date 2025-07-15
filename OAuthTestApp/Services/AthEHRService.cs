using System.Net.Http.Headers;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using OAuthTestApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace OAuthTestApp.Services;

public class AthEHRService : IEHRService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AthEHRService> _logger;

    public AthEHRService(IConfiguration configuration, ILogger<AthEHRService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }
    
    public async Task<EHRProvider> GetProviderDetails(string providerID)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();

            var query = "SELECT u.[PVID], u.[LOGINNAME], u.LASTNAME, u.FIRSTNAME, u.MIDDLENAME ";
            query += "FROM [dbo].[usr](NOLOCK) u WHERE u.PVID = " + providerID;
            
            using (var command = new SqlCommand(query, connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        var provider = new EHRProvider();
                        provider.ProviderID = reader["PVID"].ToString();
                        provider.LoginName = reader["LOGINNAME"].ToString();
                        provider.LastName = reader["LASTNAME"].ToString();
                        provider.FirstName = reader["FIRSTNAME"].ToString();
                        provider.MiddleName = reader["MIDDLENAME"].ToString();

                        return provider;
                    }
                }
            }
        }

        return null;
    }

    public async Task<string> GetPatientDataAsync(string patientId)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();

            var query = "select pp.[PId], ";	 
	        //query += "coalesce(query += @\"convert(char(10), pp.[Birthdate], 101), '') [vcBirthdate], ";
            query += "coalesce(convert(char(10), pp.[Birthdate], 101), '') [vcBirthdate], ";
            query += "coalesce(pp.[ExternalId], '') [ExternalId],  ";
            query += "coalesce(pp.[ExternalId], '') [ExternalId],  ";
            query += "coalesce(pp.[PatientId], '') [PatientId], ";
            query += "coalesce(df.[ListName], '') [ListName], ";
            query += "coalesce(pp.[First], '') [First], ";
            query += "coalesce(pp.[Middle], '') [Middle], "; 
            query += "coalesce(pp.[Last], '') [Last] ";
            query += "from PatientProfile pp left outer join [dbo].[DoctorFacility] df on pp.[DoctorId] = df.[DoctorFacilityId] where pp.PatientProfileId = " + patientId;
            using (var command = new SqlCommand(query, connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        var patientData = new
                        {
                            Data = new
                            {
                                Id = Convert.ToInt64(reader["PId"]),
                                Name = new
                                {
                                    First = reader["First"],
                                    Middle = reader["Middle"],
                                    Last = reader["Last"]
                                },
                                DOB = reader["vcBirthdate"],
                                ExternalId = reader["ExternalId"],
                                PatientId = reader["PatientId"],
                                ResponsibleProvider = reader["ListName"]
                            },
                            Controller = "Owner",
                            Action = "Index"
                        };

                        return JsonConvert.SerializeObject(patientData);
                    }
                }
            }
        }

        return null;
    }
}
