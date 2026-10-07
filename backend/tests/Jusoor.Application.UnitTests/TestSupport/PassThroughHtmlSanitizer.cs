using Jusoor.Application.Common.Interfaces;

namespace Jusoor.Application.UnitTests.TestSupport;

/// <summary>
/// Stand-in for the real allow-list sanitizer (which lives in Infrastructure and has its
/// own tests): returns its input unchanged, so handler tests exercise the handler's
/// behaviour rather than the sanitizer's policy.
/// </summary>
public sealed class PassThroughHtmlSanitizer : IEditorialHtmlSanitizer
{
    public static readonly PassThroughHtmlSanitizer Instance = new();

    public string Sanitize(string html) => html;
}
