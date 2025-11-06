// EhrBridge.Api/Utils/FileCallbackResult.cs
using Microsoft.AspNetCore.Mvc;

namespace EhrBridge.Api.Utils
{
    /// <summary>
    /// Custom ActionResult that allows writing directly to the HTTP response stream.
    /// This is used to implement non-buffering streaming of file content (like CSVs),
    /// which prevents loading large data sets into application memory.
    /// </summary>
    public class FileCallbackResult : IActionResult
    {
        private readonly string _contentType;
        private readonly Func<Stream, ActionContext, Task> _callback;

        public FileCallbackResult(string contentType, Func<Stream, ActionContext, Task> callback)
        {
            // Explicit check for arguments.
            if (string.IsNullOrEmpty(contentType) == true)
            {
                throw new ArgumentNullException(nameof(contentType));
            }
            if (callback is null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            _contentType = contentType;
            _callback = callback;
        }

        public Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            response.ContentType = _contentType;

            // Headers like Content-Disposition are set in the Controller action.

            return _callback(response.Body, context);
        }
    }
}