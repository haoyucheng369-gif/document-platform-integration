using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

var authority = builder.Configuration["Authentication:Authority"]
    ?? throw new InvalidOperationException("Authentication:Authority is required");
var externalAuthority = builder.Configuration["Authentication:ExternalAuthority"];
var audience = builder.Configuration["Authentication:Audience"]
    ?? throw new InvalidOperationException("Authentication:Audience is required");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = false;

        // REST API 只信任 Keycloak 签发给 platform-rest-api 的 JWT。
        // Docker 下 issuer 可能表现为 keycloak:8080 或 localhost:8080，所以同时允许 internal/external issuer。
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidAudience = audience,
            ValidIssuers = BuildValidIssuers(authority, externalAuthority),
            NameClaimType = "preferred_username",
            RoleClaimType = "roles"
        };
    });

builder.Services.AddAuthorization(options =>
{
    // 用户链路：普通文档允许 platform-user / platform-admin，保密文档只允许 platform-admin。
    options.AddPolicy("PlatformUser", policy =>
    {
        policy.RequireRole("platform-user", "platform-admin");
    });

    options.AddPolicy("PlatformAdmin", policy =>
    {
        policy.RequireRole("platform-admin");
    });

    // 应用链路：第三方后台集成使用 client credentials，不代表某个具体用户。
    options.AddPolicy("PlatformIntegration", policy =>
    {
        policy.RequireRole("platform-integration");
    });
});
builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter a Keycloak access token."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

static IEnumerable<string> BuildValidIssuers(string? internalAuthority, string? externalAuthority)
{
    return new[] { internalAuthority, externalAuthority }
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!.TrimEnd('/'))
        .Distinct(StringComparer.OrdinalIgnoreCase);
}
