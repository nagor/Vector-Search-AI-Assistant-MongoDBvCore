using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedLib.Services;
using VectorizeDescription;

Console.WriteLine("Description to Vector...");

Stopwatch stopwatch = new Stopwatch();
stopwatch.Start();

string? mongoDbConnection = Environment.GetEnvironmentVariable("AIPS_MONGO_DB");
ArgumentNullException.ThrowIfNull(mongoDbConnection);

string? gpt35Endpoint = Environment.GetEnvironmentVariable("GPT35_ENDPOINT");
ArgumentNullException.ThrowIfNull(gpt35Endpoint);

string? gpt35Key = Environment.GetEnvironmentVariable("GPT35_KEY");
ArgumentNullException.ThrowIfNull(gpt35Key);

var serviceCollection = new ServiceCollection();
serviceCollection.AddLogging(configure => {
    configure.AddConsole();
});

var serviceProvider = serviceCollection.BuildServiceProvider();
var logger = serviceProvider.GetService<ILogger<Program>>();

OpenAiService openAiService = new OpenAiService(
    gpt35Endpoint,
    gpt35Key,
    embeddingsDeployment: "embeddings",
    completionsDeployment: "completions",
    maxCompletionTokens: "2000",
    maxConversationTokens: "5000",
    maxEmbeddingTokens: "8000",
    logger
    );

DescriptionToVectorProcessor descriptionToVectorProcessor = new DescriptionToVectorProcessor(openAiService);

DocumentProcessor documentProcessor = new DocumentProcessor(
    mongoDbConnection,
    databaseName: "retaildb",
    collectionName: "jc",
    descriptionToVectorProcessor);
await documentProcessor.ProcessDocumentsInBatchesAsync();

stopwatch.Stop();
double timeTakenMin = stopwatch.Elapsed.TotalMinutes;
Console.WriteLine($"Total time taken: {timeTakenMin} min");