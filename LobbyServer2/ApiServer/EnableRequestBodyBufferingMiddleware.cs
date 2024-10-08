using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace CentralServer.ApiServer;

public class EnableRequestBodyBufferingMiddleware
{
    private readonly RequestDelegate _next;

    public EnableRequestBodyBufferingMiddleware(RequestDelegate next) =>
        _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        await _next(context);
    }
}