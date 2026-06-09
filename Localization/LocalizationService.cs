using Microsoft.Extensions.Localization;

namespace CargoInbox.Localization;

public class LocalizationService(IStringLocalizer<LocalizationService> localizer)
{
    public string Get(string key) => localizer[key];
    public string Get(string key, params object[] args) => localizer[key, args];
}
