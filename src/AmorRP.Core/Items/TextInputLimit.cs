using System.Globalization;
using System.Text;

namespace AmorRP.Core.Items;

// Limit the inserted portion of a paste while keeping the text after the selection.
// Counts match server validation and never split a Unicode text element.
public static class TextInputLimit
{
    public sealed record Edit(string Text, int RemovedStart, int RemovedEnd)
    {
        public int Position(int position) => Math.Clamp(position <= RemovedStart ? position
            : position < RemovedEnd ? RemovedStart : position - (RemovedEnd - RemovedStart), 0, Text.Length);
    }

    public static Edit Apply(string previous, string candidate, int maximum, int maxBytes = int.MaxValue, int maxCodeUnits = int.MaxValue)
    {
        var boundaries = StringInfo.ParseCombiningCharacters(candidate);
        if (boundaries.Length <= maximum && candidate.Length <= maxCodeUnits && Encoding.UTF8.GetByteCount(candidate) <= maxBytes)
            return new(candidate, candidate.Length, candidate.Length);
        var old = StringInfo.ParseCombiningCharacters(previous);
        int End(int[] starts, int index, int length) => index < starts.Length ? starts[index] : length;
        bool Same(int a, int b) => previous.AsSpan(old[a], End(old, a + 1, previous.Length) - old[a])
            .SequenceEqual(candidate.AsSpan(boundaries[b], End(boundaries, b + 1, candidate.Length) - boundaries[b]));
        var prefix = 0;
        while (prefix < Math.Min(old.Length, boundaries.Length) && Same(prefix, prefix)) prefix++;
        var suffix = 0;
        while (suffix < Math.Min(old.Length, boundaries.Length) - prefix && Same(old.Length - suffix - 1, boundaries.Length - suffix - 1)) suffix++;
        var suffixStart = End(boundaries, boundaries.Length - suffix, candidate.Length);
        var keepCount = Math.Max(prefix, Math.Min(boundaries.Length - suffix, maximum - suffix));
        var keep = End(boundaries, keepCount, candidate.Length);
        bool Fits() => keep + candidate.Length - suffixStart <= maxCodeUnits && Encoding.UTF8.GetByteCount(candidate.AsSpan(0, keep))
            + Encoding.UTF8.GetByteCount(candidate.AsSpan(suffixStart)) <= maxBytes;
        while (keepCount > prefix && !Fits())
            keep = End(boundaries, --keepCount, candidate.Length);
        if (prefix + suffix > maximum || !Fits())
        {
            // Editing a legacy value that already exceeded the current limit.
            suffixStart = candidate.Length;
            keepCount = Math.Min(Math.Max(0, maximum), boundaries.Length);
            keep = End(boundaries, keepCount, candidate.Length);
            while (keepCount > 0 && !Fits())
                keep = End(boundaries, --keepCount, candidate.Length);
        }
        return new(candidate[..keep] + candidate[suffixStart..], keep, suffixStart);
    }
}
