namespace Tom.WebApi.Api.Shared;

public static class Problems
{
    public static ProblemHttpResult BadRequest(string title, string detail)
        => TypedResults.Problem(title: title, detail: detail, statusCode: StatusCodes.Status400BadRequest);

    public static ProblemHttpResult NotFound(string title, string detail)
        => TypedResults.Problem(title: title, detail: detail, statusCode: StatusCodes.Status404NotFound);

    public static ProblemHttpResult Conflict(string title, string detail)
        => TypedResults.Problem(title: title, detail: detail, statusCode: StatusCodes.Status409Conflict);
}
