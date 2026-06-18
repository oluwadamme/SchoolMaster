using Microsoft.AspNetCore.Mvc.Filters;
using SchoolMaster.Application.Services.Interfaces;

namespace SchoolMaster.Api.Filters;

// Commits the Unit of Work after a controller action has run, but BEFORE its result is
// serialized to the response body. This is the key difference from a terminal middleware:
// because the response has not started yet, a failed SaveChangesAsync (for example a unique
// index violation on concurrent writes) propagates as an exception that ExceptionMiddleware
// can still translate into the correct status code. Committing in middleware after the
// response has already started would leave the client with a false 200.
public class UnitOfWorkFilter(IUnitOfWork unitOfWork) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        // Only commit when the action returned without throwing. In this codebase every error
        // path throws a typed exception (handled by ExceptionMiddleware), so a clean return is
        // always a success. SaveChangesAsync is a no-op when the change tracker is empty, so
        // read-only actions cost nothing.
        if (executed.Exception is null)
            await unitOfWork.SaveChangesAsync();
    }
}
