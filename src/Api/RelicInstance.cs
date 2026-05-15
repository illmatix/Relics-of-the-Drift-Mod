namespace DriftRelics.Api;

public sealed record RelicInstance(
    RelicSlotType SlotType,
    string? PrefixCode,
    string? SuffixCode,
    long Seed,
    bool Identified);
