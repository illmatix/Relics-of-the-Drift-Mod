namespace Baubles.Api;

public sealed record BaubleInstance(
    BaubleSlotType SlotType,
    string? PrefixCode,
    string? SuffixCode,
    long Seed,
    bool Identified);
