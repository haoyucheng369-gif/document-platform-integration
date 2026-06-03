using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Platform.DotNetApi.Auth;

namespace Platform.DotNetApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDocuwareClient<TAccessTokenProvider>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TAccessTokenProvider : class, IAccessTokenProvider
    {
        var section = configuration.GetSection("DocuwareClient");

        services.AddOptions<DocuwareClientOptions>()
            .Bind(section)
            .Validate(options => !string.IsNullOrWhiteSpace(options.BaseUrl), "DocuwareClient:BaseUrl is required");

        services.AddSingleton<IAccessTokenProvider, TAccessTokenProvider>();

        services.AddHttpClient<IDocuwareClient, DocuwareClient>((provider, client) =>
        {
            var clientOptions = provider.GetRequiredService<IOptions<DocuwareClientOptions>>().Value;

            client.BaseAddress = new Uri(clientOptions.BaseUrl);
        });

        return services;
    }
}
