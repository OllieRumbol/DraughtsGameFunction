using DraughtsGameFunctionModels.Options;
using DraughtsGameFunctionService.Instance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DraughtsGameFunction.Extensions;

/// <summary>
/// Registers the computer player and its search settings (plan sections 2 and 3.3).
/// The caller registers <see cref="TimeProvider"/>.
/// </summary>
public static class ComputerPlayerExtensions
{
    public static IServiceCollection AddComputerPlayer(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Plan section 2: a bad setting stops the app when it starts, not during a game.
        services.AddOptions<SearchOptions>()
            .Bind(configuration.GetSection(SearchOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Plan section 3.3: requests run at the same time, and a plain Random is not thread-safe.
        services.AddSingleton(Random.Shared);
        services.AddSingleton<ComputerPlayer>();

        return services;
    }
}
