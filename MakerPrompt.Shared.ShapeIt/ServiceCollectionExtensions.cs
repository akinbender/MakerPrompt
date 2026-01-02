using MakerPrompt.Shared.ShapeIt.Documents;
using MakerPrompt.Shared.ShapeIt.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace MakerPrompt.Shared.ShapeIt;

/// <summary>
/// Dependency injection extensions for ShapeIt CAD integration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers ShapeIt services for MakerPrompt.
    /// </summary>
    public static IServiceCollection AddShapeItForMakerPrompt(this IServiceCollection services)
    {
        services.AddScoped<ICadDocumentHost, CadabilityDocumentHost>();
        services.AddScoped<ISceneRenderer, NullSceneRenderer>();

        return services;
    }
}
