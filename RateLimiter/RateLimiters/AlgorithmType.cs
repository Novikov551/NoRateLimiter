namespace RateLimiter.RateLimiters
{
    public class AlgorithmType : IEquatable<AlgorithmType>
    {
        public string Name { get; set; }

        public AlgorithmType(string name)
        {
            Name = name
                ?? throw new ArgumentNullException(nameof(name));
        }

        public static readonly AlgorithmType TokenBucket = new(nameof(TokenBucket));
        public static readonly AlgorithmType SlidingWindow = new(nameof(SlidingWindow));

        public bool Equals(AlgorithmType other)
        {
            return string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is AlgorithmType other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode(StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return Name;
        }

        public static bool operator ==(AlgorithmType left, AlgorithmType right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AlgorithmType left, AlgorithmType right)
        {
            return !left.Equals(right);
        }
    }
}
