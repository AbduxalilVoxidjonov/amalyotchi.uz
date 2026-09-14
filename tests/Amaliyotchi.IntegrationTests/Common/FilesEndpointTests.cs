using System.Net;
using System.Text;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Amaliyotchi.IntegrationTests.Common;

[Collection(ApiCollection.Name)]
public sealed class FilesEndpointTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task SaqlanganFayl_AuthBilan_AslNomVaContentTypeBilanQaytadi()
    {
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.4 test");
        var admin = await Factory.CreateAdminAsync();
        var file = await Factory.CreateStoredFileAsync(admin.Id, fileName: "Shartnoma Aliyev.PDF", content: bytes);
        file.StoragePath.Should().MatchRegex(@"^\d{4}/\d{2}/[0-9a-f]{32}\.pdf$");

        var client = await Factory.LoginAsync(admin);
        var response = await client.GetAsync($"/api/files/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        response.Content.Headers.ContentDisposition.FileNameStar.Should().Be("Shartnoma Aliyev.PDF");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);

        var storage = Factory.Services.GetRequiredService<IFileStorage>();
        await storage.DeleteAsync(file.StoragePath);
        (await storage.OpenReadAsync(file.StoragePath)).Should().BeNull();

        // Yozuv bor, fayl diskda yo'q — 404.
        (await client.GetAsync($"/api/files/{file.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Fayl_Tokensiz_401()
    {
        var response = await Factory.CreateClient().GetAsync($"/api/files/{Guid.CreateVersion7()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("2026/09/yoq.pdf")]
    [InlineData("../appsettings.json")]
    public async Task YoqYokiNotogriId_404(string id)
    {
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.GetAsync($"/api/files/{id}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }
}
