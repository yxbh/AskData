using System.Text.Json.Serialization;

namespace AskData.KernelMemory.CLI.DataProcessor;

/// <summary> Represents a podcast episode with metadata and content.</summary>
public class PodcastEpisodeModel
{
    /// <summary> The title of the podcast episode.</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary> The URL link to the podcast episode.</summary>
    [JsonPropertyName("link")]
    public string Link { get; set; } = string.Empty;

    /// <summary> The publication date of the episode in RFC 2822 format.</summary>
    [JsonPropertyName("pubDate")]
    public string PubDate { get; set; } = string.Empty;

    /// <summary> The full  content of the episode.</summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; } = string.Empty;

    /// <summary> The full encoded content of the episode.</summary>
    [JsonPropertyName("contentEncoded")]
    public string? ContentEncoded { get; set; } = string.Empty;

    /// <summary> A snippet of the encoded content.</summary>
    [JsonPropertyName("contentEncodedSnippet")]
    public string? ContentEncodedSnippet { get; set; } = string.Empty;

    /// <summary> The enclosure information for the episode.</summary>
    [JsonPropertyName("enclosure")]
    public EnclosureModel? Enclosure { get; set; } = new ();

    /// <summary> The podcast transcripts available for this episode.</summary>
    [JsonPropertyName("podcastTranscripts")]
    public List<Transcript>? PodcastTranscripts { get; set; } = [];

    /// <summary> Additional metadata from the iTunes podcast directory.</summary>
    [JsonPropertyName("itunes")]
    public ItunesModel? Itunes { get; set; } = new ItunesModel();

    /// <summary> The GUID identifier for the episode.</summary>
    [JsonPropertyName("guid")]
    public string Guid { get; set; } = string.Empty;

    /// <summary> The ISO 8601 formatted date of the episode.</summary>
    [JsonPropertyName("isoDate")]
    public string IsoDate { get; set; } = string.Empty;

    /// <summary> Represents enclosure information for a podcast episode.</summary>
    public class EnclosureModel
    {
        /// <summary> The URL where the audio file can be downloaded.</summary>
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        /// <summary> The length of the audio file in bytes.</summary>
        [JsonPropertyName("length")]
        public string Length { get; set; } = string.Empty;

        /// <summary> The MIME type of the audio file.</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }

    /// <summary> Represents a transcript for the podcast episode.</summary>
    public class Transcript
    {
        /// <summary> The URL where the transcript can be accessed.</summary>
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        /// <summary> The MIME type of the transcript file.</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary> The language code for the transcript.</summary>
        [JsonPropertyName("language")]
        public string Language { get; set; } = string.Empty;
    }

    /// <summary> Represents metadata from the iTunes podcast directory.</summary>
    public class ItunesModel
    {
        /// <summary> The author of the podcast.</summary>
        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;

        /// <summary> A summary description of the episode.</summary>
        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        /// <summary> The duration of the episode in seconds.</summary>
        [JsonPropertyName("duration")]
        public string Duration { get; set; } = string.Empty;

        /// <summary> The URL to the podcast's image artwork.</summary>
        [JsonPropertyName("image")]
        public string Image { get; set; } = string.Empty;

        /// <summary> The type of episode (e.g., full, trailer).</summary>
        [JsonPropertyName("episodeType")]
        public string EpisodeType { get; set; } = string.Empty;
    }
}
