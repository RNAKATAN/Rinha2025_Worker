namespace Rinha2025_Worker.Domain
{
    public sealed record Error(string Code, string? Message = null);

    public class Result<TValue, TError>
    {
        public readonly TValue? Value;
        public readonly TError? Error;

        private readonly bool _isSuccess;

        private Result(TValue value)
        {
            _isSuccess = true;
            Value = value;
        }

        private Result(TError error)
        {
            _isSuccess = false;
            Error = error;
        }

        public static implicit operator Result<TValue, TError>(TValue value) => new(value);
        public static implicit operator Result<TValue, TError>(TError error) => new(error);

        public TResult Match<TResult>(Func<TValue, TResult> success, Func<TError, TResult> failure) =>
            _isSuccess ? success(Value!) : failure(Error!);
    }
}
