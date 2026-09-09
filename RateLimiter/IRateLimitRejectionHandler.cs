using Microsoft.AspNetCore.Http;

namespace RateLimiter
{
    /// <summary>
    /// Обработчик ответа 429 Too Many Requests. Выделен в отдельный интерфейс,
    /// чтобы middleware зависел от узкой абстракции, а не от всего Options-объекта.
    /// Дефолтная реализация: <see cref="DefaultRateLimitRejectionHandler"/>.
    /// Кастомная: реализуйте интерфейс и зарегистрируйте через
    /// <c>services.UseRejectionHandler&lt;T&gt;()</c>.
    /// </summary>
    public interface IRateLimitRejectionHandler
    {
        /// <summary>
        /// Обработать превышение лимита. Устанавливает статус-код,
        /// тело ответа и заголовки 429.
        /// </summary>
        Task HandleAsync(HttpContext httpContext);
    }
}
