using System.Diagnostics;
using Storage.Infrastructure.Helpers.Redaction;

namespace Storage.Api.Middlewares;

public class LogMiddleware
{
    private readonly RequestDelegate _next; 
    private const int MaxBufferableBodyBytes = 1024 * 1024; // 1 MB
    private const int MaxPayloadLength = 4096;

    const string LogMessageTemplate =
        "HTTP {Direction} {RequestMethod} {RequestPath} {RequestPayload} responded {HttpStatusCode} {ResponsePayload} in {Elapsed:0.0000} ms";

    public LogMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext httpContext, ILogger<LogMiddleware> logger)
    {
        // Redact request body
        string safeRequestBody = await BuildSafeRequestBodyAsync(httpContext.Request);
        safeRequestBody = Truncate(safeRequestBody, MaxPayloadLength);

        // Copy a pointer to the original response body stream
        Stream originalBodyStream = httpContext.Response.Body;

        // Create a new memory stream and use it for the temporary response body
        using var newMemoryStream = new MemoryStream();
        httpContext.Response.Body = newMemoryStream;

        var sw = Stopwatch.StartNew();
        try
        {
            await _next(httpContext);
        }
        finally
        {
            sw.Stop();

            // Only JSON/text responses are read and redacted.
            // binary responses (e.g. file downloads) are summarised from headers, never buffered.
            string safeResponseBody = BuildSafeResponseBody(httpContext.Response);
            safeResponseBody = Truncate(safeResponseBody, MaxPayloadLength);

            int statusCode = httpContext.Response.StatusCode;
            LogLevel loglevel = statusCode > 499 ? LogLevel.Error : LogLevel.Information;

            // Log using Serilog          
            logger.Log(loglevel, LogMessageTemplate, "Incoming", httpContext.Request.Method,
              httpContext.Request.Path, safeRequestBody, statusCode, safeResponseBody, (long)sw.Elapsed.TotalMilliseconds);

            httpContext.Response.Body = originalBodyStream;

            if (!httpContext.Response.HasStarted)
            {
                newMemoryStream.Seek(0, SeekOrigin.Begin);
                await newMemoryStream.CopyToAsync(originalBodyStream);
            }
        }
    }

    private static async Task<string> BuildSafeRequestBodyAsync(HttpRequest request)
    {        
        bool isJson = request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;
        bool isForm = request.ContentType?.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true;

        // Only JSON and form bodies & Get requests  are worth logging.
        // Anything else with an actual body (octet-stream, etc.) gets a placeholder.
        // A request with no body (e.g. GET) logs all the QueryString.
        if (!isJson && !isForm)
            return HasBody(request)
                ? DescribeBodyWithoutReading(request)
                : (request.QueryString.HasValue ? request.QueryString.Value!.TrimStart('?') : string.Empty);

        // Don't buffer a large upload into a string only to redact/truncate it away.
        if (request.ContentLength is long len && len > MaxBufferableBodyBytes)
            return DescribeBodyWithoutReading(request);

        // Small enough to read: buffer, read, rewind for the controller.
        var body = await GetRequestBody(request);

        return isJson
            ? JsonRedactor.TryRedact(body)
            : MultipartFormDataRedactor.TryRedact(body);
    }

    private static string Truncate(string input, int maxLen)
    {
        if (string.IsNullOrEmpty(input) || input.Length <= maxLen) return input;
        return input.Substring(0, maxLen) + "...(truncated)";
    }

    private static bool HasBody(HttpRequest request)
    {
        if (request.ContentLength is long len)
            return len > 0;
        return !string.IsNullOrEmpty(request.ContentType);
    }    
    
    private static string DescribeBodyWithoutReading(HttpRequest request)
    {
        var contentType = request.ContentType ?? "unknown";
        return request.ContentLength is long len
            ? $"[{contentType}; {len} bytes — body not logged]"
            : $"[{contentType}; body not logged]";
    }

    private static async Task<string> GetRequestBody(HttpRequest request)
    {
        request.EnableBuffering();
        string body = await new StreamReader(request.Body).ReadToEndAsync();
        request.Body.Seek(0, SeekOrigin.Begin);
        return body;
    }

    private static string BuildSafeResponseBody(HttpResponse response)
    {
        var contentType = response.ContentType ?? "";

        bool isTextual =
            contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
            contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);

        if (!isTextual)
        {
            var ct = string.IsNullOrEmpty(contentType) ? "unknown" : contentType;
            return $"[{ct}; {response.ContentLength?.ToString() ?? "?"} bytes — body not logged]";
        }

        // Read the buffered response, then rewind so it can still be copied to
        // the real response stream below (leaveOpen keeps the MemoryStream open).
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        string body = reader.ReadToEnd();
        response.Body.Seek(0, SeekOrigin.Begin);

        return JsonRedactor.TryRedact(body);
    }   
}