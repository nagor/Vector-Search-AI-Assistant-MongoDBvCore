using System.Diagnostics;
using Newtonsoft.Json;
using System.Text;
using MongoDB.Bson;

namespace ImageToText;

public class ImageToDescriptionProcessor
{
    private readonly string _gpt4VisionEndpoint;
    private readonly string _gpt4VisionKey;
    private const int Retries = 5;
    public ImageToDescriptionProcessor(string gpt4VisionEndpoint, string gpt4VisionKey)
    {
        _gpt4VisionEndpoint = gpt4VisionEndpoint;
        _gpt4VisionKey = gpt4VisionKey;
    }


    public async IAsyncEnumerable<ImageProcessingResult> Process(List<Image> images)
    {
        int promptTokensTotal = 0;
        int completionTokensTotal = 0;
        int counter = 1;

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("api-key", _gpt4VisionKey);

        foreach (var image in images)
        {
            int tries = Retries;
            bool succeed = false;

            ImageProcessingResult? result = null;
            string? responseStr = null;

            do
            {
                try
                {
                    tries--;

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"Processing #{counter} of {images.Count}, Try #{Retries-tries}");
                    sb.Append("Processing Document Id: ");
                    sb.AppendLine(image.DocumentId.ToString());
                    sb.Append("Image Id: ");
                    sb.AppendLine(image.Id.ToString());
                    sb.Append("url: ");
                    sb.AppendLine(image.Url);
                    sb.AppendLine("....................");
                    Console.WriteLine(sb.ToString());


                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();

                    var response = await httpClient.PostAsync(_gpt4VisionEndpoint,
                        new StringContent(JsonConvert.SerializeObject(GetPayload(image.Url)), Encoding.UTF8,
                            "application/json"));

                    stopwatch.Stop();
                    double timeTakenMs = stopwatch.Elapsed.TotalMilliseconds;

                    string? message;
                    responseStr = await response.Content.ReadAsStringAsync();
                    if (response.IsSuccessStatusCode)
                    {
                        var responseData = JsonConvert.DeserializeObject<Root>(responseStr);
                        message = responseData?.Choices?.FirstOrDefault()?.Message.Content;
                        int? promptTokens = responseData?.Usage?.PromptTokens;
                        int? completionTokens = responseData?.Usage?.CompletionTokens;
                        if (promptTokens != null)
                            promptTokensTotal += promptTokens.Value;
                        if (completionTokens != null)
                            completionTokensTotal += completionTokens.Value;
                        sb = new StringBuilder();
                        sb.AppendLine("message: ");
                        sb.AppendLine(message);
                        sb.Append("time: ");
                        sb.Append(timeTakenMs);
                        sb.Append(" prompt tokens: ");
                        sb.Append(promptTokens);
                        sb.Append(" completion tokens: ");
                        sb.Append(completionTokens);
                        sb.AppendLine();
                        Console.WriteLine(sb.ToString());
                        result = new ImageProcessingResult
                        {
                            Image = image, Description = message, PromptTokensRunningTotal = promptTokensTotal,
                            CompletionTokensRunningTotal = completionTokensTotal
                        };

                        succeed = true;
                        counter++;
                    }
                    else
                    {
                        message = $"Error: {response.StatusCode}, {response.ReasonPhrase} Time: {timeTakenMs} ms Response: {responseStr}";
                        Console.WriteLine(message);

                        Console.WriteLine("Waiting 30 sec...");
                        await Task.Delay(TimeSpan.FromSeconds(30));

                    }
                }
                catch (Exception ex)
                {
                    string error = $"Error: {ex}, Response: {responseStr}";
                    Console.WriteLine(error);
                }
            } while (tries > 0 && !succeed);


            if (result != null)
                yield return result;
        }
        Console.WriteLine($"Prompt tokens: {promptTokensTotal} Completion tokens: {completionTokensTotal}");
    }

    private static object GetPayload(string imageUrl)
    {
        var payload = new
        {
            // enhancements = new
            // {
            //     ocr = new { enabled = true },
            //     grounding = new { enabled = true }
            // },
            messages = new object[]
            {
                new {
                    role = "system",
                    content = new object[] {
                        new {
                            type = "text",
                            text = "You will be given pictures of apparel. You have to describe characteristics of apparel so that they can be used for search query."
                        }
                    }
                },
                new {
                    role = "user",
                    content = new object[] {
                        new {
                            type = "text",
                            text = "Describe characteristics of the apparel on the photo in a way that would be helpful for a person shopping in the online store. It must include specific details of the product, include style information and probable reason a person would search for something like that online."
                        }
                    }
                },
                new {
                    role = "assistant",
                    content = new object[] {
                        new {
                            type = "text",
                            text = "Ribbed Sweater:\nColor: Vibrant red.\nStyle: Fitted and ribbed.\nNeckline: Round.\nSleeves: Short.\nSeason: Suitable for spring or fall.\nOccasion: Versatile for both casual outings and semi-formal events.\nReason to Search: Individuals seeking a comfortable yet stylish sweater for transitional weather.\nOlive Green Trousers:\nStyle: High-waisted.\nColor: Earthy olive green.\nMaterial: Not specified, but likely a lightweight fabric.\nFunctionality: Ideal for everyday wear.\nReason to Search: Shoppers looking for trendy pants that can be dressed up or down.\nOlive Green Jacket:\nStyle: Casual and chic.\nDetails: Rolled-up sleeves.\nFeatures: Buttoned cuffs and pockets.\nMatching Set: Coordinates with the trousers.\nPurpose: A stylish layer for unpredictable weather.\nSearch Intent: Fashion enthusiasts seeking a complete outfit with coordinated pieces.\nBlack Belt:\nFunction: Cinches the waist.\nMetallic Buckle: Adds sophistication.\nVersatility: Can be paired with other outfits.\nReason to Search: Those wanting to define their waistline and elevate their look.\nOverall, this ensemble strikes a balance between comfort and style, making it an attractive choice for online shoppers seeking versatile pieces that can be mixed and matched for various occasions."
                        }
                    }
                },
                new {
                    role = "user",
                    content = new object[] {
                        new {
                            type = "image_url",
                            image_url = new {
                                // url = $"data:image/jpeg;base64,{encodedImage}"
                                url = imageUrl
                            }
                        },
                        new {
                            type = "text",
                            text = "List characteristics of apparel in the picture:"
                        }
                    }
                }
            },
            temperature = 0,
            top_p = 0.95,
            max_tokens = 4096,
            stream = false
        };
        return payload;
    }
}

public class Message
{
    public string Role { get; set; }
    public string Content { get; set; }
}

public class Choices
{
    public int Index { get; set; }
    public Message Message { get; set; }
}

public class Usage
{
    [JsonProperty("prompt_tokens")]
    public int PromptTokens { get; set; }
    [JsonProperty("completion_tokens")]
    public int CompletionTokens { get; set; }
    [JsonProperty("total_tokens")]
    public int TotalTokens { get; set; }
}

public class Root
{
    public string Id { get; set; }
    public string Object { get; set; }
    public int Created { get; set; }
    public string Model { get; set; }
    public List<Choices>? Choices { get; set; }
    public Usage? Usage { get; set; }
}

public record Image
{
    public string Url { get; set; }
    public int Id { get; set; }

    public ObjectId DocumentId { get; set; }

}

public record ImageProcessingResult
{
    public Image Image { get; set; }
    public string? Description { get; set; }
    public int PromptTokensRunningTotal { get; set; }
    public int CompletionTokensRunningTotal { get; set; }
}