namespace SharedLib.Options;

public record ChatApi
{
    public required string? ApiKey { get; init; }
}