namespace CargoInbox.Core.Entities;

public enum ConditionField { FromAddress, Subject, TextBody, Label }

public enum ConditionOperator { Contains, NotContains, StartsWith, EndsWith, Equals, NotEquals }

public enum LogicGate { AND, OR }

public class RuleCondition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public ConditionField Field { get; set; } = ConditionField.Subject;
    public ConditionOperator Operator { get; set; } = ConditionOperator.Contains;
    public string Value { get; set; } = string.Empty;
    public LogicGate LogicGate { get; set; } = LogicGate.AND;
    public int SortOrder { get; set; }

    public AutomationRule? Rule { get; set; }
}
