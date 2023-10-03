
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

public class RuleEngine<T> where T : RuleContextBase
{
    private readonly List<Rule<T>> rules = new List<Rule<T>>();

    public void AddRule(string ruleExpression, Action<T> action)
    {
        rules.Add(new Rule<T> { RuleExpression = ruleExpression, Action = action });
    }

    public void ExecuteRules(T context, List<ParameterConfiguration> parameterConfigurations)
    {
        // Relpace Parameter in place of query 

        foreach (var rule in rules)
        {
            foreach (var parameterConfiguration in parameterConfigurations)
            {
                rule.RuleExpression = rule.RuleExpression.Replace(string.Format("{0}", parameterConfiguration.AliasName), parameterConfiguration.Name);
                //ruleExpression = ruleExpression.Replace("[P2]", "'" + context.P2 + "'");
            }
        }

        foreach (var rule in rules)
        {         
            var ruleResult = EvaluateRuleExpression(context, rule.RuleExpression, parameterConfigurations);
            if (ruleResult)
            {
                rule.Action(context);
                break; // Stop after the first rule that matches
            }
        }
    }

    private bool EvaluateRuleExpression(T context, string ruleExpression, List<ParameterConfiguration> parameterConfigurations)
    {
        try
        {
            // Replace placeholders in the rule expression with actual values
            if (parameterConfigurations.Any())
            {
                foreach (var parameterConfiguration in parameterConfigurations)
                {
                    if (parameterConfiguration.Type == "String")
                    {
                        ruleExpression = ruleExpression.Replace(string.Format("[{0}]", parameterConfiguration.Name), "'" + context.GetType().GetProperty(parameterConfiguration.Name).GetValue(context, null).ToString() + "'");
                    }
                    if(parameterConfiguration.Type =="Double")
                    {
                        ruleExpression = ruleExpression.Replace(string.Format("[{0}]", parameterConfiguration.Name), context.GetType().GetProperty(parameterConfiguration.Name).GetValue(context, null).ToString());
                    }
                }
            }

            // Use DataTable.Compute to evaluate the modified expression
            var result = new DataTable().Compute(ruleExpression, null);
            return Convert.ToBoolean(result);
        }
        catch (Exception ex)
        {
            //Console.WriteLine($"Error evaluating expression: {ex.Message}");
            return false; // Handle the error gracefully and return false
        }
    }
}

public class RuleContextBase
{
    public dynamic P1 { get; set; }
    public dynamic P2 { get; set; }

    public dynamic Role { get; set; }
}


public class Rule<T>
{
    public string RuleExpression { get; set; }
    public Action<T> Action { get; set; }
}

class Program
{
    static void Main(string[] args)
    {

        var ruleContextBase = new RuleContextBase()
        {
            P1 = 8.0,
            P2 = "North",
            Role = string.Empty
        };


        var dbExpresssion = new DbExpresssion()
        {
            ParameterConfigurations = new List<ParameterConfiguration>(),
            DynamicExpresssion = new Dictionary<string, string>()
        };

        dbExpresssion.DynamicExpresssion.Add("[SalesLimit] <= 7.0 AND ([Region] = 'North' OR [Region] = 'East')", "L1");
        dbExpresssion.DynamicExpresssion.Add("[SalesLimit] <= 7.5 AND [Region] = 'South'", "L2");
        dbExpresssion.DynamicExpresssion.Add("[SalesLimit] >= 7.01 AND [SalesLimit] <= 15.0 AND ([Region] = 'North' OR [Region] = 'East' OR [Region] = 'West')", "L3");
        dbExpresssion.DynamicExpresssion.Add("[SalesLimit] >= 7.51 AND [SalesLimit] <= 20.0 AND ([Region] = 'North' OR [Region] = 'East' OR [Region] = 'West')", "L4");
        dbExpresssion.DynamicExpresssion.Add("[SalesLimit] >= 10.01 AND [SalesLimit] <= 20.0 AND ([Region] = 'North' OR [Region] = 'East' OR [Region] = 'West')", "L5");
        dbExpresssion.DynamicExpresssion.Add("[SalesLimit] >= 15.01 AND [SalesLimit] <= 25.0 AND [Region] = 'South'", "L6");
        dbExpresssion.DynamicExpresssion.Add("[SalesLimit] >= 20.01", "VP");

        dbExpresssion.ParameterConfigurations.Add(new ParameterConfiguration()
        {
            Name = "P1",
            AliasName = "SalesLimit",
            Type = "Double"
        });
        dbExpresssion.ParameterConfigurations.Add(new ParameterConfiguration()
        {
            Name = "P2",
            AliasName = "Region",
            Type = "String"
        });

        var ruleEngine = new RuleEngine<RuleContextBase>();
        foreach (var dynamicExpression in dbExpresssion.DynamicExpresssion)
        {
            ruleEngine.AddRule(dynamicExpression.Key, context => context.Role = dynamicExpression.Value);
        }

        ruleEngine.ExecuteRules(ruleContextBase, dbExpresssion.ParameterConfigurations);
        Console.WriteLine(string.Format(ruleContextBase.Role));

    }
}

public class DbExpresssion
{
    public List<ParameterConfiguration> ParameterConfigurations { get; set; }
    public Dictionary<string, string> DynamicExpresssion { get; set; }
}

public class ParameterConfiguration
{
    public string Name { get; set; }
    public string AliasName { get; set; }
    public string Type { get; set; }
}
