using Jusoor.Application.Common.Security;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Editorial;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jusoor.Api.Controllers;

/// <summary>Editorial media endpoints. The API signs one-object uploads; file bytes bypass the API.</summary>
[ApiController]
[Route("api/v1/cms/media")]
[Authorize(Policy = AuthorizationPolicies.CanAccessCms)]
public sealed class EditorialMediaController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICurrentUserService _currentUser;

    public EditorialMediaController(ISender mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    public sealed record CreateUploadRequest(
        string FileName,
        string ContentType,
        long SizeBytes,
        string AltText,
        string Credit,
        string? Caption,
        int? FocalPointX,
        int? FocalPointY);

    public sealed record CreateUploadResponse(
        Guid AssetId, string SignedUploadUrl, string UploadToken, string ResumableEndpoint,
        string Bucket, string ObjectPath, string ContentType, DateTimeOffset ExpiresAtUtc);

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 24, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListEditorialMediaQuery(page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpPost("uploads")]
    [Authorize(Policy = AuthorizationPolicies.CanCreateEditorialArticle)]
    [ProducesResponseType(typeof(CreateUploadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateUpload(CreateUploadRequest request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();

        var result = await _mediator.Send(new CreateEditorialMediaUploadCommand(
            request.FileName, request.ContentType, request.SizeBytes, request.AltText, request.Credit,
            request.Caption, request.FocalPointX, request.FocalPointY, userId), cancellationToken);

        return result.Outcome switch
        {
            EditorialMediaOutcome.Success => StatusCode(StatusCodes.Status201Created,
                new CreateUploadResponse(result.AssetId!.Value, result.UploadUrl!, result.UploadToken!,
                    result.ResumableEndpoint!, result.Bucket!, result.ObjectPath!, result.ContentType!, result.ExpiresAtUtc!.Value)),
            EditorialMediaOutcome.InvalidUpload => BadRequest(new { message = "نوع الملف أو حجمه أو بياناته غير مقبولة." }),
            EditorialMediaOutcome.StorageUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "خدمة تخزين الوسائط غير متاحة أو غير مضبوطة." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = AuthorizationPolicies.CanCreateEditorialArticle)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();

        var result = await _mediator.Send(new FinalizeEditorialMediaUploadCommand(id, userId), cancellationToken);
        return result.Outcome switch
        {
            EditorialMediaOutcome.Success => Ok(result.Asset),
            EditorialMediaOutcome.InvalidUpload => BadRequest(new { message = "الملف المرفوع لا يطابق النوع أو الحجم المصرّح به." }),
            EditorialMediaOutcome.NotFound => NotFound(new { message = "جلسة الرفع غير موجودة." }),
            EditorialMediaOutcome.UploadMissing => Conflict(new { message = "لم يكتمل رفع الملف بعد." }),
            EditorialMediaOutcome.Expired => StatusCode(StatusCodes.Status410Gone, new { message = "انتهت صلاحية جلسة الرفع. ابدأ رفعًا جديدًا." }),
            EditorialMediaOutcome.StorageUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "تعذّر التحقق من الملف لدى خدمة التخزين." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
