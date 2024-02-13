using System.Diagnostics;
using System.Text;
using MongoDB.Bson;
using SharedLib.Services;

namespace VectorizeDescription;

public class DescriptionToVectorProcessor
{
    private readonly OpenAiService _oaiService;
    private const int Retries = 5;
    public DescriptionToVectorProcessor(OpenAiService oaiService)
    {
        _oaiService = oaiService;
    }


    public async IAsyncEnumerable<DocumentProcessingResult> Process(List<Document> documents)
    {
        int promptTokensTotal = 0;
        int counter = 1;

        foreach (var document in documents)
        {
            int tries = Retries;
            bool succeed = false;

            DocumentProcessingResult? result = null;

            do
            {
                try
                {
                    tries--;

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"Processing #{counter} of {documents.Count}, Try #{Retries - tries}");
                    sb.Append("Processing Document Id: ");
                    sb.AppendLine(document.DocumentId.ToString());
                    sb.Append("Description to vectorize: ");
                    sb.AppendLine(document.ExtendedDescription);
                    sb.AppendLine("....................");
                    Console.WriteLine(sb.ToString());


                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();

                    (float[] vectors, int promptTokens) =
                        await _oaiService.GetEmbeddingsAsync(string.Empty, document.ExtendedDescription);

                    promptTokensTotal += promptTokens;

                    stopwatch.Stop();
                    double timeTakenMs = stopwatch.Elapsed.TotalMilliseconds;

                    string? message;

                    sb = new StringBuilder();
                    sb.Append("vectors generated: ");
                    sb.AppendLine(vectors.Length > 0 ? "OK" : "NOT OK");
                    sb.Append("time: ");
                    sb.Append(timeTakenMs);
                    sb.Append(" prompt tokens: ");
                    sb.Append(promptTokens);
                    sb.AppendLine();
                    Console.WriteLine(sb.ToString());
                    result = new DocumentProcessingResult
                    {
                        Document = document, Vectors = vectors, PromptTokensRunningTotal = promptTokensTotal
                    };

                    succeed = true;
                    counter++;

                }
                catch (Exception ex)
                {
                    string error = $"Error: {ex}";
                    Console.WriteLine(error);

                    Console.WriteLine("Waiting 30 sec...");
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
            } while (tries > 0 && !succeed);


            if (result != null)
                yield return result;
        }
        Console.WriteLine($"Prompt tokens: {promptTokensTotal}");
    }
}

public record Document
{
    public string ExtendedDescription { get; set; }

    public ObjectId DocumentId { get; set; }
}

public record DocumentProcessingResult
{
    public Document Document { get; set; }
    public float[] Vectors { get; set; }
    public int PromptTokensRunningTotal { get; set; }
}