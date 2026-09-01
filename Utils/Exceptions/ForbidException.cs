namespace Luxira.Api.Utils.Exceptions;

public class ForbidException : OException
{
    public ForbidException(string message = "Forbidden") : base(message, 403)
    {
    }
}
