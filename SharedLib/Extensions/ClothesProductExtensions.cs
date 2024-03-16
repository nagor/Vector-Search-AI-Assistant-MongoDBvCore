using System.Text;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using SharedLib.Models;

namespace SharedLib.Extensions;

public static class ClothesProductExtensions
{
    public static string ToFormattedString(this List<ClothesProduct> clothesProducts)
    {
        return ToFormattedString(clothesProducts, formattedProduct: product => $"{product.ProductId}  {product.Price:C}  {product.ProductName}");
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
        return $"{product.ProductId} {product.Price:C} {product.ProductName}\n{product.Gender} {product.PrimaryColor}\n{product.Description}\n{product.DescriptionGenerated}";
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