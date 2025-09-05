namespace SoundboardMAUI;

using System.Text.Json.Serialization;

public class AudioRequest
{
	[JsonPropertyName("video_url")]
	public string VideoUrl { get; set; }
}

public class AudioResponse
{
	[JsonPropertyName("download_url")]
	public string DownloadUrl { get; set; }

	[JsonPropertyName("message")]
	public string Message { get; set; }
}