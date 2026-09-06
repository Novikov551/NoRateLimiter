--ARGV[1] = maxRequests
--ARGV[2] = windowSeconds
--ARGV[3] = requested
--KEYS[1] = autentificationKey

local key = 'slidingWindow:' .. KEYS[1]
local maxRequests = tonumber(ARGV[1])
local windowSeconds = tonumber(ARGV[2])
local requested = tonumber(ARGV[3])
local now = redis.call('TIME')

local score = tonumber(now[1]) + tonumber(now[2]) / 1000000

redis.call('ZREMRANGEBYSCORE', key, 0, score - windowSeconds)

local count = redis.call('ZCARD', key)

if count + requested <= maxRequests then
    for i = 1, requested do
        redis.call('ZADD', key, score + i * 0.000001,score + i * 0.000001)
    end
    redis.call('EXPIRE', key, windowSeconds)
    return 1
else
    redis.call('EXPIRE', key, windowSeconds)
    return 0
end