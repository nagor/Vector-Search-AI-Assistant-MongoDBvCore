using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace SharedLib.Models;

public record Message
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; }

    public string Type { get; set; }

    public string SessionId { get; set; }

    public DateTime TimeStamp { get; set; }

    public string Sender { get; set; }

    public int Tokens { get; set; }

    public int PromptTokens { get; set; }

    public string Text { get; set; }
    public List<ClothesProduct>? Products { get; set; }
    public CustomerAttributes? CustomerAttributes { get; set; }
    public List<string>? ExtraQuestions { get; set; }

    public Message(
        string sessionId,
        string sender, int? tokens, int? promptTokens,
        string text,
        List<ClothesProduct>? products = null,
        CustomerAttributes? customerAttributes = null,
        List<string>? extraQuestions = null)
    {
        Id = Guid.NewGuid().ToString();
        Type = nameof(Message);
        SessionId = sessionId;
        Sender = sender;
        Tokens = tokens ?? 0;
        PromptTokens = promptTokens ?? 0;
        TimeStamp = DateTime.UtcNow;
        Text = text;
        Products = products;
        CustomerAttributes = customerAttributes;
        ExtraQuestions = extraQuestions;
    }
}