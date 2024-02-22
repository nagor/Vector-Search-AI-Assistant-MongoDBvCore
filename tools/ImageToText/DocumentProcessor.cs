using MongoDB.Bson;
using MongoDB.Driver;

namespace ImageToText;

public class DocumentProcessor
{
    private readonly IMongoCollection<BsonDocument> _collection;
    private readonly MongoClient _client;
    private readonly IMongoDatabase _database;
    private readonly ImageToDescriptionProcessor _imageToDescriptionProcessor;


    private const int BatchSize = 2000;
    private const string DescriptionGeneratedField = "description_generated";
    private const string DocumentIdField = "_id";
    private const string IdField = "id";
    private const string ImageUrlField = "image_link";

    public DocumentProcessor(string mongoDbConnection, string databaseName, string collectionName,
        string gpt4VisionEndpoint, string gpt4VisionKey)
    {
        _client = new MongoClient(mongoDbConnection);
        _database = _client.GetDatabase(databaseName);
        _collection = _database.GetCollection<BsonDocument>(collectionName);
        _imageToDescriptionProcessor = new ImageToDescriptionProcessor(gpt4VisionEndpoint, gpt4VisionKey);
    }

    public async Task ProcessDocumentsInBatchesAsync()
    {
        var batchCount = 0;

        var filter = Builders<BsonDocument>.Filter.Eq(DescriptionGeneratedField, BsonNull.Value) |
                     Builders<BsonDocument>.Filter.Eq(DescriptionGeneratedField, "");
        var projection = Builders<BsonDocument>.Projection
            .Include(DocumentIdField)
            .Include(IdField)
            .Include(ImageUrlField);

        int promptTokensTotal = 0;
        int completionTokensTotal = 0;

        // TODO: batch doesn't seem to be working, still get cursor not found.
        // using (var cursor = await _collection.FindAsync(filter, new FindOptions<BsonDocument>
        // {
        //     NoCursorTimeout = true,
        //     MaxAwaitTime = TimeSpan.MaxValue,
        //     MaxTime = TimeSpan.MaxValue,
        //     Projection = projection,
        //     BatchSize = batchSize
        // }))
        // {
        //     while (await cursor.MoveNextAsync())
        //     {
        //         var batch = cursor.Current.ToList();
        //         if (batch.Any())
        //         {
        //             var (batchPromptTokens, batchCompletionTokens) = await ProcessBatchAsync(batch);
        //             promptTokensTotal += batchPromptTokens;
        //             completionTokensTotal += batchCompletionTokens;
        //             batchCount++;
        //         }
        //     }
        // }
        using var cursor = await _collection.FindAsync(filter, new FindOptions<BsonDocument>
        {
            Projection = projection,
            BatchSize = BatchSize
        });

        await cursor.MoveNextAsync();
        var batch = cursor.Current.ToList();
        if (batch.Any())
        {
            var (batchPromptTokens, batchCompletionTokens) = await ProcessBatchAsync(batch);
            promptTokensTotal += batchPromptTokens;
            completionTokensTotal += batchCompletionTokens;
            batchCount++;
        }


        Console.WriteLine($"Processed {batch.Count} documents.");
        Console.WriteLine($"Total prompt tokens {promptTokensTotal}, total completion tokens {completionTokensTotal}.");
    }

    private async Task<(int, int)> ProcessBatchAsync(ICollection<BsonDocument> batch)
    {
        Console.WriteLine($"Processing {batch.Count.ToString()} documents");

        List<Image> images = batch.Select(doc => new Image
            {
                DocumentId = doc[DocumentIdField].AsObjectId,
                Id = doc[IdField].AsString,
                Url = doc[ImageUrlField].AsString
            }
        ).ToList();

        int promptTokensTotal = 0;
        int completionTokensTotal = 0;


        await foreach (var imageProcessingResult in _imageToDescriptionProcessor.Process(images))
        {
            promptTokensTotal = imageProcessingResult.PromptTokensRunningTotal;
            completionTokensTotal = imageProcessingResult.CompletionTokensRunningTotal;

            await _collection.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq(DocumentIdField, imageProcessingResult.Image.DocumentId),
                Builders<BsonDocument>.Update.Set(DescriptionGeneratedField, imageProcessingResult.Description));
        }

        return (promptTokensTotal, completionTokensTotal);
    }
}