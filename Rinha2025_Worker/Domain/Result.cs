using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Rinha2025_Worker.Domain
{
    public sealed record Error(string Code, string? Message = null);
    public class Result<TValue, TError>
    {
        public readonly TValue? Value;
        public readonly TError? Error;

        private bool _isSuccess;

        private Result(TValue value)
        {
            _isSuccess = true;
            value = value;
           // error = default;
        }

        private Result(TError error)
        {
            _isSuccess = false;
           // value = default;
            error = error;
        }

        //happy path
        public static implicit operator Result<TValue, TError>(TValue value) => new Result<TValue, TError>(value);

        //error path
        public static implicit operator Result<TValue, TError>(TError error) => new Result<TValue, TError>(error);

        public Result<TValue, TError> Match(Func<TValue, Result<TValue, TError>> success, Func<TError, Result<TValue, TError>> failure)
        {
            if (_isSuccess)
            {
                return success(Value!);
            }
            return failure(Error!);
        }
    }
}
