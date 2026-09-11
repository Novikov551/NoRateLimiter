namespace RateLimiter.RateLimiters
{
    /// <summary>
    /// Тип-ключ алгоритма rate limiting. Заменяет enum — позволяет
    /// добавлять новые алгоритмы без модификации библиотеки.
    /// Встроенные: <see cref="TokenBucket"/>, <see cref="SlidingWindow"/>.
    /// </summary>
    public class AlgorithmType : IEquatable<AlgorithmType>
    {
        /// <summary>
        /// Строковый идентификатор алгоритма.
        /// </summary>
        public string Name { get; }

        public AlgorithmType(string name)
        {
            Name = name
                ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>Алгоритм Token Bucket — ведро с токенами, пополняемое с заданной скоростью.</summary>
        public static readonly AlgorithmType TokenBucket = new(nameof(TokenBucket));

        /// <summary>Алгоритм Sliding Window — скользящее окно с подсчётом запросов.</summary>
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
