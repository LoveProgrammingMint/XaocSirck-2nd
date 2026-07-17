using System.Globalization;

namespace Charwolf.XSRule;

public sealed class XsRuleParser
{
    private readonly XsRuleLexer _lexer;
    private XsRuleToken _current;

    public XsRuleParser(String source)
    {
        _lexer = new XsRuleLexer(source);
        _current = _lexer.NextToken();
    }

    public XsRuleDocument ParseDocument()
    {
        XsRuleDocument document = new();
        while (_current.Type != XsRuleTokenType.End)
        {
            document.Rules.Add(ParseRule());
        }
        return document;
    }

    private XsRuleDefinition ParseRule()
    {
        Consume(XsRuleTokenType.Define);
        String name = ConsumeIdentifier();
        Consume(XsRuleTokenType.OpenBrace);

        XsRuleDefinition rule = new() { Name = name };

        while (_current.Type != XsRuleTokenType.CloseBrace)
        {
            if (_current.Type == XsRuleTokenType.String)
            {
                rule.Strings.AddRange(ParseStringSection());
            }
            else if (_current.Type == XsRuleTokenType.Conditions)
            {
                rule.Condition = ParseConditionSection();
            }
            else
            {
                throw new XsRuleParseException($"Expected 'string' or 'conditions', got '{_current.Text}'", _current.Line, _current.Column);
            }
        }

        Consume(XsRuleTokenType.CloseBrace);

        if (rule.Condition == null)
            throw new XsRuleParseException($"Rule '{name}' missing conditions", _current.Line, _current.Column);

        return rule;
    }

    private List<XsRuleString> ParseStringSection()
    {
        Consume(XsRuleTokenType.String);
        Consume(XsRuleTokenType.OpenBrace);

        List<XsRuleString> strings = [];
        Int32 id = 1;

        while (_current.Type != XsRuleTokenType.CloseBrace)
        {
            strings.Add(ParseStringEntry(id++));
        }

        Consume(XsRuleTokenType.CloseBrace);
        return strings;
    }

    private XsRuleString ParseStringEntry(Int32 id)
    {
        String literal = ConsumeStringLiteral();
        Consume(XsRuleTokenType.OpenParen);
        String partName = ConsumeIdentifier();
        Consume(XsRuleTokenType.CloseParen);
        String modifierName = ConsumeIdentifier();
        Consume(XsRuleTokenType.Semicolon);

        XsRulePart part = ParsePart(partName);
        XsRuleModifier modifier = ParseModifier(modifierName);

        Byte[] pattern;
        Boolean[] wildcardMask = [];
        Boolean hasWildcard = false;

        if (modifier == XsRuleModifier.Hex)
        {
            (pattern, wildcardMask, hasWildcard) = ParseHexPattern(literal);
        }
        else
        {
            pattern = ParseTextPattern(literal);
        }

        return new XsRuleString
        {
            Id = id,
            Pattern = pattern,
            WildcardMask = wildcardMask,
            Part = part,
            Modifier = modifier,
            HasWildcard = hasWildcard
        };
    }

    private XsRuleCondition ParseConditionSection()
    {
        Consume(XsRuleTokenType.Conditions);
        Consume(XsRuleTokenType.OpenBrace);
        XsRuleCondition condition = ParseConditionExpression();
        Consume(XsRuleTokenType.CloseBrace);
        return condition;
    }

    private XsRuleCondition ParseConditionExpression()
    {
        return ParseOr();
    }

    private XsRuleCondition ParseOr()
    {
        XsRuleCondition left = ParseAnd();
        while (_current.Type == XsRuleTokenType.Or)
        {
            Advance();
            XsRuleCondition right = ParseAnd();
            left = new XsRuleConditionOr { Left = left, Right = right };
        }
        return left;
    }

    private XsRuleCondition ParseAnd()
    {
        XsRuleCondition left = ParseNot();
        while (_current.Type == XsRuleTokenType.And)
        {
            Advance();
            XsRuleCondition right = ParseNot();
            left = new XsRuleConditionAnd { Left = left, Right = right };
        }
        return left;
    }

