--ARGV[1] = maxRequests
--ARGV[2] = windowSeconds
--KEYS[1] = autentificationKey

local key = 'slidingWindow:' .. KEYS[1]
local maxRequests = tonumber(ARGV[1])
local windowSeconds = tonumber(ARGV[2])
local now = redis.call('TIME')
local score = tonumber(now[1]) + tonumber(now[2]) / 1000000

redis.call('ZREMRANGEBYSCORE', key, 0, score - windowSeconds)

local count = redis.call('ZCARD', key)
redis.call('EXPIRE', key, windowSeconds)

return math.max(0, maxRequests - count)