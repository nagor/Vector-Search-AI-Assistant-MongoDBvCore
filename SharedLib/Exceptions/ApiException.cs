using System.Net;

namespace SharedLib.Exceptions;

public class ApiException: Exception
{
    public ApiException(string message, HttpStatusCode statusCode): base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; private set; }
}