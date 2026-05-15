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

    // Fold a 64-bit seed into a 32-bit Random seed. The XOR fold can alias
    // distant seeds (e.g. seed=0 and seed=-1 both fold to 0; any two seeds
    // whose halves XOR to the same int collide). For the bauble use case,
    // seeds are derived from entity-id + GUID hashes which are well-distributed
    // in 64 bits, so the practical collision rate is negligible. If you change
    // this fold, all unidentified bauble names with persisted seeds will
    // change — that is a save-compatibility break.
    private static int SeedToInt(long seed) => (int)(seed ^ (seed >>> 32));
}
