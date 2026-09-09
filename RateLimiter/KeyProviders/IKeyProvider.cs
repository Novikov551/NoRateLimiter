using Microsoft.AspNetCore.Http;

namespace RateLimiter.KeyProviders
{
    /// <summary>
    /// Идентификатор клиента. Возвращает строковый ключ, уникальный для клиента.
    /// Ключ + имя политики = уникальный лимитер в хранилище.
    /// Дефолтная реализация: <see cref="IpKeyProvider"/>.
    /// </summary>
    public interface IKeyProvider
    {
        /// <summary>
        /// Получить ключ клиента из HTTP-контекста.
        /// </summary>
        string GetKey(HttpContext httpContext);
    }
}
