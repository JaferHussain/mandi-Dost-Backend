using System.ComponentModel.DataAnnotations;
using MandiDost.Api.Services;
using MandiDost.Api.Services.Voice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MandiDost.Api.Controllers;

public class ParseVoiceRequest
{
    [Required, StringLength(800)]
    public string Text { get; set; } = "";
}

public record TranscriptDto(string Text);
public record VoiceStatusDto(bool SpeechRecognition);

[ApiController]
[Route("api/voice")]
public class VoiceController(IVoiceService voice) : ControllerBase
{
    /// <summary>
    /// Understands a sentence ("دو ساہیوال گائے بیچنی ہیں، 80 ہزار، لودھراں") and returns the form fields it found.
    /// It only reads the text. Nothing is created or published.
    /// </summary>
    [HttpPost("parse")]
    public async Task<VoiceParseResult> Parse(ParseVoiceRequest request, CancellationToken ct) =>
        await voice.ParseAsync(request.Text, ct);

    /// <summary>Whether the server can turn a recording into text. When false the app offers typing or the keyboard's own microphone.</summary>
    [HttpGet("status")]
    public VoiceStatusDto Status() => new(voice.SpeechRecognitionAvailable);

    /// <summary>
    /// Recording → text. Form fields: <c>file</c> (m4a/3gp/webm/ogg/mp3/aac, up to 2 MB) and optional <c>language</c> (ur or pa).
    /// Needs the speech service to be set up; otherwise answers 501 <c>voice_not_configured</c>.
    /// </summary>
    [HttpPost("transcribe")]
    [Authorize]
    [EnableRateLimiting("voice")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<TranscriptDto> Transcribe(IFormFile file, [FromForm] string? language, CancellationToken ct) =>
        new(await voice.TranscribeAsync(file, language ?? "ur", ct));
}
