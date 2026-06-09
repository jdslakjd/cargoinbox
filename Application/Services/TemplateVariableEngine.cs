using System.Text.RegularExpressions;
using CargoInbox.Core.Entities;

namespace CargoInbox.Application.Services;

public class TemplateVariableEngine
{
    private static readonly Regex VariableRegex = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

    public string Render(string template, Contact contact, Conversation? conversation = null)
    {
        if (string.IsNullOrWhiteSpace(template)) return template;

        var result = template;

        result = VariableRegex.Replace(result, match =>
        {
            var key = match.Groups[1].Value;
            return key switch
            {
                "ContactName" => contact.Name ?? "",
                "ContactEmail" => contact.Email ?? "",
                "ContactPhone" => contact.Phone ?? "",
                "ContactCompany" => contact.Company ?? "",
                "ConversationTitle" => conversation?.Title ?? "",
                "AgentName" => "CargoInbox Team",
                _ => $"{{{{{key}}}}}"
            };
        });

        return result;
    }
}
