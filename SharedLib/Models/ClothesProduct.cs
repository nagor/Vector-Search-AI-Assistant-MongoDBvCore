using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SharedLib.Models;

[BsonIgnoreExtraElements]
public class ClothesProduct
{
    [BsonElement("id")]
    public string ProductId { get; set; }

    [BsonElement("title")]
    public string ProductName { get; set; }

    // [BsonElement("brandName")]
    // public string ProductBrand { get; set; }

    [BsonElement("gender")]
    public string Gender { get; set; }

    [BsonElement("price")]
    public double Price { get; set; }

    [BsonElement("description")]
    public string Description { get; set; }

    [BsonElement("description_generated")]
    public string DescriptionGenerated { get; set; }

    [BsonElement("color")]
    public string PrimaryColor { get; set; }

    [BsonElement("image_link")]
    public string ImageUrl { get; set; }

    [BsonElement("link")]
    public string ProductUrl { get; set; }

    [BsonElement("categories")]
    public string? Categories { get; set; }

    public string? Category { get; set; }
}