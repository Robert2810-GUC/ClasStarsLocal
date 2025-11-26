namespace My.ClasStars;

public sealed class ExternalProviders
{
    public string Name { get; set; } = string.Empty;
    public DateTime? LastLoginDate { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime? ExpiryDate { get; set; }
}
