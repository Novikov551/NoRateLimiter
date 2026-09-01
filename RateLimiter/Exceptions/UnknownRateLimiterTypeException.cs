using RateLimiter.ErrorHandling;

namespace RateLimiter.Exceptions
{
    public class UnknownRateLimiterTypeException : CustomException
    {
        public override string ErrorTitle => ErrorTitles.UnknownRateLimiterType;
        public override string ErrorCode => ErrorCodes.UnknownRateLimiterTypeException;

        public string RateLimiterType { get; set; }

        private const string ErrorMessage = "An error occurred while selecting the RateLimiter. UnknownType: `{0}`. {1}";

        public UnknownRateLimiterTypeException(string rRateLimiterType,
            string? errorMessage = "")
            : base(string.Format(ErrorMessage, rRateLimiterType, errorMessage))
        {
            RateLimiterType = rRateLimiterType;
        }
    }
}
