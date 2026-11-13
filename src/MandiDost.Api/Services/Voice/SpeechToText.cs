using System.Net.Http.Headers;
using System.Text.Json;
using MandiDost.Api.Common;
using Microsoft.Extensions.Options;

namespace MandiDost.Api.Services.Voice;

/// <summary>
/// Settings for the speech-to-text service. Works with any service that offers the OpenAI "audio/transcriptions" API
/// (OpenAI, Groq and others). The key is a secret: set it with user-secrets or an environment variable, never in a file.
/// </summary>
public class VoiceOptions
{
    public const string Section = "Voice";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "whisper-1";
    /// <summary>Words the recogniser should expect. Helps it spell animals, places and numbers right.</summary>
    public string Hint { get; set; } = "گائے، بھینس، بکرا، بکری، دنبہ، اونٹ، قربانی، دو دانت، چار دانت، ہزار، لاکھ، کلو، من، لودھراں، ملتان، بہاولپور";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

public interface ISpeechToText
{
    bool IsConfigured { get; }
    Task<string> TranscribeAsync(byte[] audio, string fileName, string contentType, string language, CancellationToken ct = default);
}

public class OpenAiCompatibleSpeechToText(HttpClient http, IOptions<VoiceOptions> options, ILogger<OpenAiCompatibleSpeechToText> log) : ISpeechToText
{
    private readonly VoiceOptions _o = options.Value;
    public bool IsConfigured => _o.IsConfigured;

    public async Task<string> TranscribeAsync(byte[] audio, string fileName, string contentType, string language, CancellationToken ct = default)
    {
        if (!_o.IsConfigured)
            throw new AppException(StatusCodes.Status501NotImplemented, ErrorCodes.VoiceNotConfigured, "Voice recognition is not set up on this server.");

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(audio);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", fileName);
        form.Add(new StringContent(_o.Model), "model");
        form.Add(new StringContent(language), "language");
        form.Add(new StringContent("json"), "response_format");
        form.Add(new StringContent("0"), "temperature");
        if (!string.IsNullOrWhiteSpace(_o.Hint)) form.Add(new StringContent(_o.Hint), "prompt");

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_o.BaseUrl.TrimEnd('/')}/audio/transcriptions") { Content = form };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _o.ApiKey);

        try
        {
            using var res = await http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                log.LogWarning("Speech service answered {Status}: {Body}", (int)res.StatusCode, body.Length > 300 ? body[..300] : body);
                throw new AppException(StatusCodes.Status502BadGateway, ErrorCodes.VoiceServiceFailed, "The voice recognition service could not understand this recording.");
            }
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("text", out var t) ? (t.GetString() ?? "").Trim() : "";
        }
        catch (HttpRequestException e)
        {
            log.LogWarning(e, "Speech service not reachable");
            throw new AppException(StatusCodes.Status502BadGateway, ErrorCodes.VoiceServiceFailed, "The voice recognition service could not be reached.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AppException(StatusCodes.Status502BadGateway, ErrorCodes.VoiceServiceFailed, "The voice recognition service took too long.");
        }
        catch (JsonException)
        {
            throw new AppException(StatusCodes.Status502BadGateway, ErrorCodes.VoiceServiceFailed, "The voice recognition service sent an unexpected answer.");
        }
    }
}
