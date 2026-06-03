using Platform.DotNetApi;
using Platform.DotNetApi.Extensions;
using Platform.DotNetApi.Models;
using ThirdParty.Consumer.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthorization();
builder.Services.AddHttpClient();
builder.Services.AddDocuwareClient<KeycloakClientCredentialsTokenProvider>(builder.Configuration);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapGet("/api/documents", async (IDocuwareClient client) => await client.GetDocumentsAsync());

app.MapPost("/api/documents", async (IDocuwareClient client) =>
{
    var document = new Document
    {
        Title = "Third-party created document",
        Content = "This document was created by the consumer API."
    };
    return await client.CreateDocumentAsync(document);
});

app.MapGet("/api/documents-from-factory", async (IDocuwareClient client) => await client.GetDocumentsAsync())
    .WithName("GetDocumentsFromFactory");

app.Run();
