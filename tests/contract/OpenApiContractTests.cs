using System;
using System.Threading.Tasks;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace PubDevMcp.Tests.Contract;

public class OpenApiContractTests
{
    private readonly OpenApiContractFixture _fixture = new();

    [Fact(DisplayName = "OpenAPI contract includes all MCP tool endpoints")]
    public async Task GeneratedContractMatchesApprovedSchemaAsync()
    {
        var generatedContract = await _fixture.GetGeneratedContractAsync();

        await Verifier.Verify(generatedContract);
    }

    private sealed class OpenApiContractFixture
    {
        public Task<string> GetGeneratedContractAsync()
        {
            throw new NotImplementedException("OpenAPI contract generation pipeline is not implemented yet.");
        }
    }
}
