using Microsoft.Extensions.Options;
using SharedLib.Options;
using SharedLib.Services;

var builder = WebApplication.CreateBuilder(args);

builder.RegisterConfiguration();
builder.Services.AddRazorPages();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddServerSideBlazor();
builder.Services.RegisterServices();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

await app.RunAsync();

static class ProgramExtensions
{
    public static void RegisterConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<OpenAi>()
            .Bind(builder.Configuration.GetSection(nameof(OpenAi)));

        builder.Services.AddOptions<MongoDb>()
            .Bind(builder.Configuration.GetSection(nameof(MongoDb)));
    }

    public static void RegisterServices(this IServiceCollection services)
    {

        services.AddSingleton<OpenAiService, OpenAiService>(provider =>
        {
            var openAiOptions = provider.GetRequiredService<IOptions<OpenAi>>();

            return new OpenAiService(
                endpoint: openAiOptions.Value?.Endpoint ?? String.Empty,
                key: openAiOptions.Value?.Key ?? String.Empty,
                embeddingsDeployment: openAiOptions.Value?.EmbeddingsDeployment ?? String.Empty,
                completionsDeployment: openAiOptions.Value?.CompletionsDeployment ?? String.Empty,
                maxConversationTokens: openAiOptions.Value?.MaxConversationTokens ?? String.Empty,
                maxCompletionTokens: openAiOptions.Value?.MaxCompletionTokens ?? String.Empty,
                maxEmbeddingTokens: openAiOptions.Value?.MaxEmbeddingTokens ?? String.Empty,
                logger: provider.GetRequiredService<ILogger<OpenAiService>>()
            );
        });

        services.AddSingleton<MongoDbService, MongoDbService>(provider =>
        {
            var mongoDbOptions = provider.GetRequiredService<IOptions<MongoDb>>();

            return new MongoDbService(
                connection: mongoDbOptions.Value?.Connection ?? String.Empty,
                databaseName: mongoDbOptions.Value?.DatabaseName ?? String.Empty,
                collectionNames: mongoDbOptions.Value?.CollectionNames ?? String.Empty,
                maxVectorSearchResults: mongoDbOptions.Value?.MaxVectorSearchResults ?? String.Empty,
                vectorIndexType: mongoDbOptions.Value?.VectorIndexType ?? String.Empty,
                openAiService: provider.GetRequiredService<OpenAiService>(),
                logger: provider.GetRequiredService<ILogger<MongoDbService>>()
            );
        });
        services.AddSingleton<ChatService, ChatService>(provider =>
        {
            return new ChatService(
                mongoDbService: provider.GetRequiredService<MongoDbService>(),
                openAiService: provider.GetRequiredService<OpenAiService>(),
                logger: provider.GetRequiredService<ILogger<ChatService>>()
            );
        });
    }
}