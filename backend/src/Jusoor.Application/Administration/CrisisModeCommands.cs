using System.Text.Json;
using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Administration;

public sealed record CrisisModeDto(
    bool IsActive,
    string Title,
    string Summary,
    string SourceName,
    string SourceUrl,
    string Severity,
    IReadOnlyList<Guid> AffectedCountryIds,
    DateTimeOffset? LastModifiedAtUtc);

public sealed record GetCrisisModeQuery : IRequest<CrisisModeDto>;
public sealed record GetPublicCrisisModeQuery : IRequest<CrisisModeDto>;

public sealed class GetCrisisModeQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCrisisModeQuery, CrisisModeDto>
{
    public async Task<CrisisModeDto> Handle(GetCrisisModeQuery request, CancellationToken cancellationToken)
    {
        var mode = await context.CrisisModes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Key == CrisisMode.SingletonKey, cancellationToken);
        return mode is null
            ? new CrisisModeDto(false, string.Empty, string.Empty, string.Empty, string.Empty, "urgent", Array.Empty<Guid>(), null)
            : new CrisisModeDto(mode.IsActive, mode.Title, mode.Summary, mode.SourceName, mode.SourceUrl, mode.Severity,
                DeserializeCountryIds(mode.AffectedCountryIdsJson), mode.LastModifiedAtUtc);
    }

    internal static IReadOnlyList<Guid> DeserializeCountryIds(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Guid[]>(json) ?? Array.Empty<Guid>();
        }
        catch (JsonException)
        {
            return Array.Empty<Guid>();
        }
    }
}

public sealed class GetPublicCrisisModeQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPublicCrisisModeQuery, CrisisModeDto>
{
    public async Task<CrisisModeDto> Handle(GetPublicCrisisModeQuery request, CancellationToken cancellationToken)
    {
        var mode = await context.CrisisModes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Key == CrisisMode.SingletonKey && item.IsActive, cancellationToken);
        return mode is null
            ? new CrisisModeDto(false, string.Empty, string.Empty, string.Empty, string.Empty, "urgent", Array.Empty<Guid>(), null)
            : new CrisisModeDto(true, mode.Title, mode.Summary, mode.SourceName, mode.SourceUrl, mode.Severity,
                GetCrisisModeQueryHandler.DeserializeCountryIds(mode.AffectedCountryIdsJson), mode.LastModifiedAtUtc);
    }
}

public sealed record SaveCrisisModeCommand(
    bool IsActive,
    string Title,
    string Summary,
    string SourceName,
    string SourceUrl,
    string Severity,
    IReadOnlyList<Guid> AffectedCountryIds) : IRequest<CrisisModeDto>;

public sealed class SaveCrisisModeCommandValidator : AbstractValidator<SaveCrisisModeCommand>
{
    public SaveCrisisModeCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(CrisisMode.TitleMaxLength);
        RuleFor(x => x.Summary).NotEmpty().MaximumLength(CrisisMode.SummaryMaxLength);
        RuleFor(x => x.SourceName).MaximumLength(CrisisMode.SourceNameMaxLength)
            .Must((request, value) => !request.IsActive || !string.IsNullOrWhiteSpace(value))
            .WithMessage("أضف اسم المصدر قبل تفعيل وضع الأزمة.");
        RuleFor(x => x.SourceUrl).MaximumLength(CrisisMode.SourceUrlMaxLength)
            .Must((request, value) => !request.IsActive || IsHttpUrl(value))
            .WithMessage("أضف رابط مصدر موثوق (HTTP أو HTTPS) قبل التفعيل.")
            .Must(value => string.IsNullOrWhiteSpace(value) || IsHttpUrl(value))
            .WithMessage("رابط المصدر يجب أن يستخدم HTTP أو HTTPS.");
        RuleFor(x => x.Severity).Must(value => value is "advisory" or "urgent" or "emergency");
        RuleFor(x => x.AffectedCountryIds).NotNull().Must(ids => ids.Count <= 100)
            .WithMessage("يمكن تحديد 100 دولة كحد أقصى.");
        RuleFor(x => x).Must(x => !x.IsActive || x.AffectedCountryIds.Count > 0)
            .WithMessage("حدد دولة واحدة على الأقل قبل تفعيل وضع الأزمة.");
        RuleFor(x => x.AffectedCountryIds).Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("لا يمكن اختيار الدولة أكثر من مرة.");
    }

    private static bool IsHttpUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http";
}

public sealed class SaveCrisisModeCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    : IRequestHandler<SaveCrisisModeCommand, CrisisModeDto>
{
    public async Task<CrisisModeDto> Handle(SaveCrisisModeCommand request, CancellationToken cancellationToken)
    {
        var ids = request.AffectedCountryIds.Distinct().ToArray();
        var found = ids.Length == 0 ? 0 : await context.Countries.AsNoTracking()
            .CountAsync(country => ids.Contains(country.Id), cancellationToken);
        if (found != ids.Length)
            throw new ValidationException("تحتوي قائمة الدول على دولة غير معروفة.");

        var mode = await context.CrisisModes
            .SingleOrDefaultAsync(item => item.Key == CrisisMode.SingletonKey, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var countryJson = JsonSerializer.Serialize(ids);
        if (mode is null)
        {
            mode = CrisisMode.Create(request.IsActive, request.Title, request.Summary, request.SourceName, request.SourceUrl, request.Severity, countryJson);
            mode.CreatedAtUtc = now;
            mode.CreatedByUserId = currentUser.UserId;
            context.CrisisModes.Add(mode);
        }
        else
        {
            mode.Update(request.IsActive, request.Title, request.Summary, request.SourceName, request.SourceUrl, request.Severity, countryJson);
            mode.LastModifiedAtUtc = now;
            mode.LastModifiedByUserId = currentUser.UserId;
        }

        await context.SaveChangesAsync(cancellationToken);
        return new CrisisModeDto(mode.IsActive, mode.Title, mode.Summary, mode.SourceName, mode.SourceUrl, mode.Severity, ids, mode.LastModifiedAtUtc);
    }
}
