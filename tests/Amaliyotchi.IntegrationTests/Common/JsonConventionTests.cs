using System.Net;
using System.Text.Json;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Common;

[Collection(ApiCollection.Name)]
public sealed class JsonConventionTests(ApiFixture fixture)
{
    [Fact]
    public async Task Javob_CamelCaseProperty_VaEnumCamelCaseString()
    {
        var admin = await fixture.Factory.CreateAdminAsync();
        var (_, _) = await fixture.Factory.LoginWithResultAsync(admin);

        var response = await fixture.Factory.CreateClient()
            .PostJsonAsync("/api/auth/login", new { admin.HemisId, admin.Password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        root.TryGetProperty("accessToken", out _).Should().BeTrue("property'lar camelCase");
        root.TryGetProperty("AccessToken", out _).Should().BeFalse();

        var user = root.GetProperty("user");
        user.GetProperty("role").ValueKind.Should().Be(JsonValueKind.String, "enum raqam emas, string");
        user.GetProperty("role").GetString().Should().Be("admin");
        user.GetProperty("isSuperAdmin").ValueKind.Should().Be(JsonValueKind.False);
        user.GetProperty("facultyId").ValueKind.Should().Be(JsonValueKind.Null, "admin fakultetga biriktirilmaydi");
    }

    [Fact]
    public async Task ProblemDetails_CamelCase_ErrorsKalitlariPascalCase()
    {
        var response = await fixture.Factory.CreateClient()
            .PostJsonAsync("/api/auth/login", new { HemisId = "", Password = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(400);
        root.TryGetProperty("traceId", out _).Should().BeTrue();
        var errors = root.GetProperty("errors");
        errors.TryGetProperty("HemisId", out _).Should().BeTrue("FluentValidation kalitlari PascalCase qoladi");
        errors.TryGetProperty("Password", out _).Should().BeTrue();
    }

    [Fact]
    public void Enum_CamelCaseStringdanOqiladi()
    {
        var dto = JsonSerializer.Deserialize<RoleHolder>("""{"role":"student"}""", JsonDefaults.Options);

        dto!.Role.Should().Be(Amaliyotchi.Domain.Enums.UserRole.Student);
    }

    private sealed record RoleHolder(Amaliyotchi.Domain.Enums.UserRole Role);
}
