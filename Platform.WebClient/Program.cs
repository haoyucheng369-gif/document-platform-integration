using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Platform.WebClient.Auth;
using Platform.WebClient.Clients;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// WebClient 是服务器端 MVC 应用：浏览器只保存本应用的 cookie。
// 授权码换 token 的过程由后端完成，用户密码不会进入 WebClient。
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddOpenIdConnect(options =>
    {
        var oidcSection = builder.Configuration.GetSection("Authentication:Oidc");
        var externalAuthority = oidcSection["ExternalAuthority"]?.TrimEnd('/');

        // 授权码模式：confidential client 使用 client secret 把 code 换成 token。
        // SaveTokens=true 会把 access_token 保存到认证票据，后续调用 REST API 时再取出。
        options.Authority = oidcSection["Authority"];
        options.ClientId = oidcSection["ClientId"];
        options.ClientSecret = oidcSection["ClientSecret"];
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.SaveTokens = true;
        options.RequireHttpsMetadata = false;
        options.GetClaimsFromUserInfoEndpoint = false;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuers = BuildValidIssuers(options.Authority, externalAuthority),
            NameClaimType = "preferred_username",
            RoleClaimType = "roles"
        };

        // Docker 场景下，后端通过容器 hostname 读取 Keycloak metadata。
        // 浏览器运行在宿主机，所以重定向地址必须使用 localhost。
        if (!string.IsNullOrWhiteSpace(externalAuthority))
        {
            options.Events = new OpenIdConnectEvents
            {
                OnRedirectToIdentityProvider = context =>
                {
                    context.ProtocolMessage.IssuerAddress = RewriteIssuerAddress(
                        context.ProtocolMessage.IssuerAddress,
                        options.Authority!,
                        externalAuthority);

                    return Task.CompletedTask;
                },
                OnRedirectToIdentityProviderForSignOut = context =>
                {
                    context.ProtocolMessage.IssuerAddress = RewriteIssuerAddress(
                        context.ProtocolMessage.IssuerAddress,
                        options.Authority!,
                        externalAuthority);

                    return Task.CompletedTask;
                }
            };
        }
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<UserAccessTokenHandler>();

// WebClient 调 Platform.RestApi 时不会再申请第二个 token。
// UserAccessTokenHandler 会读取当前用户保存的 access_token，并附加 Authorization header。
builder.Services.AddHttpClient<IPlatformApiClient, PlatformApiClient>(client =>
{
    var baseUrl = builder.Configuration["PlatformApi:BaseUrl"];

    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        throw new InvalidOperationException("PlatformApi:BaseUrl is required");
    }

    client.BaseAddress = new Uri(baseUrl);
})
.AddHttpMessageHandler<UserAccessTokenHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseStaticFiles();
app.UseRouting();
// 顺序很重要：先从 cookie 还原用户身份，再执行授权判断。
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// 这里只改浏览器重定向地址；metadata 和 token 调用仍使用后端可访问的 authority。
static string RewriteIssuerAddress(string issuerAddress, string internalAuthority, string externalAuthority)
{
    return issuerAddress.StartsWith(internalAuthority, StringComparison.OrdinalIgnoreCase)
        ? externalAuthority + issuerAddress[internalAuthority.Length..]
        : issuerAddress;
}

static IEnumerable<string> BuildValidIssuers(string? internalAuthority, string? externalAuthority)
{
    return new[] { internalAuthority, externalAuthority }
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!.TrimEnd('/'))
        .Distinct(StringComparer.OrdinalIgnoreCase);
}
