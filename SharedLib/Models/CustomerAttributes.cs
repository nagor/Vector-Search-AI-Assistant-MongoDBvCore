using Newtonsoft.Json;

namespace SharedLib.Models;

public class CustomerAttributes
{
    private static readonly List<string> Genders = new()
    {
        "Mens", "Womens", "Boys", "Girls", "Unisex", "Undefined"
    };

    [JsonProperty("gender")]
    public string? Gender { get; set; }
    [JsonProperty("minPrice")]
    public double? MinPrice { get; set; }
    [JsonProperty("maxPrice")]
    public double? MaxPrice { get; set; }

    public override string ToString()
    {
        return $"Gender: {Gender ?? ""} MinPrice: {(MinPrice == null ? "--" : MinPrice):C} MaxPrice: {(MaxPrice == null ? "--" : MaxPrice):C}";
    }

    public void Sanitize()
    {
        if (!string.IsNullOrWhiteSpace(Gender) && !Genders.Contains(Gender))
        {
            Gender = "Undefined";
        }

        if (MinPrice is < 0)
        {
            MinPrice = null;
        }

        if (MaxPrice is < 0)
        {
            MaxPrice = null;
        }
    }
}