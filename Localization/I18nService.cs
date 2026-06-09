using System.Globalization;
using Microsoft.Extensions.Localization;

namespace CargoInbox.Localization;

public class I18nService(IStringLocalizer<I18nService> localizer)
{
    public string T(string key) => localizer[key];
    public string T(string key, params object[] args) => localizer[key, args];

    public string GetCulture() => CultureInfo.CurrentCulture.Name;
    public string GetUiCulture() => CultureInfo.CurrentUICulture.Name;
}
