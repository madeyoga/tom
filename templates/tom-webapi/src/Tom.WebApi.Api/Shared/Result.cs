namespace Tom.WebApi.Api.Shared;

public readonly record struct Result<T>
{
    private Result(T? value, ProblemHttpResult? problem)
    {
        Value = value;
        Problem = problem;
    }

    public T? Value { get; }

    public ProblemHttpResult? Problem { get; }

    public static implicit operator Result<T>(T value) => new(value, null);

    public static implicit operator Result<T>(ProblemHttpResult problem) => new(default, problem);
}

public readonly record struct Result
{
    private Result(ProblemHttpResult? problem) => Problem = problem;

    public ProblemHttpResult? Problem { get; }

    public static Result Success => default;

    public static implicit operator Result(ProblemHttpResult problem) => new(problem);
}
