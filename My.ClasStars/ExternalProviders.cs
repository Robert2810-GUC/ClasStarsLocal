using System;

namespace My.ClasStars;

public class ExternalProviders
{
    public string Name { get; set; } = null;
    public DateTime? LastLoginDate { get; set; }=null;
    public string ImageUrl { get; set; } = null;
    public DateTime? ExpiryDate { get; set; } = null;
}