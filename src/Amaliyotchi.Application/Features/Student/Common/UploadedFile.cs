namespace Amaliyotchi.Application.Features.Student.Common;

/// <summary>Multipart'dan kelgan fayl — Application ASP.NET <c>IFormFile</c> ga bog'lanmaydi.
/// <paramref name="OpenRead"/> har chaqirilganda yangi oqim beradi; chaqiruvchi yopadi.
/// Kundalik ilovalari ham, check-in selfisi ham shu tur orqali o'tadi.</summary>
public sealed record UploadedFile(string FileName, string ContentType, long Length, Func<Stream> OpenRead);
