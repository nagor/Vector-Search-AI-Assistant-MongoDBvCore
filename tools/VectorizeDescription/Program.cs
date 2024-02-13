using System.Diagnostics;
using VectorizeDescription;

Console.WriteLine("Image to Text...");

Stopwatch stopwatch = new Stopwatch();
stopwatch.Start();

string? mongoDbConnection = Environment.GetEnvironmentVariable("AIPS_MONGO_DB");
ArgumentNullException.ThrowIfNull(mongoDbConnection);

string? gpt4VisionEndpoint = Environment.GetEnvironmentVariable("GPT4_VISION_ENDPOINT");
ArgumentNullException.ThrowIfNull(gpt4VisionEndpoint);

string? gpt4VisionKey = Environment.GetEnvironmentVariable("GPT4_VISION_KEY");
ArgumentNullException.ThrowIfNull(gpt4VisionKey);

DocumentProcessor documentProcessor = new DocumentProcessor(
    mongoDbConnection,
    databaseName: "retaildb",
    collectionName: "test",
    gpt4VisionEndpoint, gpt4VisionKey);
await documentProcessor.ProcessDocumentsInBatchesAsync();

stopwatch.Stop();
double timeTakenMin = stopwatch.Elapsed.TotalMinutes;
Console.WriteLine($"Total time taken: {timeTakenMin} min");