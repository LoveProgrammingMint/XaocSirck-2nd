using System.Text;

namespace Charwolf.XSRule;

public enum XsRuleTokenType
{
    End,
    Define,
    String,
    Conditions,
    Identifier,
    StringLiteral,
    Number,
    And,
    Or,
    Not,
    OpenBrace,
    CloseBrace,
    OpenParen,
    CloseParen,
    Semicolon
}

public readonly struct XsRuleToken
{
    public readonly XsRuleTokenType Type;
    public readonly String Text;
    public readonly Int32 Line;
    public readonly Int32 Column;

    public XsRuleToken(XsRuleTokenType type, String text, Int32 line, Int32 column)
    {
        Type = type;
        Text = text;
        Line = line;
        Column = column;
    }
}

public sealed class XsRuleLexer
{
    private readonly String _source;
    private Int32 _pos;
    private Int32 _line;
    private Int32 _column;

    public XsRuleLexer(String source)
    {
        _source = RemoveComments(source);
        _pos = 0;
        _line = 1;
        _column = 1;
    }

    public XsRuleToken NextToken()
    {
        SkipWhitespace();

        if (_pos >= _source.Length)
            return new(XsRuleTokenType.End, String.Empty, _line, _column);

        Char c = _source[_pos];
        Int32 line = _line;
        Int32 column = _column;

        if (c == '{') { Advance(); return new(XsRuleTokenType.OpenBrace, "{", line, column); }
        if (c == '}') { Advance(); return new(XsRuleTokenType.CloseBrace, "}", line, column); }
        if (c == '(') { Advance(); return new(XsRuleTokenType.OpenParen, "(", line, column); }
        if (c == ')') { Advance(); return new(XsRuleTokenType.CloseParen, ")", line, column); }
        if (c == ';') { Advance(); return new(XsRuleTokenType.Semicolon, ";", line, column); }

        if (c == '"')
            return ReadStringLiteral(line, column);

        if (Char.IsDigit(c))
            return ReadNumber(line, column);

        if (Char.IsLetter(c) || c == '_')
            return ReadIdentifierOrKeyword(line, column);

        throw new XsRuleParseException($"Unexpected character '{c}'", line, column);
    }

    private XsRuleToken ReadStringLiteral(Int32 line, Int32 column)
    {
        Advance();
        StringBuilder sb = new();
        while (_pos < _source.Length)
        {
            Char c = _source[_pos];
            if (c == '"')
            {
                Advance();
                return new(XsRuleTokenType.StringLiteral, sb.ToString(), line, column);
            }
            if (c == '\n')
                throw new XsRuleParseException("Unterminated string literal", line, column);

            sb.Append(c);
            Advance();
        }
        throw new XsRuleParseException("Unterminated string literal", line, column);
    }

    private XsRuleToken ReadNumber(Int32 line, Int32 column)
    {
        StringBuilder sb = new();
        while (_pos < _source.Length && Char.IsDigit(_source[_pos]))
        {
            sb.Append(_source[_pos]);
            Advance();
        }
        return new(XsRuleTokenType.Number, sb.ToString(), line, column);
    }

    private XsRuleToken ReadIdentifierOrKeyword(Int32 line, Int32 column)
    {
        StringBuilder sb = new();
        while (_pos < _source.Length && (Char.IsLetterOrDigit(_source[_pos]) || _source[_pos] == '_'))
        {
            sb.Append(_source[_pos]);
            Advance();
        }

        String text = sb.ToString();
        XsRuleTokenType type = text switch
        {
            "define" => XsRuleTokenType.Define,
            "string" => XsRuleTokenType.String,
            "conditions" => XsRuleTokenType.Conditions,
            "and" => XsRuleTokenType.And,
            "or" => XsRuleTokenType.Or,
            "not" => XsRuleTokenType.Not,
            _ => XsRuleTokenType.Identifier
        };

        return new(type, text, line, column);
    }

    private void SkipWhitespace()
    {
        while (_pos < _source.Length && Char.IsWhiteSpace(_source[_pos]))
            Advance();
    }

    private void Advance()
    {
        if (_pos >= _source.Length)
            return;

        if (_source[_pos] == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }
        _pos++;
    }

    private static String RemoveComments(String source)
    {
        StringBuilder sb = new(source.Length);
        Int32 i = 0;
        Boolean inString = false;

        while (i < source.Length)
        {
            Char c = source[i];

            if (inString)
            {
                sb.Append(c);
                if (c == '"')
                    inString = false;
                i++;
                continue;
            }

            if (c == '"')
            {
                sb.Append(c);
                inString = true;
                i++;
                continue;
            }

            if (c == '<' && i + 1 < source.Length && source[i + 1] == '#')
            {
                Int32 end = source.IndexOf("#>", i + 2, StringComparison.Ordinal);
                if (end == -1)
                    break;
                i = end + 2;
                continue;
            }

            if (c == '#')
            {
                while (i < source.Length && source[i] != '\n')
                    i++;
                if (i < source.Length)
                {
                    sb.Append('\n');
                    i++;
                }
                continue;
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }
}

public sealed class XsRuleParseException : Exception
{
    public Int32 Line { get; }
    public Int32 Column { get; }

    public XsRuleParseException(String message, Int32 line, Int32 column)
        : base($"{message} at line {line}, column {column}")
    {
        Line = line;
        Column = column;
    }
}
