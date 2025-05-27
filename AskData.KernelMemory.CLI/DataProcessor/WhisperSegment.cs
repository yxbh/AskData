using System.Text.Json.Serialization;

namespace AskData.KernelMemory.CLI.DataProcessor;

internal class WhisperSegment
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty; // The transcribed text segment

    [JsonPropertyName("start")]
    public double Start { get; set; } // Start time of the segment in seconds

    [JsonPropertyName("end")]
    public double End { get; set; } // End time of the segment in seconds

    [JsonPropertyName("id")]
    public int Id { get; set; } // Unique identifier for the segment

    [JsonPropertyName("start_time")]
    public string StartTime { get; set; } = string.Empty; // Start time formatted as a string (e.g., "00:01:23.456")

    [JsonPropertyName("end_time")]
    public string EndTime { get; set; } = string.Empty; // End time formatted as a string (e.g., "00:01:25.678")
}
