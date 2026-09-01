namespace RateLimiter.ErrorHandling
{
    //TODO вынести потом в отдельную либу
    public abstract class CustomException : Exception
    {
        public abstract string ErrorTitle { get; }
        public abstract string ErrorCode { get; }

        public CustomException(string message) : base(message)
        {

        }

        public CustomException(string message, Exception innerException)
            : base(message, innerException)
        {

        }
    }
}
