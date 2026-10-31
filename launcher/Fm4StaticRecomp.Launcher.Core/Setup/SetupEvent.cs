// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fm4StaticRecomp.Launcher.Core.Setup;

public sealed record SetupEvent(
    [property: JsonPropertyName("stage")] string Stage,
    [property: JsonPropertyName("percent")] int Percent,
    [property: JsonPropertyName("message")] string Message)
{
    private const string Prefix = "::fm4::";

    public static SetupEvent? Parse(string line)
    {
        if (!line.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        try { return JsonSerializer.Deserialize<SetupEvent>(line[Prefix.Length..]); }
        catch (JsonException) { return null; }
    }
}
