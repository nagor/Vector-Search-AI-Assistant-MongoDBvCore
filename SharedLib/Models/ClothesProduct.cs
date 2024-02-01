using System.Text;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace SharedLib.Models;

public class ClothesProduct
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string id { get; set; }

    [BsonRepresentation(BsonType.Int64)]
    public long ProductID { get; set; }

    public string ProductName { get; set; }
    public string ProductBrand { get; set; }
    public string Gender { get; set; }
    public double Price { get; set; }
    public int NumImages { get; set; }
    public string Description { get; set; }
    public string PrimaryColor { get; set; }

    public float[]? vector { get; set; }
}
public static class ClothesProductExtensions
{
    public static string ToFormattedString(this List<ClothesProduct> clothesProducts)
    {
        return ToFormattedString(clothesProducts, formattedProduct: product => $"{product.ProductID}  {product.Price:C}  {product.ProductName}");
    }

    public static string ToFormattedString(this List<ClothesProduct> clothesProducts, Func<ClothesProduct, string> formattedProduct)
    {
        StringBuilder stringBuilder = new StringBuilder();

        foreach (var product in clothesProducts)
        {
            stringBuilder.AppendLine(formattedProduct(product));
        }

        return stringBuilder.ToString();
    }

    public static string ToFormattedString(this ClothesProduct product)
    {
        return $"{product.ProductID} {product.Price:C} {product.ProductName}\n{product.ProductBrand} {product.Gender} {product.PrimaryColor}\n{product.Description}";
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