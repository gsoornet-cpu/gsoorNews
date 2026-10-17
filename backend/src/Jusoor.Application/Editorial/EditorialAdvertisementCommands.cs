using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Editorial;

public static class EditorialAdSlotCatalog
{
    public static readonly IReadOnlyDictionary<string, (string Label, string Surface)> Slots =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["navbar-banner"] = ("بانر الهيدر بين الشعار والبحث", "web"),
            ["hero-after-videos"] = ("بعد فيديوهات المقدمة", "mobile"),
            ["after-mughtarib"] = ("بعد أخبار المغتربين", "mobile"),
            ["success-ad"] = ("إعلان قسم قصص النجاح", "both"),
            ["atmaen-ad"] = ("داخل قسم اطمن", "both"),
            ["editorial-top"] = ("قبل خط أحمر ومع مسؤول", "web"),
            ["latest-news-square"] = ("أسفل أحدث الأخبار", "web"),
            ["lamma-top"] = ("أعلى قسم اللمة الحلوة", "web"),
            ["lamma-events-square"] = ("أسفل فعاليات وتغطيات", "web"),
            ["services-inline"] = ("بين خدمات الموقع", "web"),
        };

    public static bool Supports(string key, EditorialAdSurface surface)
        => Slots.TryGetValue(key, out var slot)
           && (slot.Surface == "both" || slot.Surface == (surface == EditorialAdSurface.Web ? "web" : "mobile"));
}

public sealed record EditorialAdPlacementInput(string SlotKey, EditorialAdSurface Surface, Guid MediaAssetId,
    string TargetUrl, string AltText, int DisplayOrder = 0);

public sealed record SaveEditorialAdPlacementsCommand(IReadOnlyList<EditorialAdPlacementInput> Items)
    : IRequest<EditorialAdPlacementsResult>;
public sealed record EditorialAdPlacementsResult(bool Succeeded, IReadOnlyList<EditorialAdPlacementDto> Items);

public sealed class SaveEditorialAdPlacementsCommandValidator : AbstractValidator<SaveEditorialAdPlacementsCommand>
{
    public SaveEditorialAdPlacementsCommandValidator()
    {
        RuleFor(x => x.Items).NotNull().Must(items => items.Count <= EditorialAdSlotCatalog.Slots.Count * 2);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.SlotKey).NotEmpty().Must((value, key) => EditorialAdSlotCatalog.Supports(key, value.Surface));
            item.RuleFor(x => x.MediaAssetId).NotEmpty();
            item.RuleFor(x => x.TargetUrl).NotEmpty().MaximumLength(EditorialAdPlacement.TargetUrlMaxLength)
                .Must(IsHttpUrl).WithMessage("Target URL must use HTTP or HTTPS.");
            item.RuleFor(x => x.AltText).NotEmpty().MaximumLength(EditorialAdPlacement.AltTextMaxLength);
            item.RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
        });
        RuleFor(x => x.Items).Must(items => items.Select(x => (x.SlotKey, x.Surface)).Distinct().Count() == items.Count)
            .WithMessage("A surface can have only one ad in each slot.");
    }

    private static bool IsHttpUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme is "http" or "https");
}

