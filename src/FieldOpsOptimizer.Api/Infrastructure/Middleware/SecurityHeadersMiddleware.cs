using System.Net;

namespace FieldOpsOptimizer.Api.Infrastructure.Middleware;

/// <summary>
/// Middleware that adds security headers to protect against common web vulnerabilities
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        ILogger<SecurityHeadersMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers before processing the request
        AddSecurityHeaders(context);

        await _next(context);
    }

    private void AddSecurityHeaders(HttpContext context)
    {
        var response = context.Response;
        var headers = response.Headers;

        // Remove server information disclosure
        headers.Remove("Server");
        headers.Remove("X-Powered-By");

        // X-Frame-Options: Prevents clickjacking attacks
        if (!headers.ContainsKey("X-Frame-Options"))
        {
            headers.Add("X-Frame-Options", "DENY");
        }

        // X-Content-Type-Options: Prevents MIME type sniffing
        if (!headers.ContainsKey("X-Content-Type-Options"))
        {
            headers.Add("X-Content-Type-Options", "nosniff");
        }

        // X-XSS-Protection: Enables XSS filtering in older browsers
        if (!headers.ContainsKey("X-XSS-Protection"))
        {
            headers.Add("X-XSS-Protection", "1; mode=block");
        }

        // Referrer-Policy: Controls how much referrer information should be included
        if (!headers.ContainsKey("Referrer-Policy"))
        {
            headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        }

        // X-Permitted-Cross-Domain-Policies: Restricts Adobe Flash and PDF cross-domain policies
        if (!headers.ContainsKey("X-Permitted-Cross-Domain-Policies"))
        {
            headers.Add("X-Permitted-Cross-Domain-Policies", "none");
        }

        // Content Security Policy: Helps prevent XSS attacks
        if (!headers.ContainsKey("Content-Security-Policy"))
        {
            var cspValue = BuildContentSecurityPolicy();
            headers.Add("Content-Security-Policy", cspValue);
        }

        // Strict-Transport-Security (HSTS): Forces HTTPS connections
        if (context.Request.IsHttps && !headers.ContainsKey("Strict-Transport-Security"))
        {
            // Only add HSTS header over HTTPS connections
            var hstsValue = _environment.IsDevelopment() 
                ? "max-age=31536000" // 1 year for development
                : "max-age=63072000; includeSubDomains; preload"; // 2 years with subdomains for production
            headers.Add("Strict-Transport-Security", hstsValue);
        }

        // Permissions-Policy (formerly Feature-Policy): Controls browser features
        if (!headers.ContainsKey("Permissions-Policy"))
        {
            headers.Add("Permissions-Policy", 
                "camera=(), microphone=(), geolocation=(), payment=(), usb=(), magnetometer=(), gyroscope=(), speaker=()");
        }

        // Cross-Origin-Embedder-Policy: Helps enable crossOriginIsolated state
        if (!headers.ContainsKey("Cross-Origin-Embedder-Policy"))
        {
            headers.Add("Cross-Origin-Embedder-Policy", "require-corp");
        }

        // Cross-Origin-Opener-Policy: Helps isolate browsing context
        if (!headers.ContainsKey("Cross-Origin-Opener-Policy"))
        {
            headers.Add("Cross-Origin-Opener-Policy", "same-origin");
        }

        // Cache-Control for API responses
        if (!headers.ContainsKey("Cache-Control") && IsApiEndpoint(context))
        {
            headers.Add("Cache-Control", "no-store, no-cache, must-revalidate");
            headers.Add("Pragma", "no-cache");
        }
    }

    private string BuildContentSecurityPolicy()
    {
        // Restrictive CSP for API - adjust as needed for your frontend requirements
        var policies = new[]
        {
            "default-src 'none'", // Start with nothing allowed
            "script-src 'self'", // Only allow scripts from same origin
            "style-src 'self' 'unsafe-inline'", // Allow styles from same origin and inline (for Swagger)
            "img-src 'self' data:", // Allow images from same origin and data URLs
            "font-src 'self'", // Allow fonts from same origin
            "connect-src 'self'", // Allow AJAX requests to same origin
            "base-uri 'self'", // Restrict base element URLs
            "form-action 'self'", // Restrict form submission targets
            "frame-ancestors 'none'", // No frames allowed (redundant with X-Frame-Options)
            "object-src 'none'", // No plugins
            "media-src 'none'", // No media
            "worker-src 'none'", // No web workers
            "manifest-src 'none'" // No web manifests
        };

        // Add additional allowed sources for development (Swagger UI)
        if (_environment.IsDevelopment())
        {
            return string.Join("; ", policies.Select(p => 
                p == "script-src 'self'" ? "script-src 'self' 'unsafe-inline' 'unsafe-eval'" : p));
        }

        return string.Join("; ", policies);
    }

    private static bool IsApiEndpoint(HttpContext context)
    {
        return context.Request.Path.StartsWithSegments("/api");
    }
}

/// <summary>
/// Extension methods for adding security headers middleware
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    /// <summary>
    /// Adds the security headers middleware to the application pipeline
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder</returns>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}