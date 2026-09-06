--ARGV[1] = capacity
--ARGV[2] = refillRate
--ARGV[3] = ttl
--KEYS[1] = autentificationKey

local key = 'tokenBucket:' .. KEYS[1]
local capacity = tonumber(ARGV[1])
local refillRate = tonumber(ARGV[2])
local now = tonumber(redis.call('TIME')[1])
local tokens = tonumber(redis.call('HGET', key, 'tokens'))
local lastRefill = tonumber(redis.call('HGET', key, 'last_refill'))
local ttlSeconds = tonumber(ARGV[3])

if tokens == nil then
    tokens = capacity
    lastRefill = now
end

local elapsed = now - lastRefill
    
tokens = math.min(capacity, tokens + refillRate * elapsed)

redis.call('HSET', key, 'tokens', tokens)
redis.call('EXPIRE', key, ttlSeconds)
redis.call('HSET', key, 'last_refill', now)
redis.call('EXPIRE', key, ttlSeconds)

return tokens