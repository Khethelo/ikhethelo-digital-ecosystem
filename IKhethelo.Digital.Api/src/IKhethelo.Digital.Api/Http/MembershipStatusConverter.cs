using System.Text.Json;
using System.Text.Json.Serialization;
using IKhethelo.Digital.Domain;

namespace IKhethelo.Digital.Api.Http;

// Existing tenants continue to receive their established wire names. ESHL uses approval terminology.
public sealed class MembershipStatusConverter : JsonConverter<MembershipApplicationStatus>
{
    private readonly IHttpContextAccessor context = new HttpContextAccessor();
    private readonly StrictEnumConverter<MembershipApplicationStatus> strict = new();
    public override MembershipApplicationStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        strict.Read(ref reader, typeToConvert, options);
    public override void Write(Utf8JsonWriter writer, MembershipApplicationStatus value, JsonSerializerOptions options)
    {
        var tenant = context.HttpContext?.Items["ResolvedTenantCode"] as string;
        var name = tenant is not null and not "ESHL" ? value switch
        {
            MembershipApplicationStatus.Pending => "AwaitingInPersonCompletion",
            MembershipApplicationStatus.Approved => "Completed",
            MembershipApplicationStatus.Rejected => "LegacyClosed",
            _ => throw new JsonException("Invalid membership status.")
        } : value.ToString();
        writer.WriteStringValue(name);
    }
}
