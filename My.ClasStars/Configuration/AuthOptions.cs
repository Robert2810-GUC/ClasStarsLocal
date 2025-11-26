namespace My.ClasStars.Configuration;

public sealed class AuthOptions
{
    public string ClasstarsAuthSecret { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
}
