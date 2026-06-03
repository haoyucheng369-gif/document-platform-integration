namespace Platform.DotNetApi;

public sealed record DocuwareClientOptions
{
    public string BaseUrl { get; init; } = "http://restapi";
}
