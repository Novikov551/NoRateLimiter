 --ARGV[1] = windowSeconds
 --KEYS[1] = autentificationKey
 
 local key = 'slidingWindow:' .. KEYS[1]
 local now = redis.call('TIME')
 local windowSeconds = tonumber(ARGV[1])
 local score = tonumber(now[1]) + tonumber(now[2]) / 1000000

 redis.call('ZREMRANGEBYSCORE', key, 0, score - windowSeconds)

 local oldest = redis.call('ZRANGE', key, 0, 0, 'WITHSCORES')

 if #oldest == 0 then
     return score
 end
 
 local resetTime = tonumber(oldest[2]) + windowSeconds
 
 return resetTime