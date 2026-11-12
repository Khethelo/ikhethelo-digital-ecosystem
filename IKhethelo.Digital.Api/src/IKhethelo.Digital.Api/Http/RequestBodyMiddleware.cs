using System.Text.Json;
using IKhethelo.Digital.Application;

namespace IKhethelo.Digital.Api.Http;

public sealed class RequestBodyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (!request.Path.StartsWithSegments("/api/v1") || request.Method is not ("POST" or "PUT" or "PATCH")) { await next(context); return; }
        if (!request.HasJsonContentType()) { await Problems.WriteAsync(context, 415); return; }
        if (request.ContentLength > 16384) { await Problems.WriteAsync(context, 413); return; }
        request.EnableBuffering();
        // Count streamed/chunked bodies as well as Content-Length bodies.
        using var buffer = new MemoryStream();
        var block = new byte[4096]; int read;
        while ((read = await request.Body.ReadAsync(block, context.RequestAborted)) > 0)
        {
            if (buffer.Length + read > 16384) { await Problems.WriteAsync(context, 413); return; }
            buffer.Write(block, 0, read);
        }
        request.Body.Position = 0;
        JsonDocument document;
        try { document = JsonDocument.Parse(buffer.ToArray()); }
        catch (JsonException) { await Problems.WriteAsync(context, 400); return; }
        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object) { await Problems.WriteAsync(context, 400); return; }
            // MVC's unmapped-member rejection validates each DTO's actual properties.
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new RequestFailure(400, "Unknown or duplicate request property.");
                if (property.Name == "reviewNotes" && property.Value.ValueKind == JsonValueKind.Null &&
                    request.Path.Value!.EndsWith("/activate", StringComparison.Ordinal))
                    throw new RequestFailure(422, "One or more validation errors occurred.", new() { ["reviewNotes"] = ["Omit absent reviewNotes instead of supplying null."] });
            }
        }
        await next(context);
    }
}
