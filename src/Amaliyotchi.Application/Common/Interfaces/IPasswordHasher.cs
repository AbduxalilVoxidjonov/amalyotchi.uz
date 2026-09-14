namespace Amaliyotchi.Application.Common.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>Xesh formati eskirgan bo'lsa <c>needsRehash</c> true qaytadi —
    /// muvaffaqiyatli kirishda parolni jimgina yangi formatga o'tkazish uchun.</summary>
    bool Verify(string password, string hash, out bool needsRehash);
}
