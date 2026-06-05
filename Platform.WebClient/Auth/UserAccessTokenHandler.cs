using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;

namespace Platform.WebClient.Auth;

public class UserAccessTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserAccessTokenHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // 这里是 WebClient -> RestApi 的出站请求拦截点。
        // token 来自当前用户的 cookie session，不是重新向 IdP 申请。
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No active HTTP context is available.");

        var accessToken = await httpContext.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("The current user session does not contain an access token.");
        }

        // RestApi 会根据这个用户 access_token 里的 roles 判断 platform-user / platform-admin。
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
