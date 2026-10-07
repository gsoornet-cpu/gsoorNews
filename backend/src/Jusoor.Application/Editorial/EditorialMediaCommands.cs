using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Editorial.Contracts;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jusoor.Application.Editorial;

public sealed record CreateEditorialMediaUploadCommand(
    string FileName, string ContentType, long SizeBytes, string AltText, string Credit,
    string? Caption, int? FocalPointX, int? FocalPointY, string ActorUserId)
    : IRequest<EditorialMediaUploadResult>;

public sealed class CreateEditorialMediaUploadCommandValidator : AbstractValidator<CreateEditorialMediaUploadCommand>
{
    public CreateEditorialMediaUploadCommandValidator()
    {
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(EditorialMediaAsset.FileNameMaxLength)
            .Must(name => Path.GetFileName(name.Replace('\\', '/')) == name.Replace('\\', '/'));
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x).Must(x => EditorialMediaPolicy.TryGetKind(x.FileName, x.ContentType, x.SizeBytes, out _))
            .WithMessage("The file type or size is not supported.");
        RuleFor(x => x.AltText).NotEmpty().MaximumLength(EditorialMediaAsset.AltTextMaxLength);
        RuleFor(x => x.Credit).NotEmpty().MaximumLength(EditorialMediaAsset.CreditMaxLength);
        RuleFor(x => x.Caption).MaximumLength(EditorialMediaAsset.CaptionMaxLength);
        RuleFor(x => x.FocalPointX).InclusiveBetween(0, 100).When(x => x.FocalPointX.HasValue);
        RuleFor(x => x.FocalPointY).InclusiveBetween(0, 100).When(x => x.FocalPointY.HasValue);
        RuleFor(x => x).Must(x => x.FocalPointX.HasValue == x.FocalPointY.HasValue)
            .WithMessage("Both focal point coordinates must be supplied together.");
        RuleFor(x => x).Must(x => x.FocalPointX is null || x.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Focal points apply only to images.");
    }
}

public sealed record FinalizeEditorialMediaUploadCommand(Guid AssetId, string ActorUserId)
    : IRequest<EditorialMediaResult>;

public sealed record ListEditorialMediaQuery(int Page, int PageSize) : IRequest<PagedEditorialMedia>;

public sealed record EditorialMediaUploadResult(
    EditorialMediaOutcome Outcome, Guid? AssetId, string? UploadUrl, string? UploadToken,
    string? ResumableEndpoint, string? Bucket, string? ObjectPath, string? ContentType, DateTimeOffset? ExpiresAtUtc)
{
    public bool Succeeded => Outcome == EditorialMediaOutcome.Success;
}

public sealed record EditorialMediaResult(EditorialMediaOutcome Outcome, EditorialMediaDto? Asset);

public sealed record PagedEditorialMedia(IReadOnlyList<EditorialMediaDto> Items, int Page, int PageSize, int TotalCount);

public enum EditorialMediaOutcome { Success, NotFound, InvalidUpload, UploadMissing, Expired, StorageUnavailable }

public sealed class CreateEditorialMediaUploadCommandHandler : IRequestHandler<CreateEditorialMediaUploadCommand, EditorialMediaUploadResult>
{
    private static readonly TimeSpan UploadLifetime = TimeSpan.FromHours(2);
    private readonly IApplicationDbContext _context;
    private readonly IEditorialMediaStorage _storage;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<CreateEditorialMediaUploadCommandHandler> _logger;

    public CreateEditorialMediaUploadCommandHandler(
        IApplicationDbContext context,
        IEditorialMediaStorage storage,
        IDateTimeProvider clock,
        ILogger<CreateEditorialMediaUploadCommandHandler> logger)
    {
        _context = context;
        _storage = storage;
        _clock = clock;
        _logger = logger;
    }

    public async Task<EditorialMediaUploadResult> Handle(CreateEditorialMediaUploadCommand request, CancellationToken cancellationToken)
    {
        if (!EditorialMediaPolicy.TryGetKind(request.FileName, request.ContentType, request.SizeBytes, out var kind))
        {
            return new EditorialMediaUploadResult(EditorialMediaOutcome.InvalidUpload, null, null, null, null, null, null, null, null);
        }

        var fileName = Path.GetFileName(request.FileName.Replace('\\', '/'));
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var now = _clock.UtcNow;
        var asset = EditorialMediaAsset.CreatePending(
            $"editorial/{now:yyyy/MM}/{Guid.NewGuid():N}{extension}", fileName, kind,
            request.ContentType.Trim().ToLowerInvariant(), request.SizeBytes, request.AltText, request.Credit,
            request.Caption, request.FocalPointX, request.FocalPointY, request.ActorUserId, now.Add(UploadLifetime));

        _context.EditorialMediaAssets.Add(asset);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var signed = await _storage.CreateSignedUploadAsync(asset.ObjectPath, cancellationToken);
            return new EditorialMediaUploadResult(EditorialMediaOutcome.Success, asset.Id, signed.Url, signed.Token,
                signed.ResumableEndpoint, signed.Bucket, signed.ObjectPath, asset.ContentType, signed.ExpiresAtUtc);
        }
        catch (EditorialMediaStorageException ex)
        {
            asset.MarkUploadFailed();
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("Supabase did not issue an editorial media upload URL for asset {AssetId}: {FailureCode}", asset.Id, ex.FailureCode);
            return new EditorialMediaUploadResult(EditorialMediaOutcome.StorageUnavailable, null, null, null, null, null, null, null, null);
        }
    }
}

