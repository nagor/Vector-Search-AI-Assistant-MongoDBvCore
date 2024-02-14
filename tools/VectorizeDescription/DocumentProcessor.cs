using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;

namespace VectorizeDescription;

public class DocumentProcessor
{
    private readonly IMongoCollection<BsonDocument> _collection;
    private readonly MongoClient _client;
    private readonly IMongoDatabase _database;
    private readonly DescriptionToVectorProcessor _descriptionToVectorProcessor;

    public DocumentProcessor(string mongoDbConnection, string databaseName, string collectionName,
        DescriptionToVectorProcessor descriptionToVectorProcessor)
    {
        _descriptionToVectorProcessor = descriptionToVectorProcessor;
        _client = new MongoClient(mongoDbConnection);
        _database = _client.GetDatabase(databaseName);
        _collection = _database.GetCollection<BsonDocument>(collectionName);
    }

    public async Task ProcessDocumentsInBatchesAsync()
    {
        var batchSize = 2000;
        var batchCount = 0;

        var filter = Builders<BsonDocument>.Filter.Eq("vector", BsonNull.Value) |
                     Builders<BsonDocument>.Filter.Eq("vector", "");
        var projection = Builders<BsonDocument>.Projection
            .Include("_id")
            .Include("data.id")
            .Include("data.gender")
            .Include("data.baseColour")
            .Include("data.productDisplayName")
            .Include("data.description");

        int promptTokensTotal = 0;

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
            BatchSize = batchSize
        });

        await cursor.MoveNextAsync();
        var batch = cursor.Current.ToList();
        if (batch.Any())
        {
            int batchPromptTokens = await ProcessBatchAsync(batch);
            promptTokensTotal += batchPromptTokens;
            batchCount++;
        }


        Console.WriteLine($"Processed {batch.Count} documents.");
        Console.WriteLine($"Total prompt tokens {promptTokensTotal}.");
    }

    private async Task<int> ProcessBatchAsync(ICollection<BsonDocument> batch)
    {
        Console.WriteLine($"Processing {batch.Count.ToString()} documents");


        List<Document> documents = batch.Select(doc =>
        {
            Document document = new Document { DocumentId = doc["_id"].AsObjectId };

            StringBuilder sb = new StringBuilder();
            sb.Append(doc["data"]["gender"].AsString);
            sb.Append(" ");
            sb.Append(doc["data"]["baseColour"].AsString);
            sb.Append(" ");
            sb.Append(doc["data"]["productDisplayName"].AsString);
            sb.Append(" ");
            sb.AppendLine(doc["data"]["description"].AsString);

            document.ExtendedDescription = sb.ToString();

            return document;
        }).ToList();

        int promptTokensTotal = 0;

        await foreach (var imageProcessingResult in _descriptionToVectorProcessor.Process(documents))
        {
            promptTokensTotal = imageProcessingResult.PromptTokensRunningTotal;

            await _collection.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", imageProcessingResult.Document.DocumentId),
                Builders<BsonDocument>.Update.Set("vector", imageProcessingResult.Vectors));
        }

        return promptTokensTotal;
    }
}