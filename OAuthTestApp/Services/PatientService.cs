using System;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc;

namespace OAuthTestApp.Services;
public class PatientService
{
    private readonly IConfiguration _configuration;

    public PatientService(IConfiguration configuration)
    {
        _configuration = configuration;
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
                //command.Parameters.AddWithValue("@PatientId", patientId);

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
