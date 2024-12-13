namespace TextLocalizer;

internal static class FileParser
{
    public static Dictionary<string, LocalizedText> Parse(TranslationsFileData fileData)
    {
        var dictionary = new Dictionary<string, LocalizedText>();
        var untranslatableSpan = "untranslatable".AsSpan();

        for (var i = 0; i < fileData.SourceText.Lines.Count; i++)
        {
            var line = fileData.SourceText.Lines[i];
            var spanLine = line.ToString().AsSpan().Trim();

            if (spanLine.IsEmpty || spanLine[0] == '#')
            {
                continue;
            }

            var colonIndex = FindKeyValueDelimiterIndex(spanLine);
            if (colonIndex == -1)
            {
                continue;
            }

            ReadOnlySpan<char> keyValuePart;
            var commentPart = ReadOnlySpan<char>.Empty;
            var commentIndex = spanLine.IndexOf('#');

            if (commentIndex != -1)
            {
                keyValuePart = spanLine.Slice(0, commentIndex).Trim();
                commentPart = spanLine.Slice(commentIndex + 1).Trim();
            }
            else
            {
                keyValuePart = spanLine;
            }

            var key = keyValuePart.Slice(0, colonIndex).TrimEnd();
            var value = keyValuePart.Slice(colonIndex + 1).TrimStart();

            if (value.Length > 1 &&
                (value[0] == '\'' && value[value.Length - 1] == '\'' ||
                 value[0] == '"' && value[value.Length - 1] == '"'))
            {
                value = value.Slice(1, value.Length - 2);
            }

            var untranslatable = commentPart.Equals(untranslatableSpan, StringComparison.OrdinalIgnoreCase);

            dictionary[key.ToString()] = new LocalizedText(value.ToString(), i, untranslatable);
        }

        return dictionary;
    }

    private static int FindKeyValueDelimiterIndex(ReadOnlySpan<char> line)
    {
        var insideSingleQuotes = false;
        var insideDoubleQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var current = line[i];

            switch (current)
            {
                case '\'' when !insideDoubleQuotes:
                    insideSingleQuotes = !insideSingleQuotes;
                    break;
                case '"' when !insideSingleQuotes:
                    insideDoubleQuotes = !insideDoubleQuotes;
                    break;
                case ':' when !insideSingleQuotes && !insideDoubleQuotes:
                    return i;
            }
        }

        return -1;
    }
}
