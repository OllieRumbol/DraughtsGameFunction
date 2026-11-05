using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;

namespace DraughtsGameFunctionTests;

/// <summary>
/// The parts of a <see cref="FunctionContext"/> that the middleware reads:
/// the function name, the cancellation token and the HTTP context.
/// </summary>
internal sealed class FakeFunctionContext : FunctionContext
{
    // The key that ASP.NET Core integration uses to store the HTTP context, and that
    // GetHttpContext() reads. PickTime checked it in Worker.Extensions.Http.AspNetCore 2.1.1.
    private const string HttpContextKey = "HttpRequestContext";

    private readonly CancellationToken _cancellationToken;

    public FakeFunctionContext(HttpContext? httpContext = null, CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;

        if (httpContext is not null)
        {
            Items[HttpContextKey] = httpContext;
        }
    }

    public override string InvocationId => "fake-invocation";

    public override string FunctionId => "fake-function";

    public override FunctionDefinition FunctionDefinition { get; } = new FakeFunctionDefinition();

    public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();

    public override CancellationToken CancellationToken => _cancellationToken;

    public override IServiceProvider InstanceServices { get; set; } = null!;

    public override TraceContext TraceContext => throw new NotSupportedException();

    public override BindingContext BindingContext => throw new NotSupportedException();

    public override RetryContext RetryContext => throw new NotSupportedException();

    public override IInvocationFeatures Features => throw new NotSupportedException();

    private sealed class FakeFunctionDefinition : FunctionDefinition
    {
        public override ImmutableArray<FunctionParameter> Parameters => [];

        public override string PathToAssembly => string.Empty;

        public override string EntryPoint => string.Empty;

        public override string Id => "fake-function";

        public override string Name => "FakeFunction";

        public override IImmutableDictionary<string, BindingMetadata> InputBindings => ImmutableDictionary<string, BindingMetadata>.Empty;

        public override IImmutableDictionary<string, BindingMetadata> OutputBindings => ImmutableDictionary<string, BindingMetadata>.Empty;
    }
}
