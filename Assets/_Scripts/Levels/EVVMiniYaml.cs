using System;
using System.Collections.Generic;
using System.Globalization;

// Minimal indentation-based YAML reader for the level file subset used by EVVLevelLoader:
// nested mappings, sequences (including sequences of mappings), quoted/unquoted scalars,
// and '#' comments. It is not a general-purpose YAML parser.
public static class EVVMiniYaml
{
    struct Line
    {
        public int Indent;
        public string Content;
    }

    public static object Parse(string yamlText)
    {
        List<Line> lines = Tokenize(yamlText);
        if (lines.Count == 0)
        {
            return null;
        }

        int cursor = 0;
        return ParseBlock(lines, ref cursor, lines[0].Indent);
    }

    static List<Line> Tokenize(string text)
    {
        List<Line> lines = new List<Line>();
        string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
        string[] rawLines = normalized.Split('\n');

        for (int i = 0; i < rawLines.Length; i++)
        {
            string stripped = StripComment(rawLines[i]);
            if (string.IsNullOrWhiteSpace(stripped))
            {
                continue;
            }

            int indent = 0;
            while (indent < stripped.Length && stripped[indent] == ' ')
            {
                indent++;
            }

            string content = stripped.Substring(indent).TrimEnd();
            if (content.Length == 0)
            {
                continue;
            }

            Line line;
            line.Indent = indent;
            line.Content = content;
            lines.Add(line);
        }

        return lines;
    }

    static string StripComment(string line)
    {
        bool inSingleQuote = false;
        bool inDoubleQuote = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
            }
            else if (c == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
            }
            else if (c == '#' && !inSingleQuote && !inDoubleQuote)
            {
                bool atLineStart = i == 0;
                bool precededByWhitespace = i > 0 && char.IsWhiteSpace(line[i - 1]);
                if (atLineStart || precededByWhitespace)
                {
                    return line.Substring(0, i);
                }
            }
        }

        return line;
    }

    static object ParseBlock(List<Line> lines, ref int cursor, int indent)
    {
        if (cursor >= lines.Count)
        {
            return null;
        }

        if (IsSequenceItem(lines[cursor].Content))
        {
            return ParseSequence(lines, ref cursor, indent);
        }

        return ParseMapping(lines, ref cursor, indent);
    }

    static bool IsSequenceItem(string content)
    {
        return content == "-" || content.StartsWith("- ", StringComparison.Ordinal);
    }

    static List<object> ParseSequence(List<Line> lines, ref int cursor, int indent)
    {
        List<object> list = new List<object>();

        while (cursor < lines.Count && lines[cursor].Indent == indent && IsSequenceItem(lines[cursor].Content))
        {
            string content = lines[cursor].Content.Length > 1
                ? lines[cursor].Content.Substring(1).TrimStart()
                : "";
            cursor++;

            if (content.Length == 0)
            {
                if (cursor < lines.Count && lines[cursor].Indent > indent)
                {
                    list.Add(ParseBlock(lines, ref cursor, lines[cursor].Indent));
                }
                else
                {
                    list.Add(null);
                }

                continue;
            }

            int colonIndex = FindMappingColon(content);
            if (colonIndex < 0)
            {
                list.Add(ParseScalar(content));
                continue;
            }

            Dictionary<string, object> map = new Dictionary<string, object>();
            int childIndent = cursor < lines.Count ? lines[cursor].Indent : -1;
            ApplyKeyValueContent(map, content, lines, ref cursor, indent);

            while (cursor < lines.Count
                && childIndent > indent
                && lines[cursor].Indent == childIndent
                && !IsSequenceItem(lines[cursor].Content))
            {
                ParseMappingEntry(lines, ref cursor, childIndent, map);
            }

            list.Add(map);
        }

        return list;
    }

    static Dictionary<string, object> ParseMapping(List<Line> lines, ref int cursor, int indent)
    {
        Dictionary<string, object> map = new Dictionary<string, object>();

        while (cursor < lines.Count && lines[cursor].Indent == indent && !IsSequenceItem(lines[cursor].Content))
        {
            ParseMappingEntry(lines, ref cursor, indent, map);
        }

        return map;
    }

    static void ParseMappingEntry(List<Line> lines, ref int cursor, int indent, Dictionary<string, object> map)
    {
        string content = lines[cursor].Content;
        cursor++;
        ApplyKeyValueContent(map, content, lines, ref cursor, indent);
    }

    static void ApplyKeyValueContent(Dictionary<string, object> map, string content, List<Line> lines, ref int cursor, int parentIndent)
    {
        int colonIndex = FindMappingColon(content);
        if (colonIndex < 0)
        {
            return;
        }

        string key = content.Substring(0, colonIndex).Trim();
        string valueText = content.Substring(colonIndex + 1).Trim();

        if (valueText.Length > 0)
        {
            map[key] = ParseScalar(valueText);
            return;
        }

        if (cursor < lines.Count && lines[cursor].Indent > parentIndent)
        {
            map[key] = ParseBlock(lines, ref cursor, lines[cursor].Indent);
        }
        else
        {
            map[key] = null;
        }
    }

    static int FindMappingColon(string content)
    {
        bool inSingleQuote = false;
        bool inDoubleQuote = false;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];
            if (c == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
            }
            else if (c == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
            }
            else if (c == ':' && !inSingleQuote && !inDoubleQuote)
            {
                bool atLineEnd = i + 1 >= content.Length;
                bool followedBySpace = !atLineEnd && content[i + 1] == ' ';
                if (atLineEnd || followedBySpace)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    static object ParseScalar(string text)
    {
        text = text.Trim();

        bool isDoubleQuoted = text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"';
        bool isSingleQuoted = text.Length >= 2 && text[0] == '\'' && text[text.Length - 1] == '\'';
        if (isDoubleQuoted || isSingleQuoted)
        {
            return text.Substring(1, text.Length - 2);
        }

        int intValue;
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue))
        {
            return intValue;
        }

        float floatValue;
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue))
        {
            return floatValue;
        }

        if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(text, "null", StringComparison.OrdinalIgnoreCase) || text == "~")
        {
            return null;
        }

        return text;
    }
}
