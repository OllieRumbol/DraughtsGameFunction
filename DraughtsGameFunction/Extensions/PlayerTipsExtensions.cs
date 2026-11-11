using DraughtsGameFunctionService.Instance;
using DraughtsGameFunctionService.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace DraughtsGameFunction.Extensions;

/// <summary>
/// Registers the service behind GetPotentialMoves. It keeps no state, so one instance serves every request.
/// </summary>
public static class PlayerTipsExtensions
{
    public static IServiceCollection AddPlayerTips(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IPlayerTipsService, PlayerTipsService>();

        return services;
    }
}
