using System.Reflection;
using Microsoft.Extensions.Localization;

namespace My.ClasStars;

public class CommonLocalizationService
{
    private readonly IStringLocalizerFactory _localizerFactory;

    public CommonLocalizationService(IStringLocalizerFactory localizerFactory)
    {
        _localizerFactory = localizerFactory;
    }

    public string GetString(string resourceName, string key)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var localizer = _localizerFactory.Create(resourceName, assembly.GetName().Name);
        return localizer[key];
    }
}