namespace Jusoor.Infrastructure.Ingestion;

public class SsrfViolationException : Exception
{
    public SsrfViolationException(string message, Exception? inner = null) : base(message, inner) { }
}
