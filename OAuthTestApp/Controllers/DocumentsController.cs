using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using OAuthTestApp.Models;
using OAuthTestApp.Services;

namespace OAuthTestApp.Controllers
{
    [Authorize]
    public class DocumentsController : Controller
    {
        private readonly DocumentService _documentService;

        public DocumentsController(DocumentService documentService)
        {
            _documentService = documentService;
        }

        public async Task<IActionResult> Index()
        {
            var accessToken = HttpContext.Session.GetString("AccessToken");
            var patientID = HttpContext.Session.GetString("PatientID");
            var providerID = HttpContext.Session.GetString("ProviderID");

            if (accessToken == null || patientID == null || providerID == null)
            {
                return Unauthorized();
            }

            var documentsURL = await _documentService.GetDocumentsAsync(patientID, providerID, accessToken);

            ViewData["DMDocsURL"] = documentsURL;
            return Redirect(documentsURL);
        }
    }
}
