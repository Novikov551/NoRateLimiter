namespace RateLimiter.RateLimiters
{
    public class CompositeRateLimiterFactory : IRateLimiterFactory
    {
        private readonly Dictionary<AlgorithmType, IAlgorithmRateLimiterFactory> _factories;
        private readonly RateLimiterOptions _options;

        public CompositeRateLimiterFactory(
            IEnumerable<IAlgorithmRateLimiterFactory> factories,
            RateLimiterOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));

            _factories = factories?
                .GroupBy(f => f.Algorithm)
                .ToDictionary(g => g.Key, g => g.Last())
                ?? throw new ArgumentNullException(nameof(factories));

            foreach (var (name, policy) in _options.Policies)
            {
                if (!_factories.ContainsKey(policy.Algorithm))
                {
                    throw new InvalidOperationException($"Policy '{name} requires algorithm '{policy.Algorithm}'," +
                        $"but no {nameof(IAlgorithmRateLimiterFactory)} is registered for it.");
                }
            }
        }

        public IRateLimiter Create(string policyName)
        {
            var policy = GetPolicy(policyName);
            var factory = GetFactory(policy);

            return factory.Create(policy);
        }

        public int GetDefaultRemaining(string policyName)
        {
            var policy = GetPolicy(policyName);
            var factory = GetFactory(policy);

            return factory.GetDefaultRemaining(policy);
        }

        #region Private


        private IAlgorithmRateLimiterFactory GetFactory(RateLimiterPolicy policy)
        {
            if (!_factories.TryGetValue(policy.Algorithm, out var factory))
            {
                throw new InvalidOperationException($"Algorithm `{policy.Algorithm}` have no registered factories for it.");
            }

            return factory;
        }

        private RateLimiterPolicy GetPolicy(string policyName)
        {
            if (!_options.Policies.TryGetValue(policyName, out var policy))
            {
                throw new InvalidOperationException($"Rate limiter policy '{policyName}' not found.");
            }

            return policy;
        }



        #endregion
    }
}
