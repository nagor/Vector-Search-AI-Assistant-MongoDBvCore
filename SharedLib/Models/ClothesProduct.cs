using System.Text;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace SharedLib.Models;

public class ClothesProduct
{
    //[BsonId]
    [BsonRepresentation(BsonType.Int64)]
    public long ProductID { get; set; }

    public string ProductName { get; set; }
    public string ProductBrand { get; set; }
    public string Gender { get; set; }
    public double Price { get; set; }
    public int NumImages { get; set; }
    public string Description { get; set; }
    public string PrimaryColor { get; set; }
}
public static class ClothesProductExtensions
{
    public static string ToFormattedString(this List<ClothesProduct> clothesProducts)
    {
        StringBuilder stringBuilder = new StringBuilder();

        foreach (var product in clothesProducts)
        {
            stringBuilder.AppendLine($"{product.ProductID}  {product.Price:C}  {product.ProductName}");
        }

        return stringBuilder.ToString();
    }

    public static List<ClothesProduct> GetProducts(string bsonData)
    {
        // Convert BSON string to a list of BsonDocuments
        List<BsonDocument> bsonDocuments = BsonSerializer.Deserialize<List<BsonDocument>>(bsonData);

        // Deserialize BsonDocuments to a list of C# objects (ClothesProduct model)
        List<ClothesProduct> products = bsonDocuments.Select(doc => BsonSerializer.Deserialize<ClothesProduct>(doc)).ToList();

        return products;
    }
}