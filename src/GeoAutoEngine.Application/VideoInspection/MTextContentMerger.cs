using System.Text;

namespace GeoAutoEngine.Application.VideoInspection;

/// <summary>
/// Aplica ao conteúdo de um MText já editado pelo desenhista somente as palavras que mudaram
/// no texto gerado, preservando quebras de parágrafo (\P), espaços e códigos de formatação
/// que o desenhista deixou. Ex.: "12,00 m TRINCA" (linhas unidas à mão) + gerado
/// "12,00 m\PFISSURA" resulta em "12,00 m FISSURA".
/// </summary>
public static class MTextContentMerger
{
    /// <summary>
    /// Retorna o conteúdo atual com as diferenças de palavras aplicadas. Se as palavras forem
    /// iguais (sem diferenciar maiúsculas), devolve <paramref name="currentContents"/> sem alteração.
    /// </summary>
    public static string Merge(string currentContents, string generatedContents)
    {
        ArgumentNullException.ThrowIfNull(currentContents);
        ArgumentNullException.ThrowIfNull(generatedContents);

        var segments = Tokenize(currentContents);
        var currentWords = new List<int>();
        for (var index = 0; index < segments.Count; index++)
        {
            if (segments[index].IsWord)
            {
                currentWords.Add(index);
            }
        }

        var generatedWords = Tokenize(generatedContents).Where(segment => segment.IsWord).ToArray();
        if (currentWords.Count == 0)
        {
            return generatedWords.Length == 0 ? currentContents : generatedContents;
        }

        var operations = Diff(
            currentWords.Select(index => segments[index].Plain).ToArray(),
            generatedWords.Select(segment => segment.Plain).ToArray());
        if (operations.All(operation => operation.Kind == EditKind.Equal))
        {
            return currentContents;
        }

        var replacements = new string?[segments.Count];
        var removed = new bool[segments.Count];
        var lastEqualWord = -1;
        var position = 0;
        while (position < operations.Count)
        {
            if (operations[position].Kind == EditKind.Equal)
            {
                lastEqualWord = operations[position].CurrentIndex;
                position++;
                continue;
            }

            var deleted = new List<int>();
            var inserted = new List<string>();
            while (position < operations.Count && operations[position].Kind != EditKind.Equal)
            {
                var operation = operations[position];
                if (operation.Kind == EditKind.Delete)
                {
                    deleted.Add(operation.CurrentIndex);
                }
                else
                {
                    inserted.Add(generatedWords[operation.GeneratedIndex].Raw);
                }

                position++;
            }

            var insertedText = string.Join(" ", inserted);
            if (deleted.Count > 0)
            {
                var first = currentWords[deleted[0]];
                var last = currentWords[deleted[deleted.Count - 1]];
                for (var index = first + 1; index <= last; index++)
                {
                    if (segments[index].IsWord || segments[index].IsPlainSeparator)
                    {
                        removed[index] = true;
                    }
                }

                if (inserted.Count > 0)
                {
                    replacements[first] = insertedText;
                }
                else
                {
                    removed[first] = true;
                    RemoveOneNeighbourSeparator(segments, removed, first, last, lastEqualWord >= 0);
                }
            }
            else if (lastEqualWord >= 0)
            {
                var anchor = currentWords[lastEqualWord];
                replacements[anchor] = (replacements[anchor] ?? segments[anchor].Raw) + " " + insertedText;
            }
            else
            {
                var firstWord = currentWords[0];
                replacements[firstWord] = insertedText + " " + (replacements[firstWord] ?? segments[firstWord].Raw);
            }
        }

        var result = new StringBuilder(currentContents.Length + 16);
        for (var index = 0; index < segments.Count; index++)
        {
            if (!removed[index])
            {
                result.Append(replacements[index] ?? segments[index].Raw);
            }
        }

        return result.ToString();
    }

    private static void RemoveOneNeighbourSeparator(
        IReadOnlyList<Segment> segments,
        bool[] removed,
        int first,
        int last,
        bool hasWordBefore)
    {
        if (hasWordBefore && first > 0 && segments[first - 1].IsPlainSeparator && !removed[first - 1])
        {
            removed[first - 1] = true;
            return;
        }

        if (last + 1 < segments.Count && segments[last + 1].IsPlainSeparator)
        {
            removed[last + 1] = true;
        }
    }

    private enum EditKind
    {
        Equal,
        Delete,
        Insert,
    }

    private readonly record struct EditOperation(EditKind Kind, int CurrentIndex, int GeneratedIndex);

