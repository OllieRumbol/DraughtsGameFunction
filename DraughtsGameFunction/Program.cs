using DraughtsGameFunction.Extensions;
using DraughtsGameFunction.Middleware;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// After ConfigureFunctionsWebApplication, so the HTTP context is available to the middleware.
// Request logging is outermost, so it also records the 500 that the exception middleware writes.
builder.UseMiddleware<RequestLoggingMiddleware>();
builder.UseMiddleware<ExceptionHandlingMiddleware>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddWorkerTelemetry(builder.Configuration);

builder.Build().Run();
