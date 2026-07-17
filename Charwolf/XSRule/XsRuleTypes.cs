namespace Charwolf.XSRule;

public enum XsRulePart : Byte
{
    Head,
    Code,
    Res,
    Imp,
    Exp
}

public enum XsRuleModifier : Byte
{
    Wildcard,
    Hex
}

public sealed class XsRuleString
{
    public Int32 Id { get; set; }
    public Byte[] Pattern { get; set; } = [];
    public Boolean[] WildcardMask { get; set; } = [];
    public XsRulePart Part { get; set; }
    public XsRuleModifier Modifier { get; set; }
    public Boolean HasWildcard { get; set; }
}

public abstract class XsRuleCondition
{
}

public sealed class XsRuleConditionLiteral : XsRuleCondition
{
    public Int32 StringId { get; set; }
}

public sealed class XsRuleConditionNot : XsRuleCondition
{
    public XsRuleCondition Operand { get; set; } = null!;
}

public sealed class XsRuleConditionAnd : XsRuleCondition
{
    public XsRuleCondition Left { get; set; } = null!;
    public XsRuleCondition Right { get; set; } = null!;
}

public sealed class XsRuleConditionOr : XsRuleCondition
{
    public XsRuleCondition Left { get; set; } = null!;
    public XsRuleCondition Right { get; set; } = null!;
}

public sealed class XsRuleDefinition
{
    public String Name { get; set; } = String.Empty;
    public List<XsRuleString> Strings { get; set; } = [];
    public XsRuleCondition Condition { get; set; } = null!;
}

public sealed class XsRuleDocument
{
    public List<XsRuleDefinition> Rules { get; set; } = [];
}
