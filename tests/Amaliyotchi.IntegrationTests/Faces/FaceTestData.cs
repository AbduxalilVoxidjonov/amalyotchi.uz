using System.Net.Http.Headers;
using System.Text.Json;
using Amaliyotchi.Application.Features.Faces;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Faces;

internal static class FaceTestData
{
    public static MultipartFormDataContent FaceForm(byte[]? photo, string? consent = "true", string contentType = "image/jpeg")
    {
        var form = new MultipartFormDataContent();
        if (photo is not null)
        {
            var part = new ByteArrayContent(photo);
            part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(part, "photo", "etalon.jpg");
        }

        if (consent is not null)
            form.Add(new StringContent(consent), "consent");
        return form;
    }

    /// <summary>Talaba API orqali etalon yuboradi (200 bo'lishi shart).</summary>
    public static async Task<StudentFaceDto> EnrollAsync(this HttpClient student, string identity)
    {
        using var form = FaceForm(FakeFaceEngine.Photo(identity));
        var response = await student.PostAsync("/api/student/face", form);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadAsync<StudentFaceDto>())!;
    }

    public static async Task<JsonElement> ProblemAsync(this HttpResponseMessage response)
    {
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    public static string FirstError(this JsonElement problem, string field)
        => problem.GetProperty("errors").GetProperty(field)[0].GetString()!;
}
