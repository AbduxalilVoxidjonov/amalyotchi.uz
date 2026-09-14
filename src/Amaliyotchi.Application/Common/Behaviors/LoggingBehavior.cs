using Amaliyotchi.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Amaliyotchi.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<TRequest> logger,
    ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // So'rov tanasi log'ga yozilmaydi: unda parol va shaxsiy ma'lumot bo'lishi mumkin.
        logger.LogInformation(
            "So'rov: {RequestName} · foydalanuvchi {UserId} ({Role})",
            typeof(TRequest).Name,
            currentUser.UserId,
            currentUser.Role);

        return await next();
    }
}
