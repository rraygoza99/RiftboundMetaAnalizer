public class Result<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }
    public IEnumerable<string> Errors { get; }

    private Result(bool success, T value, IEnumerable<string> errors)
    {
        IsSuccess = success;
        Value = value;
        Errors = errors;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(IEnumerable<string> errors) => new(false, default, errors);

}