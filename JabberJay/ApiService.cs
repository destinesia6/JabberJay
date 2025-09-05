using System.Net;
using System.Text;
using System.Text.Json;
using SoundboardMAUI;

namespace JabberJay;

public class ApiService
{
	private readonly HttpClient _httpClient = new() { BaseAddress = new Uri(_cloudRunBaseUrl) };
	// IMPORTANT: Replace with your actual Cloud Run service URL
	private const string _cloudRunBaseUrl = "https://yt-dlp-api-service-339817114185.us-east1.run.app";

	/// <summary>
	/// Requests a signed download URL from the Cloud Run API.
	/// </summary>
	public async Task<AudioResponse?> RequestAudioDownloadUrl(string videoUrl)
	{
		AudioRequest requestData = new() { VideoUrl = videoUrl };
		StringContent jsonContent = new(
			JsonSerializer.Serialize(requestData),
			Encoding.UTF8,
			"application/json"
		);

		try
		{
			HttpResponseMessage response = await _httpClient.PostAsync("/download-audio", jsonContent); // Endpoint path
			response.EnsureSuccessStatusCode(); // Throws if status code is not 2xx

			string jsonResponse = await response.Content.ReadAsStringAsync();
			return JsonSerializer.Deserialize<AudioResponse>(jsonResponse);
		}
		catch (HttpRequestException ex)
		{
			Console.WriteLine($"API Request Error: {ex.Message}");
			return new AudioResponse { Message = $"API Request Failed: {ex.Message}" };
		}
		catch (JsonException ex)
		{
			Console.WriteLine($"JSON Deserialization Error: {ex.Message}");
			return new AudioResponse { Message = $"API Response Parse Error: {ex.Message}" };
		}
		catch (Exception ex)
		{
			Console.WriteLine($"An unexpected error occurred during API request: {ex.Message}");
			return new AudioResponse { Message = $"An unexpected error occurred: {ex.Message}" };
		}
	}

	/// <summary>
	/// Downloads a file from a given URL and saves it to a specified folder.
	/// </summary>
	/// <param name="fileUrl">The URL to download from (e.g., signed GCS URL).</param>
	/// <param name="targetFileName">The desired filename for the saved file.</param>
	/// <param name="outputDirectory">The local directory to save the file (e.g., FileSystem.AppDataDirectory).</param>
	/// <param name="progress">Optional progress reporter for download.</param>
	/// <returns>The full path to the downloaded file, or null if failed.</returns>
	public async Task<string?> DownloadFile(
		string fileUrl,
		string outputDirectory,
		IProgress<DownloadProgress>? progress = null)
	{
		try
		{
			// Ensure the output directory exists
			if (!Directory.Exists(outputDirectory))
			{
				Directory.CreateDirectory(outputDirectory);
			}

			string originalFileName = WebUtility.UrlDecode(Path.GetFileName(fileUrl));
			originalFileName = originalFileName[..originalFileName.LastIndexOf('?')];
			string nameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
			int fileNameLength = nameWithoutExtension.LastIndexOf('_');
			const int fileNameMaxLength = 35;
			if (fileNameLength > fileNameMaxLength) fileNameLength = fileNameMaxLength;
            
			string targetFileName = fileNameLength > 0 ? nameWithoutExtension[..fileNameLength] + Path.GetExtension(originalFileName) 
				: originalFileName;
            
			string filePath = Path.Combine(outputDirectory, targetFileName);

			if (File.Exists(filePath)) return null;


			using (HttpResponseMessage response = await _httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead))
			{
				response.EnsureSuccessStatusCode();

				long? totalBytes = response.Content.Headers.ContentLength;
				long totalBytesRead = 0;
				byte[] buffer = new byte[8192]; // 8KB buffer

				await using (Stream stream = await response.Content.ReadAsStreamAsync())
				{
					await using (FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
					{
						int bytesRead;
						while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
						{
							await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
							totalBytesRead += bytesRead;

							if (progress == null || !totalBytes.HasValue) continue;
							// Calculate progress as a percentage (0.0 to 1.0)
							double currentProgress = (double)totalBytesRead / totalBytes.Value;
							progress.Report(new DownloadProgress(currentProgress, totalBytesRead, totalBytes.Value));
						}
					}
				}
			}

			Console.WriteLine($"File downloaded to: {filePath}");
			return filePath; // Return the path where the file was saved
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error downloading file: {ex.Message}");
			return null;
		}
	}
}

// Model for download progress updates
public class DownloadProgress(double percentage, long bytesReceived, long totalBytes)
{
	public double Percentage { get; } = percentage;
	public long BytesReceived { get; } = bytesReceived;
	public long TotalBytes { get; } = totalBytes;
}