using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FieldOpsOptimizer.Api.Controllers;

/// <summary>
/// Controller for CSRF token management
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class CsrfController : ControllerBase
{
    private readonly IAntiforgery _antiforgery;
    private readonly ILogger<CsrfController> _logger;

    public CsrfController(IAntiforgery antiforgery, ILogger<CsrfController> logger)
    {
        _antiforgery = antiforgery;
        _logger = logger;
    }

    /// <summary>
    /// Get CSRF token for form submissions
    /// This endpoint provides the anti-forgery token that clients need to include
    /// in their requests to protected endpoints
    /// </summary>
    /// <returns>CSRF token information</returns>
    [HttpGet("token")]
    [ProducesResponseType(typeof(CsrfTokenResponse), StatusCodes.Status200OK)]
    public IActionResult GetCsrfToken()
    {
        try
        {
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            
            var response = new CsrfTokenResponse
            {
                Token = tokens.RequestToken!,
                HeaderName = "X-XSRF-TOKEN",
                CookieName = "__RequestVerificationToken"
            };

            _logger.LogDebug("CSRF token provided to client");
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating CSRF token");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}

/// <summary>
/// Response model for CSRF token information
/// </summary>
public class CsrfTokenResponse
{
    public string Token { get; set; } = string.Empty;
    public string HeaderName { get; set; } = string.Empty;
    public string CookieName { get; set; } = string.Empty;
}