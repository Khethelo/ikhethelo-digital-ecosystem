using System.Text.Json;
using IKhethelo.Digital.Application;
using Microsoft.OpenApi.Readers;
using Xunit;

namespace IKhethelo.Digital.Tests;

// Contract validation does not need a database or a test server.
public sealed class NtmContractTests
{
    [Fact]
    public void NtmSchemasMatchDtosAndAllNineOperationsRequireBearerAuthentication()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Contract", "ikhethelo-digital-api.openapi.yaml");
        var contract = new OpenApiStringReader().Read(File.ReadAllText(path), out var diagnostics);
        Assert.Empty(diagnostics.Errors);
        Assert.Equal(9, contract.Paths.Where(x => x.Key.Contains("/ntm/", StringComparison.Ordinal)).Sum(x => x.Value.Operations.Count));
        Assert.Single(contract.SecurityRequirements);
        foreach (var operation in contract.Paths.Where(x => x.Key.Contains("/ntm/", StringComparison.Ordinal)).SelectMany(x => x.Value.Operations.Values))
            Assert.Empty(operation.Security); // Empty per-operation collection inherits the nonempty global Bearer requirement in this contract.
        foreach (var (schema, type) in new (string, Type)[]
        {
            ("NtmVeteranWrite", typeof(NtmVeteranWrite)), ("NtmVeteran", typeof(NtmVeteranResponse)),
            ("NtmBusinessWrite", typeof(NtmBusinessWrite)), ("NtmBusiness", typeof(NtmBusinessResponse)),
            ("NtmDashboard", typeof(NtmDashboard))
        })
            Assert.Equal(type.GetProperties().Select(x => JsonNamingPolicy.CamelCase.ConvertName(x.Name)).Order(), contract.Components.Schemas[schema].Properties.Keys.Order());
    }
}