public sealed class SaveEditorialAdPlacementsCommandHandler : IRequestHandler<SaveEditorialAdPlacementsCommand, EditorialAdPlacementsResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IEditorialMediaStorage _storage;

    public SaveEditorialAdPlacementsCommandHandler(IApplicationDbContext context, IEditorialMediaStorage storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task<EditorialAdPlacementsResult> Handle(SaveEditorialAdPlacementsCommand request, CancellationToken cancellationToken)
    {
        var ids = request.Items.Select(x => x.MediaAssetId).Distinct().ToArray();
        var validIds = await _context.EditorialMediaAssets.AsNoTracking()
            .Where(x => ids.Contains(x.Id) && x.Kind == EditorialMediaKind.Image && x.Status == EditorialMediaStatus.Ready)
            .Select(x => x.Id).ToListAsync(cancellationToken);
        if (validIds.Count != ids.Length) return new(false, Array.Empty<EditorialAdPlacementDto>());

        var existing = await _context.EditorialAdPlacements.ToListAsync(cancellationToken);
        var requestedKeys = request.Items.Select(x => (x.SlotKey, x.Surface)).ToHashSet();
        _context.EditorialAdPlacements.RemoveRange(existing.Where(x => !requestedKeys.Contains((x.SlotKey, x.Surface))));
        foreach (var input in request.Items)
        {
            var row = existing.FirstOrDefault(x => x.SlotKey == input.SlotKey && x.Surface == input.Surface);
            if (row is null)
                _context.EditorialAdPlacements.Add(EditorialAdPlacement.Create(input.SlotKey, input.Surface,
                    input.MediaAssetId, input.TargetUrl, input.AltText, input.DisplayOrder));
            else row.Update(input.SlotKey, input.Surface, input.MediaAssetId, input.TargetUrl, input.AltText, input.DisplayOrder);
        }
        await _context.SaveChangesAsync(cancellationToken);
        return new(true, await EditorialAdPlacementQueries.Read(_context, _storage, cancellationToken));
    }
}

public sealed record EditorialAdPlacementDto(string SlotKey, string Surface, Guid MediaAssetId,
    string MediaUrl, string TargetUrl, string AltText, int DisplayOrder);
public sealed record GetEditorialAdPlacementsQuery : IRequest<IReadOnlyList<EditorialAdPlacementDto>>;

public sealed class GetEditorialAdPlacementsQueryHandler : IRequestHandler<GetEditorialAdPlacementsQuery, IReadOnlyList<EditorialAdPlacementDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IEditorialMediaStorage _storage;
    public GetEditorialAdPlacementsQueryHandler(IApplicationDbContext context, IEditorialMediaStorage storage)
    { _context = context; _storage = storage; }
    public Task<IReadOnlyList<EditorialAdPlacementDto>> Handle(GetEditorialAdPlacementsQuery request, CancellationToken cancellationToken)
        => EditorialAdPlacementQueries.Read(_context, _storage, cancellationToken);
}

public sealed record GetPublicAdPlacementsQuery(EditorialAdSurface Surface) : IRequest<IReadOnlyList<EditorialAdPlacementDto>>;

public sealed class GetPublicAdPlacementsQueryHandler : IRequestHandler<GetPublicAdPlacementsQuery, IReadOnlyList<EditorialAdPlacementDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IEditorialMediaStorage _storage;
    public GetPublicAdPlacementsQueryHandler(IApplicationDbContext context, IEditorialMediaStorage storage)
    { _context = context; _storage = storage; }
    public Task<IReadOnlyList<EditorialAdPlacementDto>> Handle(GetPublicAdPlacementsQuery request, CancellationToken cancellationToken)
        => EditorialAdPlacementQueries.Read(_context, _storage, cancellationToken, request.Surface);
}

internal static class EditorialAdPlacementQueries
{
    public static async Task<IReadOnlyList<EditorialAdPlacementDto>> Read(IApplicationDbContext context,
        IEditorialMediaStorage storage, CancellationToken cancellationToken, EditorialAdSurface? surface = null)
    {
        var query = from placement in context.EditorialAdPlacements.AsNoTracking()
                    join asset in context.EditorialMediaAssets.AsNoTracking() on placement.MediaAssetId equals asset.Id
                    where asset.Kind == EditorialMediaKind.Image && asset.Status == EditorialMediaStatus.Ready
                    select new { placement, asset };
        if (surface.HasValue) query = query.Where(x => x.placement.Surface == surface.Value);
        var rows = await query.OrderBy(x => x.placement.DisplayOrder).ThenBy(x => x.placement.SlotKey)
            .Select(x => new
            {
                x.placement.SlotKey, x.placement.Surface, MediaAssetId = x.asset.Id, x.asset.ObjectPath,
                x.placement.TargetUrl, x.placement.AltText, x.placement.DisplayOrder
            })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new EditorialAdPlacementDto(row.SlotKey, row.Surface.ToString().ToLowerInvariant(),
            row.MediaAssetId, storage.GetPublicUrl(row.ObjectPath), row.TargetUrl, row.AltText, row.DisplayOrder)).ToArray();
    }
}
