namespace Amaliyotchi.Application.Features.Student.Diary;

/// <summary>Multipart'dan kelgan fayl — Application ASP.NET <c>IFormFile</c> ga bog'lanmaydi.
/// <paramref name="OpenRead"/> har chaqirilganda yangi oqim beradi; chaqiruvchi yopadi.</summary>
public sealed record UploadedFile(string FileName, string ContentType, long Length, Func<Stream> OpenRead);
