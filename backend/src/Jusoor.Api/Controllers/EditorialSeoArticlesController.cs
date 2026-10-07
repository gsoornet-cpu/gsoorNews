using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Api.Controllers;

[ApiController]
[Route("api/v1/cms/seo/articles")]
[Authorize(Policy = AuthorizationPolicies.CanEditEditorialSeo)]
public sealed class EditorialSeoArticlesController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    public EditorialSeoArticlesController(IApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) return BadRequest(new { message = "page must be positive and pageSize must be 1–100." });
        var query = _context.EditorialArticles.AsNoTracking().OrderByDescending(a => a.LastModifiedAtUtc ?? a.CreatedAtUtc);
        var count = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new { a.Id, a.Title, a.Slug, Status = a.Status.ToString(), a.NoIndex, a.PrimaryCategoryId })
            .ToListAsync(ct);
        return Ok(new { items, page, pageSize, totalCount = count });
    }
}
