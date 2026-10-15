using System.Net;
using Amaliyotchi.Application.Common.Caching;
using Amaliyotchi.Application.Features.Admin.Dashboard;
using Amaliyotchi.Application.Features.Admin.Nav;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.Domain.Organization;
using Amaliyotchi.IntegrationTests.Infrastructure;
using Amaliyotchi.IntegrationTests.Supervision;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary>O'qish keshi (HybridCache) va uning bekor qilinishi: keshlangan javoblar (nav, dashboard, filtrlar) har qanday
/// yozuvdan keyin darhol yangilanadi — admin amalidan keyin eski badge ko'rinmaydi. Bekor qilish handler'larda emas,
/// <c>CacheInvalidationInterceptor</c> da (SaveChanges / tranzaksiya commit) — shu yerda tekshiriladi.</summary>
[Collection(ApiCollection.Name)]
public sealed class CacheInvalidationTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    private IAppCache Cache => Factory.Services.GetRequiredService<IAppCache>();

    /// <summary>Kesh haqiqatan ishlaydi: ikkinchi o'qish factory'ni chaqirmaydi; tegishli entity saqlansa — chaqiradi.</summary>
    [Fact]
    public async Task Interceptor_TegishliEntitySaqlansa_TegBekorQilinadi_BoshqasidaQolmaydi()
    {
        var key = $"test:probe:{Guid.NewGuid():N}";
        var calls = 0;
        ValueTask<int> Probe() => Cache.GetOrCreateAsync(
            key, _ => ValueTask.FromResult(++calls), TimeSpan.FromMinutes(5), [CacheTags.Organization]);

        (await Probe()).Should().Be(1);
        (await Probe()).Should().Be(1, "ikkinchi o'qish keshdan");

        // Organization tegiga bog'lanmagan entity (Company — faqat nav/dashboard tegi) bu kalitni tozalamaydi.
        await Factory.CreateCompanyAsync();
        (await Probe()).Should().Be(1, "Company Organization tegiga kirmaydi");

        // Tuzilma yozuvi (fakultet) — Organization tegi bekor bo'ladi.
        await Factory.WithDbAsync(async db =>
        {
            var marker = Guid.NewGuid().ToString("N")[..6];
            db.Faculties.Add(Faculty.Create($"Kesh fakulteti {marker}", $"K{marker[..4]}".ToUpperInvariant()));
            await db.SaveChangesAsync();
        });
        (await Probe()).Should().Be(2, "fakultet saqlangach kesh bekor qilinadi");
        (await Probe()).Should().Be(2);
    }

    /// <summary>Tranzaksiya ichida: SaveChanges'dan keyin ham, commit'dan keyin ham tozalanadi — commit'gacha boshqa so'rov
    /// eski holatni qayta keshlab qo'ysa ham u commit'da o'chiriladi. Rollback'da qo'shimcha tozalash yo'q.</summary>
    [Fact]
    public async Task Interceptor_Tranzaksiya_CommitdanKeyinYanaTozalaydi()
    {
        var key = $"test:probe:{Guid.NewGuid():N}";
        var calls = 0;
        ValueTask<int> Probe() => Cache.GetOrCreateAsync(
            key, _ => ValueTask.FromResult(++calls), TimeSpan.FromMinutes(5), [CacheTags.Organization]);

        (await Probe()).Should().Be(1);

        // Ishlab chiqarishdagi kabi (DbTransactions): retry strategiyasi ichida foydalanuvchi tranzaksiyasi.
        await Factory.WithDbAsync(db => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var marker = Guid.NewGuid().ToString("N")[..6];
            db.Faculties.Add(Faculty.Create($"Tx fakulteti {marker}", $"T{marker[..4]}".ToUpperInvariant()));
            await db.SaveChangesAsync();

            (await Probe()).Should().Be(2, "SaveChanges'dan keyin darhol tozalangan");
            (await Probe()).Should().Be(2, "commit'gacha qayta keshlangan (poyga holati)");

            await transaction.CommitAsync();
        }));

        (await Probe()).Should().Be(3, "commit'dan keyin yana tozalanadi");
    }

    /// <summary>Frontend ariza qaroridan keyin nav'ni qayta so'raydi — server keshi ham shu zahoti yangi son berishi shart.</summary>
    [Fact]
    public async Task Nav_ArizaQarorida_BadgeDarhol_Kamayadi()
    {
        var s = await Factory.CreateAdminScenarioAsync(approved: false);

        var before = (await (await s.Client.GetAsync("/api/admin/nav")).Content.ReadAsync<AdminNavDto>())!;
        var cached = (await (await s.Client.GetAsync("/api/admin/nav")).Content.ReadAsync<AdminNavDto>())!;
        cached.Should().BeEquivalentTo(before);
        before.Counts.Applications.Should().BePositive();

        var decision = await s.Client.PostJsonAsync($"/api/admin/applications/{s.Application.Id}/decision",
            new { decision = "approve", radiusM = 200, checklist = new[] { 0, 1, 2, 6 }, comment = "OK" });
        decision.StatusCode.Should().Be(HttpStatusCode.OK, await decision.Content.ReadAsStringAsync());

        var after = (await (await s.Client.GetAsync("/api/admin/nav")).Content.ReadAsync<AdminNavDto>())!;
        after.Counts.Applications.Should().Be(before.Counts.Applications - 1, "qaror nav keshini bekor qiladi");
    }

    /// <summary>Talaba (yoki boshqa admin) amali ham keshni bekor qiladi — bu yerda to'g'ridan-to'g'ri DB yozuvi (EF).</summary>
    [Fact]
    public async Task Nav_VaDashboard_YangiTalabadan_KeyinDarhol_Yangilanadi()
    {
        var client = await Factory.LoginAsAdminAsync();
        var group = await Factory.CreateGroupAsync();

        var nav = (await (await client.GetAsync("/api/admin/nav")).Content.ReadAsync<AdminNavDto>())!;
        var dashboard = (await (await client.GetAsync("/api/admin/dashboard")).Content.ReadAsync<AdminDashboardDto>())!;

        await Factory.CreateStudentAsync(group: group);

        var navAfter = (await (await client.GetAsync("/api/admin/nav")).Content.ReadAsync<AdminNavDto>())!;
        var dashboardAfter = (await (await client.GetAsync("/api/admin/dashboard")).Content.ReadAsync<AdminDashboardDto>())!;
        navAfter.Counts.Students.Should().Be(nav.Counts.Students + 1);
        dashboardAfter.Stats.StudentsTotal.Should().Be(dashboard.Stats.StudentsTotal + 1);
    }

    /// <summary>Ma'lumotnoma (talabalar filtri) keshlanadi va yangi fakultet API orqali qo'shilgach darhol ko'rinadi.</summary>
    [Fact]
    public async Task Filtrlar_YangiFakultet_DarholKorinadi()
    {
        var client = await Factory.LoginAsAdminAsync();
        _ = await client.GetFromJsonAsync<AdminStudentFiltersDto>("/api/admin/students/filters");

        var marker = Guid.NewGuid().ToString("N")[..6];
        var created = await client.PostJsonAsync("/api/admin/faculties", new { name = $"Filtr Fakulteti {marker}", code = $"f{marker[..4]}" });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        var filters = (await client.GetFromJsonAsync<AdminStudentFiltersDto>("/api/admin/students/filters"))!;
        filters.Faculties.Should().Contain(f => f.Name == $"Filtr Fakulteti {marker}");
    }

    /// <summary>ExecuteUpdate interceptor'dan o'tmaydi — bunday yo'llar <see cref="IAppCache.InvalidateAsync"/> ni o'zi chaqiradi.</summary>
    [Fact]
    public async Task InvalidateAsync_QoldaChaqirilganda_TegBekorQilinadi()
    {
        var key = $"test:probe:{Guid.NewGuid():N}";
        var calls = 0;
        ValueTask<int> Probe() => Cache.GetOrCreateAsync(
            key, _ => ValueTask.FromResult(++calls), TimeSpan.FromMinutes(5), [CacheTags.AdminDashboard]);

        (await Probe()).Should().Be(1);
        await Cache.InvalidateAsync(CacheTags.All);
        (await Probe()).Should().Be(2);
    }
}
