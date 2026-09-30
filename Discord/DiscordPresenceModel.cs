using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AppleMusicDiscordRPC.Discord;

public class DiscordHandshake
{
    [JsonPropertyName("v")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = string.Empty;
}

public class DiscordPayload<T>
{
    [JsonPropertyName("cmd")]
    public string Command { get; set; } = "SET_ACTIVITY";

    [JsonPropertyName("args")]
    public T? Args { get; set; }

    [JsonPropertyName("nonce")]
    public string Nonce { get; set; } = System.Guid.NewGuid().ToString();
}

public class SetActivityArgs
{
    [JsonPropertyName("pid")]
    public int Pid { get; set; }

    [JsonPropertyName("activity")]
    public DiscordActivity? Activity { get; set; }
}

public class DiscordActivity
{
    [JsonPropertyName("type")]
    public int Type { get; set; } = 2; // 2 = Listening to, 0 = Playing

    [JsonPropertyName("details")]
    public string? Details { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("timestamps")]
    public DiscordTimestamps? Timestamps { get; set; }

    [JsonPropertyName("assets")]
    public DiscordAssets? Assets { get; set; }

    [JsonPropertyName("buttons")]
    public List<DiscordButton>? Buttons { get; set; }
}

public class DiscordTimestamps
{
    [JsonPropertyName("start")]
    public long? Start { get; set; }

    [JsonPropertyName("end")]
    public long? End { get; set; }
}

public class DiscordAssets
{
    [JsonPropertyName("large_image")]
    public string? LargeImage { get; set; }

    [JsonPropertyName("large_text")]
    public string? LargeText { get; set; }

    [JsonPropertyName("small_image")]
    public string? SmallImage { get; set; }

    [JsonPropertyName("small_text")]
    public string? SmallText { get; set; }
}

public class DiscordButton
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
