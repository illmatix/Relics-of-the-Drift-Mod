using System;
using System.Text;

namespace Baubles.Affixes;

public static class ScrambleNameGenerator
{
    private static readonly string[] Consonants =
    {
        "th", "sk", "vr", "dr", "kr", "mk", "ven", "ul", "drai", "sko",
        "zh", "fn", "gr", "pl", "qor", "rha"
    };

    private static readonly string[] Vowels =
    {
        "ai", "ul", "ok", "oo", "ae", "io", "an", "or", "ei", "uu"
    };

    public static string Generate(long seed)
    {
        var rng = new Random(SeedToInt(seed));
        int syllableCount = rng.Next(2, 5);
        var sb = new StringBuilder();

        for (int i = 0; i < syllableCount; i++)
        {
            sb.Append(Consonants[rng.Next(Consonants.Length)]);
            sb.Append(Vowels[rng.Next(Vowels.Length)]);
        }

        // Optional " of " connector that splits the name into two halves.
        if (rng.NextDouble() < 0.4)
        {
            int extraSyllables = rng.Next(1, 3);
            sb.Append(" of ");
            for (int i = 0; i < extraSyllables; i++)
            {
                sb.Append(Consonants[rng.Next(Consonants.Length)]);
                sb.Append(Vowels[rng.Next(Vowels.Length)]);
            }
            // Capitalise the second half too.
            int spaceIdx = sb.ToString().LastIndexOf(' ');
            sb[spaceIdx + 1] = char.ToUpperInvariant(sb[spaceIdx + 1]);
        }

        sb[0] = char.ToUpperInvariant(sb[0]);
        return sb.ToString();
    }

    private static int SeedToInt(long seed) => (int)(seed ^ (seed >>> 32));
}
