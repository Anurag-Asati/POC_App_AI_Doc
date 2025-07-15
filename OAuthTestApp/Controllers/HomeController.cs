using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using OAuthTestApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using OAuthTestApp.Services;
using Serilog;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace OAuthTestApp.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly DocumentService _documentService;

    public HomeController(ILogger<HomeController> logger, DocumentService documentService)
    {
        _logger = logger;
        _documentService = documentService;
    }

    [Authorize]
    public IActionResult Secure()
    {
        ViewData["AccessToken"] = HttpContext.Session.GetString("AccessToken");
        ViewData["PatientID"] = HttpContext.Session.GetString("PatientID");

        return View();
    }

    [Authorize]
    public async Task<IActionResult> Index(string launch, string iss)
    {
        _logger.LogDebug("Routed to HomeController Index method");
        _logger.LogDebug("AccessToken in HomeController: " + HttpContext.Session.GetString("AccessToken"));
        
        ViewData["PatientID"] = HttpContext.Session.GetString("PatientID");
        ViewData["TokenResponse"] = HttpContext.Session.GetString("TokenResponse");
        ViewData["ProviderID"] = HttpContext.Session.GetString("ProviderID");
        ViewData["IDToken"] = HttpContext.Session.GetString("IDToken");
        ViewData["launchCode"] = HttpContext.Session.GetString("launchCode");
        var claimsCount = Convert.ToInt32(HttpContext.Session.GetString("ClaimsCount"));
        ViewData["ClaimsCount"] = claimsCount.ToString();
        for (int i = 0; i < claimsCount; i++)
        {
            ViewData["Claim" + i.ToString()] = HttpContext.Session.GetString("Claim" + i.ToString());
        }

        //return View();

        var accessToken = HttpContext.Session.GetString("AccessToken");
        var patientID = HttpContext.Session.GetString("PatientID");
        var providerID = HttpContext.Session.GetString("ProviderID");

        if (accessToken == null || string.IsNullOrEmpty(accessToken.Trim()) || patientID == null || providerID == null)
        {
            return Unauthorized();
        }

        var documentsURL = await _documentService.GetDocumentsAsync(patientID, providerID, accessToken);

        return Redirect(documentsURL);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Logout()
    {
        return SignOut(new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
