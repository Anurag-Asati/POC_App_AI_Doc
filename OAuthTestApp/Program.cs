#nullable disable

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using System.IdentityModel.Tokens.Jwt;
using OAuthTestApp.Services;
using OAuthTestApp;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

//#error version

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.File("Logs/log.txt", rollingInterval: RollingInterval.Day)
);

var authServer = builder.Configuration.GetValue<string>("AuthServer");
var clientId = builder.Configuration.GetValue<string>(authServer + "ClientId");
var clientSecret = builder.Configuration.GetValue<string>(authServer + "ClientSecret");
var authorizationServer = builder.Configuration.GetValue<string>(authServer + "AuthorizationServer");
var redirectUrl = builder.Configuration.GetValue<string>(authServer + "RedirectUrl");
var signoutRedirectUrl = builder.Configuration.GetValue<string>(authServer + "SignoutRedirectUrl");
var authorizationEndpoint = builder.Configuration.GetValue<string>(authServer + "AuthorizationEndpoint");
var tokenEndpoint = builder.Configuration.GetValue<string>(authServer + "TokenEndpoint");
var audience = builder.Configuration.GetValue<string>(authServer + "Audience");

builder.Services.Configure<DMSettings>(builder.Configuration.GetSection("DMSettings"));

builder.Services.AddAuthentication(options =>
    {
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddOAuth("OpenIdConnect", options =>
    {
        options.ClientId = clientId;
        options.ClientSecret = clientSecret;
        options.AuthorizationEndpoint = authorizationEndpoint;
        options.TokenEndpoint = tokenEndpoint;
        var scopes = builder.Configuration.GetValue<string>("Scopes");
        var scopesArr = scopes.Split(" ");
        foreach (var scope in scopesArr)
        {
            options.Scope.Add(scope);
            Log.Debug("Added scope: " + scope);
        }

        options.CallbackPath = redirectUrl;

        options.SaveTokens = true;
        
        options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
        {
            OnRedirectToAuthorizationEndpoint = context =>
                {
                    string launchCode = context.Request.Query["launch"];
                    if (launchCode != null)
                    {
                        context.HttpContext.Session.SetString("launchCode", launchCode + " on redirect");
                        Log.Debug("launchCode on redirect: " + launchCode);
                    }
                    else
                    {
                        context.HttpContext.Session.SetString("launchCode", "No launch code");
                        Log.Error("No LaunchCode");
                    }
                    string issuer = context.Request.Query["iss"];
                    if (issuer != null)
                    {
                        context.HttpContext.Session.SetString("issuer", issuer);
                        Log.Debug("issuer: " + issuer);
                    }
                    else
                    {
                        context.HttpContext.Session.SetString("issuer", "No issuer code");
                        Log.Error("No issuer code");
                    }
                    
                    var builder = new UriBuilder(context.RedirectUri);
                    var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
                    query["aud"] = audience;
                    query["launch"] = launchCode;
                    query["disable_userselection"] = "true";

                    builder.Query = query.ToString();
                    Log.Debug("Redirecting to Authorization Endpoint with URL: " + builder.ToString());
                    context.Response.Redirect(builder.ToString());
                    return Task.CompletedTask;
                },
            
            OnCreatingTicket = async context =>
                {
                    Log.Debug("Redirect endpoint: " + context.Request.Scheme + "://" + context.Request.Host + context.Request.Path + context.Request.QueryString);
                    
                    if (context.AccessToken != null)
                    {
                        context.HttpContext.Session.SetString("AccessToken", context.AccessToken);
                    }
                    else
                    {
                        context.HttpContext.Session.SetString("AccessToken", "AccessToken is null");
                        Log.Error("AccessToken is null");
                    }

                    var tokenResponse = context.TokenResponse.Response;
                    Log.Debug("Token Response: " + tokenResponse.RootElement.ToString());
                    if (tokenResponse.RootElement.TryGetProperty("id_token", out var idTokenElement))
                    {
                        var idToken = idTokenElement.GetString();
                        var pvID = ExtractProviderIdToken(idToken);
                        Log.Information("Provider ID: " + pvID ?? "No pvID found");
                        context.HttpContext.Session.SetString("IDToken", idToken ?? "IDToken is null");
                        var claimsDict = ExtractClaimsFromIdToken(idToken);
                        int claimsCount = 0;
                        foreach (var claim in claimsDict)
                        {
                            context.HttpContext.Session.SetString("Claim" + claimsCount.ToString(), "Claim type: " + claim.Key + ", Claim Value: " + claim.Value);
                            Log.Debug("Claim[" + claimsCount + "]: " + claim.Key + " = " + claim.Value);
                            claimsCount++;
                        }
                        context.HttpContext.Session.SetString("ClaimsCount", claimsCount.ToString());
                        context.HttpContext.Session.SetString("ProviderID", pvID ?? "ID Token exist but PVID is null");
                    }

                    var jsonDoc = JsonDocument.Parse(tokenResponse.RootElement.ToString());
                    var extendedContext = jsonDoc.RootElement.GetProperty("extended_context").GetString();
                    var contextDoc = JsonDocument.Parse(extendedContext);
                    var patientProfileId = contextDoc.RootElement.GetProperty("currentItem").GetProperty("patientProfileId").GetInt32();
                    context.HttpContext.Session.SetString("PatientID", patientProfileId.ToString());
                    Log.Information("Patient Profile ID: " + patientProfileId);

                    context.HttpContext.Session.SetString("TokenResponse", tokenResponse.RootElement.ToString());
                    
                    var identity = (ClaimsIdentity)context.Principal.Identity;
                    
                    await Task.CompletedTask;
                }
        };
    });

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IEHRService, AthEHRService>();
builder.Services.AddTransient<PatientService>();
builder.Services.AddTransient<DocumentService>();
builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(30);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            //options.Cookie.SameSite = SameSiteMode.None;
        });

var app = builder.Build();
app.UseSerilogRequestLogging();
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
    {
        _ = endpoints.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        _ = endpoints.MapControllerRoute(
            name: "documents",
            pattern: "documents",
            defaults: new { controller = "Documents", action = "Index" });
    });

app.Run();

static Dictionary<string, string> ExtractClaimsFromIdToken(string idToken)
{
    var handler = new JwtSecurityTokenHandler();
    var jwtToken = handler.ReadJwtToken(idToken);

    Dictionary<string, string> claims = new Dictionary<string, string>();
    foreach (var c in jwtToken.Claims)
    {
        claims.Add(c.Type, c.Value);
    }

    return claims;
}

static string ExtractProviderIdToken(string idToken)
{
    var handler = new JwtSecurityTokenHandler();
    var jwtToken = handler.ReadJwtToken(idToken);

    var pvID = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

    return pvID;
}