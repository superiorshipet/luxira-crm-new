namespace Luxira.Api.Utils.Exceptions;

public class InternalServerException : OException
{
    public InternalServerException(string message = "An internal server error occurred.") : base(message, 500)
    {
    }
}
