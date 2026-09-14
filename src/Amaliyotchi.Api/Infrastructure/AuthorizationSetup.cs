using System.Text;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Amaliyotchi.Api.Infrastructure;

public static class AuthorizationSetup
{
    public static IServiceCollection AddJwtAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt bo'limi sozlanmagan.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    // Inbound map "name" ni ClaimTypes.Name ga xaritalamaydi (faqat "unique_name"),
                    // shuning uchun User.Identity.Name to'ldirilishi uchun aniq ko'rsatiladi.
                    // RoleClaimType qo'yilmaydi: "role" ni inbound map o'zi ClaimTypes.Role ga xaritalaydi.
                    NameClaimType = JwtRegisteredClaimNames.Name
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AdminOnly, p => p.RequireRole(nameof(UserRole.Admin)))
            // Baholash, kundalik tekshiruvi, ariza/ruxsat qarorlari — RBAC bo'yicha faqat tyutor (admin baho qo'ymaydi).
            .AddPolicy(Policies.TutorOnly, p => p.RequireRole(nameof(UserRole.Tutor)))
            .AddPolicy(Policies.StudentOnly, p => p.RequireRole(nameof(UserRole.Student)))
            // Tyutor GET'lari: admin ham ko'radi (ko'lami — hammasi).
            .AddPolicy(Policies.TutorOrAdmin, p => p.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Tutor)))
            .AddPolicy(Policies.Authenticated, p => p.RequireAuthenticatedUser());

        return services;
    }
}
