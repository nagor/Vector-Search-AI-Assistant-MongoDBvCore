using System.Text;
using SharedLib.Models;

namespace SharedLib.Extensions;

public static class MessageExtensions
{
    public static string ToFormattedString(this Message message)
    {
        string? formattedProducts = message.Products?.ToFormattedString(
            product =>
            {
                string productStr = $"{product.ProductId}  {product.Price:C}  {product.ProductName}";
                string productId = product.ProductId;
                return
                    $"<img src=\"{product.ImageUrl}\" alt=\"Description of image\" class=\"thumbnail5\"> {productStr} <a href=\"#\" onclick=\"ProductsHelper.giveReasoning('{productId}', '', '')\">Why you may like it?</a> <a href=\"{product.ProductUrl}\" target=\"_blank\">Check out the product!</a>\n";
            });

        StringBuilder stringBuilder = new StringBuilder();

        stringBuilder.AppendLine("Based on your story I can recommend the following products:");
        stringBuilder.AppendLine(formattedProducts ?? "No products found.");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine(new string('-', 20));
        stringBuilder.AppendLine();
        stringBuilder.AppendLine(message.Text);
        stringBuilder.AppendLine();
        stringBuilder.AppendLine(new string('-', 20));
        stringBuilder.AppendLine();
        stringBuilder.Append("User attributes: ");
        stringBuilder.AppendLine(message.CustomerAttributes?.ToString() ?? "No user attributes found.");

        string formattedMessage = stringBuilder.ToString();
        return formattedMessage;
    }
}