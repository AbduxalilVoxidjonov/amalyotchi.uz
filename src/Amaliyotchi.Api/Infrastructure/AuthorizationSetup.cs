using System.Security.Claims;
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

                // Imzo va muddat to'g'ri bo'lsa ham: hisob faolsizlantirilgan/o'chirilgan yoki parol, login, rol,
                // fakultet o'zgargan bo'lsa token rad etiladi (401). Tekshiruv keshlangan — har so'rovda DB emas.
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;
                        var subject = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                        var stamp = principal?.FindFirst(UserSessionValidator.StampClaim)?.Value;

                        var sessions = context.HttpContext.RequestServices.GetRequiredService<UserSessionValidator>();
                        if (!Guid.TryParse(subject, out var userId)
                            || !await sessions.IsCurrentAsync(userId, stamp, context.HttpContext.RequestAborted))
                        {
                            context.Fail("Sessiya eskirgan: hisob holati yoki parol/login o'zgargan.");
                        }
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AdminOnly, p => p.RequireRole(nameof(UserRole.Admin)))
            // Bosh admin: claim login/refresh'da bazadan yoziladi; belgi o'zgarsa stamp o'zgaradi → eski token 401,
            // mijoz refresh qiladi va yangi claim oladi. Handler'lar bazadan qo'shimcha tekshiradi.
            .AddPolicy(Policies.SuperAdminOnly, p => p
                .RequireRole(nameof(UserRole.Admin))
                .RequireClaim(JwtTokenService.SuperAdminClaim, "true"))
            .AddPolicy(Policies.StudentOnly, p => p.RequireRole(nameof(UserRole.Student)))
            .AddPolicy(Policies.Authenticated, p => p.RequireAuthenticatedUser());

        return services;
    }
}
