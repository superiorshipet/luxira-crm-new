namespace Luxira.Api.Utils.Exceptions;

public class BadRequestException : OException
{
    public BadRequestException(string message) : base(message, 400)
    {
    }
}