    private static List<EditOperation> Diff(IReadOnlyList<string> current, IReadOnlyList<string> generated)
    {
        var lengths = new int[current.Count + 1, generated.Count + 1];
        for (var i = current.Count - 1; i >= 0; i--)
        {
            for (var j = generated.Count - 1; j >= 0; j--)
            {
                lengths[i, j] = SameWord(current[i], generated[j])
                    ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }

        var operations = new List<EditOperation>(current.Count + generated.Count);
        int c = 0, g = 0;
        while (c < current.Count && g < generated.Count)
        {
            if (SameWord(current[c], generated[g]))
            {
                operations.Add(new EditOperation(EditKind.Equal, c++, g++));
            }
            else if (lengths[c + 1, g] >= lengths[c, g + 1])
            {
                operations.Add(new EditOperation(EditKind.Delete, c++, -1));
            }
            else
            {
                operations.Add(new EditOperation(EditKind.Insert, -1, g++));
            }
        }

        while (c < current.Count)
        {
            operations.Add(new EditOperation(EditKind.Delete, c++, -1));
        }

        while (g < generated.Count)
        {
            operations.Add(new EditOperation(EditKind.Insert, -1, g++));
        }

        return operations;
    }

    private static bool SameWord(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private readonly record struct Segment(bool IsWord, string Raw, string Plain, bool IsPlainSeparator);

    private static List<Segment> Tokenize(string contents)
    {
        var segments = new List<Segment>();
        var word = new StringBuilder();
        var plain = new StringBuilder();
        var separator = new StringBuilder();
        var separatorIsPlain = true;

        void FlushWord()
        {
            if (word.Length > 0)
            {
                segments.Add(new Segment(true, word.ToString(), plain.ToString(), false));
                word.Clear();
                plain.Clear();
            }
        }

        void FlushSeparator()
        {
            if (separator.Length > 0)
            {
                segments.Add(new Segment(false, separator.ToString(), string.Empty, separatorIsPlain));
                separator.Clear();
                separatorIsPlain = true;
            }
        }

        void AppendSeparator(string raw, bool isPlain)
        {
            FlushWord();
            separator.Append(raw);
            separatorIsPlain &= isPlain;
        }

        void AppendWord(string raw, string visible)
        {
            FlushSeparator();
            word.Append(raw);
            plain.Append(visible);
        }

        var position = 0;
        while (position < contents.Length)
        {
            var character = contents[position];
            if (character == '\\' && position + 1 < contents.Length)
            {
                var code = contents[position + 1];
                switch (code)
                {
                    case '\\':
                    case '{':
                    case '}':
                        AppendWord(contents.Substring(position, 2), code.ToString());
                        position += 2;
                        continue;
                    case 'P':
                    case 'N':
                    case '~':
                        AppendSeparator(contents.Substring(position, 2), isPlain: true);
                        position += 2;
                        continue;
                    case 'L':
                    case 'l':
                    case 'O':
                    case 'o':
                    case 'K':
                    case 'k':
                        AppendSeparator(contents.Substring(position, 2), isPlain: false);
                        position += 2;
                        continue;
                    case 'U' when position + 7 <= contents.Length &&
                                  contents[position + 2] == '+' &&
                                  TryParseHex(contents.Substring(position + 3, 4), out var unicode):
                        AppendWord(contents.Substring(position, 7), ((char)unicode).ToString());
                        position += 7;
                        continue;
                    default:
                        var end = contents.IndexOf(';', position + 2);
                        if (FormattingCodesWithArgument.IndexOf(code) >= 0 && end > 0)
                        {
                            AppendSeparator(contents.Substring(position, end - position + 1), isPlain: false);
                            position = end + 1;
                            continue;
                        }

                        break;
                }
            }

            if (character is '{' or '}')
            {
                AppendSeparator(character.ToString(), isPlain: false);
            }
            else if (char.IsWhiteSpace(character))
            {
                AppendSeparator(character.ToString(), isPlain: true);
            }
            else
            {
                AppendWord(character.ToString(), character.ToString());
            }

            position++;
        }

        FlushWord();
        FlushSeparator();
        return segments;
    }

    private const string FormattingCodesWithArgument = "fFHCcTQWApS";

    private static bool TryParseHex(string value, out int result) => int.TryParse(
        value,
        System.Globalization.NumberStyles.HexNumber,
        System.Globalization.CultureInfo.InvariantCulture,
        out result);
}
