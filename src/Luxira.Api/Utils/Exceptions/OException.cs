namespace Luxira.Api.Utils.Exceptions;

public class OException : Exception
{
    public int StatusCode { get; }

    public OException(string message, int statusCode = 500) : base(message)
    {
        StatusCode = statusCode;
    }
}
