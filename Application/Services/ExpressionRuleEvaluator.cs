using System.Linq.Expressions;
using System.Text.Json;
using CargoInbox.Core.Entities;

namespace CargoInbox.Application.Services;

public class ExpressionRuleEvaluator
{
    public Func<ConversationMessage, bool> BuildPredicate(AutomationRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.ConditionsJson) || rule.ConditionsJson == "[]")
        {
            return msg => msg.Subject.Contains(rule.ConditionKeyword, StringComparison.OrdinalIgnoreCase)
                       || msg.TextBody.Contains(rule.ConditionKeyword, StringComparison.OrdinalIgnoreCase);
        }

        var conditions = JsonSerializer.Deserialize<List<RuleCondition>>(rule.ConditionsJson)
                         ?? [];

        if (conditions.Count == 0)
            return _ => true;

        var param = Expression.Parameter(typeof(ConversationMessage), "m");
        Expression? combined = null;
        LogicGate currentGate = LogicGate.AND;

        foreach (var c in conditions.OrderBy(c => c.SortOrder))
        {
            var expr = BuildConditionExpression(c, param);
            combined = combined == null
                ? expr
                : currentGate == LogicGate.AND
                    ? Expression.AndAlso(combined, expr)
                    : Expression.OrElse(combined, expr);
            currentGate = c.LogicGate;
        }

        return Expression.Lambda<Func<ConversationMessage, bool>>(combined!, param).Compile();
    }

    private Expression BuildConditionExpression(RuleCondition c, ParameterExpression param)
    {
        Expression property = c.Field switch
        {
            ConditionField.FromAddress => Expression.Property(param, nameof(ConversationMessage.FromAddress)),
            ConditionField.Subject => Expression.Property(param, nameof(ConversationMessage.Subject)),
            ConditionField.TextBody => Expression.Property(param, nameof(ConversationMessage.TextBody)),
            _ => Expression.Property(param, nameof(ConversationMessage.Subject))
        };

        var value = Expression.Constant(c.Value, typeof(string));
        var ignoreCase = Expression.Constant(StringComparison.OrdinalIgnoreCase);

        return c.Operator switch
        {
            ConditionOperator.Contains => Expression.Call(property, nameof(string.Contains), Type.EmptyTypes, value),
            ConditionOperator.NotContains => Expression.Not(Expression.Call(property, nameof(string.Contains), Type.EmptyTypes, value)),
            ConditionOperator.StartsWith => Expression.Call(property, nameof(string.StartsWith), Type.EmptyTypes, value, ignoreCase),
            ConditionOperator.EndsWith => Expression.Call(property, nameof(string.EndsWith), Type.EmptyTypes, value, ignoreCase),
            ConditionOperator.Equals => Expression.Call(property, nameof(string.Equals), Type.EmptyTypes, value, ignoreCase),
            ConditionOperator.NotEquals => Expression.Not(Expression.Call(property, nameof(string.Equals), Type.EmptyTypes, value, ignoreCase)),
            _ => Expression.Call(property, nameof(string.Contains), Type.EmptyTypes, value)
        };
    }
}
