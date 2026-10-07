using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Common.Security;
using Jusoor.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Api.Controllers;

[ApiController]
[Route("api/v1/cms/editorial-categories")]
[Authorize(Policy = AuthorizationPolicies.CanEditEditorialSeo)]
public sealed class EditorialTaxonomyController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    public EditorialTaxonomyController(IApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _context.EditorialCategories.AsNoTracking()
        .Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ThenBy(c => c.NameAr)
        .Select(c => new { c.Id, c.NameAr, c.Slug, c.DisplayOrder }).ToListAsync(ct));

    public sealed record CategoryInput(string NameAr, string Slug, int DisplayOrder);

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanManageEditorialTaxonomy)]
    public async Task<IActionResult> Create(CategoryInput input, CancellationToken ct)
    {
        var slug = input.Slug.Trim().ToLowerInvariant();
        if (await _context.EditorialCategories.AnyAsync(c => c.Slug == slug, ct)) return Conflict(new { message = "التصنيف موجود بالفعل." });
        try { var category = new EditorialCategory(input.NameAr, slug, input.DisplayOrder); _context.EditorialCategories.Add(category); await _context.SaveChangesAsync(ct); return CreatedAtAction(nameof(List), new { id = category.Id }, category); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageEditorialTaxonomy)]
    public async Task<IActionResult> Update(Guid id, CategoryInput input, CancellationToken ct)
    {
        var category = await _context.EditorialCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return NotFound();
        var slug = input.Slug.Trim().ToLowerInvariant();
        if (await _context.EditorialCategories.AnyAsync(c => c.Id != id && c.Slug == slug, ct)) return Conflict(new { message = "التصنيف موجود بالفعل." });
        try { category.Rename(input.NameAr, slug, input.DisplayOrder); await _context.SaveChangesAsync(ct); return Ok(category); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanManageEditorialTaxonomy)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var category = await _context.EditorialCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return NotFound();
        category.Deactivate(); await _context.SaveChangesAsync(ct); return NoContent();
    }
}
