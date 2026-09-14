using System.Reflection;
using Amaliyotchi.Application.Common.Behaviors;
using Amaliyotchi.Application.Common.Scoping;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Amaliyotchi.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);

        // Tartib muhim: log → o'lchov → validatsiya → handler.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Ma'lumot ko'lami: so'rov davomida bir marta hisoblanadi.
        services.AddScoped<IScopeResolver, ScopeResolver>();

        return services;
    }
}
