using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Timezone.Management.Integration.Tests;

public sealed class DetailedExceptionMiddleware(RequestDelegate next)
{
	public async Task InvokeAsync(HttpContext context)
	{
		try
		{
			await next(context);
		}
		catch (Exception ex)
		{
			context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
			context.Response.ContentType = "application/json";
			await context.Response.WriteAsJsonAsync(new
			{
				message = ex.Message,
				exceptionType = ex.GetType().FullName,
				stackTrace = ex.StackTrace
			});
		}
	}
}

public sealed class DetailedExceptionStartupFilter : IStartupFilter
{
	public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
		app =>
		{
			app.UseMiddleware<DetailedExceptionMiddleware>();
			next(app);
		};
}