public sealed class FinalizeEditorialMediaUploadCommandHandler : IRequestHandler<FinalizeEditorialMediaUploadCommand, EditorialMediaResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IEditorialMediaStorage _storage;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<FinalizeEditorialMediaUploadCommandHandler> _logger;

    public FinalizeEditorialMediaUploadCommandHandler(
        IApplicationDbContext context,
        IEditorialMediaStorage storage,
        IDateTimeProvider clock,
        ILogger<FinalizeEditorialMediaUploadCommandHandler> logger)
    {
        _context = context;
        _storage = storage;
        _clock = clock;
        _logger = logger;
    }

    public async Task<EditorialMediaResult> Handle(FinalizeEditorialMediaUploadCommand request, CancellationToken cancellationToken)
    {
        var asset = await _context.EditorialMediaAssets.FirstOrDefaultAsync(x => x.Id == request.AssetId, cancellationToken);
        if (asset is null || asset.UploadedByUserId != request.ActorUserId)
        {
            return new EditorialMediaResult(EditorialMediaOutcome.NotFound, null);
        }

        if (asset.Status == EditorialMediaStatus.Ready)
        {
            return new EditorialMediaResult(EditorialMediaOutcome.Success, ToDto(asset, _storage));
        }

        if (asset.Status != EditorialMediaStatus.PendingUpload)
        {
            return new EditorialMediaResult(EditorialMediaOutcome.InvalidUpload, null);
        }

        if (_clock.UtcNow > asset.UploadExpiresAtUtc)
        {
            asset.MarkUploadFailed();
            await _context.SaveChangesAsync(cancellationToken);
            return new EditorialMediaResult(EditorialMediaOutcome.Expired, null);
        }

        StoredMediaObject? stored;
        try
        {
            stored = await _storage.InspectPublicObjectAsync(asset.ObjectPath, cancellationToken);
        }
        catch (EditorialMediaStorageException ex)
        {
            _logger.LogWarning("Supabase could not verify editorial media asset {AssetId}: {FailureCode}", asset.Id, ex.FailureCode);
            return new EditorialMediaResult(EditorialMediaOutcome.StorageUnavailable, null);
        }

        if (stored is null)
        {
            return new EditorialMediaResult(EditorialMediaOutcome.UploadMissing, null);
        }

        if (stored.SizeBytes != asset.SizeBytes
            || !string.Equals(stored.ContentType, asset.ContentType, StringComparison.OrdinalIgnoreCase)
            || !EditorialMediaPolicy.HasExpectedSignature(asset.Kind, asset.ContentType, stored.PrefixBytes))
        {
            asset.MarkUploadFailed();
            await _context.SaveChangesAsync(cancellationToken);
            try { await _storage.DeleteObjectAsync(asset.ObjectPath, cancellationToken); }
            catch (EditorialMediaStorageException ex)
            {
                _logger.LogError("Rejected editorial media object cleanup failed for asset {AssetId}: {FailureCode}", asset.Id, ex.FailureCode);
            }

            return new EditorialMediaResult(EditorialMediaOutcome.InvalidUpload, null);
        }

        asset.MarkReady(stored.SizeBytes, stored.ContentType, _clock.UtcNow);
        await _context.SaveChangesAsync(cancellationToken);
        return new EditorialMediaResult(EditorialMediaOutcome.Success, ToDto(asset, _storage));
    }

    internal static EditorialMediaDto ToDto(EditorialMediaAsset asset, IEditorialMediaStorage storage) => new(
        asset.Id, asset.OriginalFileName, asset.Kind.ToString(), asset.ContentType, asset.SizeBytes,
        asset.AltText, asset.Credit, asset.Caption, asset.FocalPointX, asset.FocalPointY,
        storage.GetPublicUrl(asset.ObjectPath), asset.ReadyAtUtc!.Value);
}

public sealed class ListEditorialMediaQueryValidator : AbstractValidator<ListEditorialMediaQuery>
{
    public ListEditorialMediaQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class ListEditorialMediaQueryHandler : IRequestHandler<ListEditorialMediaQuery, PagedEditorialMedia>
{
    private readonly IApplicationDbContext _context;
    private readonly IEditorialMediaStorage _storage;

    public ListEditorialMediaQueryHandler(IApplicationDbContext context, IEditorialMediaStorage storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task<PagedEditorialMedia> Handle(ListEditorialMediaQuery request, CancellationToken cancellationToken)
    {
        var query = _context.EditorialMediaAssets.AsNoTracking().Where(x => x.Status == EditorialMediaStatus.Ready);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.ReadyAtUtc).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        return new PagedEditorialMedia(items.Select(x => FinalizeEditorialMediaUploadCommandHandler.ToDto(x, _storage)).ToArray(),
            request.Page, request.PageSize, count);
    }
}
