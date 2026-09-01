namespace Luxira.Api.Utils.Exceptions;

public class UnauthorizedException : OException
{
    public UnauthorizedException(string message = "Unauthorized") : base(message, 401)
    {
    }
}
