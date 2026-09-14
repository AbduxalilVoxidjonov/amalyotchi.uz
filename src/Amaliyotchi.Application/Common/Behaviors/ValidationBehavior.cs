using FluentValidation;
using MediatR;
using ValidationException = Amaliyotchi.Application.Common.Exceptions.ValidationException;

namespace Amaliyotchi.Application.Common.Behaviors;

/// <summary>Validatsiya har bir handler ichida emas, quvurda bir marta bajariladi.</summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var applicable = validators.ToArray();
        if (applicable.Length == 0)
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(applicable.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next();
    }
}
