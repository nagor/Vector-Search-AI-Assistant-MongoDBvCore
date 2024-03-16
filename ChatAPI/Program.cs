using ChatAPI.Middlewares;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using SharedLib.Exceptions;
using SharedLib.Options;
using SharedLib.Services;

var builder = WebApplication.CreateBuilder(args);

builder.RegisterConfiguration();

builder.Services.AddControllers();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.RegisterServices();
builder.Services.AddApplicationInsightsTelemetry();

//builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
else
{
    app.SetupErrorsTracking();
}

app.UseApiKeyMiddleware();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

static class ProgramExtensions
{
    public static void RegisterConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<OpenAi>()
            .Bind(builder.Configuration.GetSection(nameof(OpenAi)));

        builder.Services.AddOptions<MongoDb>()
            .Bind(builder.Configuration.GetSection(nameof(MongoDb)));

        builder.Services.AddOptions<ChatApi>()
            .Bind(builder.Configuration.GetSection(nameof(ChatApi)));
    }

    public static void RegisterServices(this IServiceCollection services)
    {

        services.AddScoped<OpenAiService, OpenAiService>(provider =>
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

        services.AddScoped<MongoDbService, MongoDbService>(provider =>
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
        services.AddScoped<ChatService, ChatService>(provider =>
        {
            return new ChatService(
                mongoDbService: provider.GetRequiredService<MongoDbService>(),
                openAiService: provider.GetRequiredService<OpenAiService>(),
                logger: provider.GetRequiredService<ILogger<ChatService>>()
            );
        });
    }

    public static void SetupErrorsTracking(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                context.Response.StatusCode = 500; // or another Status according to Exception Type
                context.Response.ContentType = "application/json";

                var error = context.Features.Get<IExceptionHandlerFeature>();
                if (error != null)
                {
                    var ex = error.Error;

                    if (ex is ApiException apiException)
                    {
                        context.Response.StatusCode = (int)apiException.StatusCode;
                    }

                    // Log the error to AppInsights
                    var telemetryClient = context.RequestServices.GetRequiredService<TelemetryClient>();
                    telemetryClient.TrackException(ex);

                    await context.Response.WriteAsync(ex.Message).ConfigureAwait(false);
                }
            });
        });
    }
}