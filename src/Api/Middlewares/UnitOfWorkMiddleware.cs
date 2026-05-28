namespace SchoolMaster.Api.Middlewares;
using SchoolMaster.Application.Services.Interfaces;

public class UnitOfWorkMiddleware
{
    private readonly RequestDelegate _next;

    public UnitOfWorkMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IUnitOfWork unitOfWork)
    {
        await _next(context);

        if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
        {
            await unitOfWork.SaveChangesAsync();
        }
    }
}