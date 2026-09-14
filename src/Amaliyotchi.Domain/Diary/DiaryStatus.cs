namespace Amaliyotchi.Domain.Diary;

/// <summary>Kundalik yozuvi holati: yuborildi → tyutor ko'rdi → tasdiqlandi (ball bilan) / qayta yozish.</summary>
public enum DiaryStatus
{
    Submitted = 1,
    Seen = 2,
    Rewrite = 3,
    Approved = 4
}
