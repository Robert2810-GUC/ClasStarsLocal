namespace My.ClasStars.Configuration;

public sealed class AuthOptions
{
    public string ClasstarsAuthSecret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
}
