using System.Text.Json;

namespace RateLimiter.RateLimiters.SlidingWindow
{
    /// <summary>
    /// Реализация алгоритма Sliding Window. Хранит метки времени запросов
    /// и удаляет устаревшие при каждой проверке. Потокобезопасен (lock).
    /// Поддерживает сериализацию для распределённых хранилищ.
    /// </summary>
    public class SlidingWindow : ISerializableRateLimiter
    {
        private readonly TimeSpan _window;
        private List<DateTime> _requests;
        private readonly int _maxRequests;
        private readonly object _lock = new object();

        public SlidingWindow(TimeSpan window,
            int maxRequests)
        {
            _window = window;
            _requests = new List<DateTime>();
            _maxRequests = maxRequests;
        }

        public bool TryConsume(int tokens = 1)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                ClearWindow(now);

                if (_requests.Count + tokens > _maxRequests)
                {
                    return false;
                }

                for (var i = 0; i < tokens; i++)
                {
                    _requests.Add(now);
                }

                return true;
            }
        }

        private void ClearWindow(DateTime now)
        {
            _requests.RemoveAll(e => e < now - _window);
        }

        public int GetRemaining()
        {
            lock (_lock)
            {
                return Math.Max(0, _maxRequests - _requests.Count);
            }
        }

        public DateTime GetReset()
        {
            lock (_lock)
            {
                if(_requests.Count < _maxRequests)
                {
                    return DateTime.UtcNow;
                }

                return _requests[0] + _window;
            }
        }

        public string Serialize()
        {
            lock (_lock)
            {
                ClearWindow(DateTime.UtcNow);

                return JsonSerializer.Serialize(new { Requests = _requests });
            }
        }

        public void Deserialize(string state)
        {
            lock (_lock)
            {
                using var doc = JsonDocument.Parse(state);
                var requestselement = doc.RootElement.GetProperty("Requests");
                var requests = JsonSerializer.Deserialize<List<DateTime>>(requestselement.GetRawText());
                if (requests != null)
                {
                    _requests = requests;
                }
            }
        }
    }
}