    private XsRuleCondition ParseNot()
    {
        if (_current.Type == XsRuleTokenType.Not)
        {
            Advance();
            return new XsRuleConditionNot { Operand = ParseNot() };
        }
        return ParsePrimary();
    }

    private XsRuleCondition ParsePrimary()
    {
        if (_current.Type == XsRuleTokenType.OpenParen)
        {
            Advance();
            XsRuleCondition condition = ParseConditionExpression();
            Consume(XsRuleTokenType.CloseParen);
            return condition;
        }

        if (_current.Type == XsRuleTokenType.Number)
        {
            Int32 id = Int32.Parse(_current.Text, CultureInfo.InvariantCulture);
            Advance();
            return new XsRuleConditionLiteral { StringId = id };
        }

        throw new XsRuleParseException($"Expected number or '(', got '{_current.Text}'", _current.Line, _current.Column);
    }

    private static XsRulePart ParsePart(String name)
    {
        return name.ToLowerInvariant() switch
        {
            "head" => XsRulePart.Head,
            "code" => XsRulePart.Code,
            "res" => XsRulePart.Res,
            "imp" => XsRulePart.Imp,
            "exp" => XsRulePart.Exp,
            _ => throw new XsRuleParseException($"Unknown part '{name}'", 0, 0)
        };
    }

    private static XsRuleModifier ParseModifier(String name)
    {
        return name.ToLowerInvariant() switch
        {
            "wildcard" => XsRuleModifier.Wildcard,
            "hex" => XsRuleModifier.Hex,
            _ => throw new XsRuleParseException($"Unknown modifier '{name}'", 0, 0)
        };
    }

    private static Byte[] ParseTextPattern(String literal)
    {
        return System.Text.Encoding.UTF8.GetBytes(literal);
    }

    private static (Byte[] Pattern, Boolean[] WildcardMask, Boolean HasWildcard) ParseHexPattern(String literal)
    {
        String trimmed = literal.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[^1] == ']')
            trimmed = trimmed[1..^1];

        List<Byte> bytes = [];
        List<Boolean> mask = [];
        Boolean hasWildcard = false;

        for (Int32 i = 0; i < trimmed.Length;)
        {
            if (Char.IsWhiteSpace(trimmed[i]))
            {
                i++;
                continue;
            }

            if (trimmed[i] == '?')
            {
                if (i + 1 < trimmed.Length && trimmed[i + 1] == '?')
                {
                    bytes.Add(0);
                    mask.Add(true);
                    hasWildcard = true;
                    i += 2;
                    continue;
                }
            }

            if (i + 1 >= trimmed.Length)
                throw new XsRuleParseException($"Invalid hex pattern '{literal}'", 0, 0);

            String hex = trimmed.Substring(i, 2);
            if (!Byte.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out Byte b))
                throw new XsRuleParseException($"Invalid hex byte '{hex}'", 0, 0);

            bytes.Add(b);
            mask.Add(false);
            i += 2;
        }

        return ([.. bytes], [.. mask], hasWildcard);
    }

    private String ConsumeIdentifier()
    {
        if (_current.Type != XsRuleTokenType.Identifier)
            throw new XsRuleParseException($"Expected identifier, got '{_current.Text}'", _current.Line, _current.Column);
        String text = _current.Text;
        Advance();
        return text;
    }

    private String ConsumeStringLiteral()
    {
        if (_current.Type != XsRuleTokenType.StringLiteral)
            throw new XsRuleParseException($"Expected string literal, got '{_current.Text}'", _current.Line, _current.Column);
        String text = _current.Text;
        Advance();
        return text;
    }

    private void Consume(XsRuleTokenType type)
    {
        if (_current.Type != type)
            throw new XsRuleParseException($"Expected {type}, got '{_current.Text}'", _current.Line, _current.Column);
        Advance();
    }

    private void Advance()
    {
        _current = _lexer.NextToken();
    }
}
