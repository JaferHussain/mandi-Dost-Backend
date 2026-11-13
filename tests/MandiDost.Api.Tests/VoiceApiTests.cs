using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MandiDost.Api.Services.Voice;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace MandiDost.Api.Tests;

/// <summary>An API whose speech service is a fake, so we can see exactly what would be sent to the real one.</summary>
public class VoiceApiFactory : ApiFactory
{
    public FakeSpeechHandler Handler { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Voice:ApiKey", "test-key");
        builder.UseSetting("Voice:BaseUrl", "https://speech.example.test/v1");
        builder.ConfigureServices(services =>
            services.AddHttpClient<ISpeechToText, OpenAiCompatibleSpeechToText>().ConfigurePrimaryHttpMessageHandler(() => Handler));
    }
}

public class FakeSpeechHandler : HttpMessageHandler
{
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public string ResponseBody { get; set; } = "{\"text\":\"گائے بیچنی ہے\"}";
    public HttpRequestMessage? Request { get; private set; }
    public string? RequestBody { get; private set; }
    public int Calls { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        Request = request;
        RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        return new HttpResponseMessage(Status) { Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json") };
    }
}

public class VoiceApiTests(VoiceApiFactory voice) : IClassFixture<VoiceApiFactory>
{
    // Minimal MP4/M4A header ("....ftypM4A ") – the server judges recordings by their first bytes
    private static readonly byte[] M4a = [0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70, 0x4D, 0x34, 0x41, 0x20, 0x00, 0x00, 0x02, 0x00, 0x11, 0x22];

    private static MultipartFormDataContent Recording(byte[] bytes, string? language = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/mp4");
        form.Add(file, "file", "voice.m4a");
        if (language is not null) form.Add(new StringContent(language), "language");
        return form;
    }

    private async Task<HttpClient> UserAsync()
    {
        var client = voice.CreateClient();
        return client.WithToken((await client.RegisterAsync()).AccessToken);
    }

    // ---- Understanding a sentence --------------------------------------------------------------

    [Fact]
    public async Task Parse_turns_a_sentence_into_fields_without_needing_a_login()
    {
        var res = await voice.CreateClient().PostAsJsonAsync("/api/voice/parse",
            new { text = "میرے پاس دو ساہیوال گائے ہیں، دو دانت، 80 ہزار روپے فی جانور، لودھراں" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var r = (await res.Content.ReadFromJsonAsync<JsonElement>());
        Assert.Equal("sell", r.GetProperty("intent").GetString());
        Assert.Equal("cow", r.GetProperty("categorySlug").GetString());
        Assert.Equal(1, r.GetProperty("categoryId").GetInt32());
        Assert.Equal("livestock", r.GetProperty("section").GetString());
        Assert.Equal(2, r.GetProperty("quantity").GetDecimal());
        Assert.Equal("head", r.GetProperty("quantityUnit").GetString());
        Assert.Equal("two", r.GetProperty("teeth").GetString());
        Assert.Equal(80_000, r.GetProperty("price").GetInt64());
        Assert.Equal("perItem", r.GetProperty("priceUnit").GetString());
        Assert.Equal("female", r.GetProperty("gender").GetString());
        Assert.Equal(1, r.GetProperty("locationId").GetInt32());
        Assert.Equal("purpose", r.GetProperty("missing")[0].GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Parse_refuses_empty_text(string text)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await voice.CreateClient().PostAsJsonAsync("/api/voice/parse", new { text })).StatusCode);
    }

    [Fact]
    public async Task Parse_refuses_a_wall_of_text()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await voice.CreateClient().PostAsJsonAsync("/api/voice/parse", new { text = new string('ا', 801) })).StatusCode);
    }

    // ---- Recording to text ----------------------------------------------------------------------

    [Fact]
    public async Task Status_says_whether_recordings_can_be_turned_into_text()
    {
        var on = await voice.CreateClient().GetFromJsonAsync<JsonElement>("/api/voice/status");
        Assert.True(on.GetProperty("speechRecognition").GetBoolean());

        using var off = new ApiFactory(); // no key set
        var no = await off.CreateClient().GetFromJsonAsync<JsonElement>("/api/voice/status");
        Assert.False(no.GetProperty("speechRecognition").GetBoolean());
    }

    [Fact]
    public async Task Transcribe_sends_the_recording_to_the_speech_service_and_returns_its_text()
    {
        var client = await UserAsync();
        var res = await client.PostAsync("/api/voice/transcribe", Recording(M4a, "ur"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("گائے بیچنی ہے", (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("text").GetString());

        var sent = voice.Handler.Request!;
        Assert.Equal("https://speech.example.test/v1/audio/transcriptions", sent.RequestUri!.ToString());
        Assert.Equal("Bearer", sent.Headers.Authorization!.Scheme);
        Assert.Equal("test-key", sent.Headers.Authorization.Parameter);
        var body = voice.Handler.RequestBody!;
        Assert.Contains("name=model", body);
        Assert.Contains("whisper-1", body);
        Assert.Contains("name=language", body);
        Assert.Contains("name=file; filename=voice.m4a", body.Replace("\"", ""));
        Assert.Contains("name=prompt", body);
    }

    [Fact]
    public async Task Transcribe_needs_a_login()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await voice.CreateClient().PostAsync("/api/voice/transcribe", Recording(M4a))).StatusCode);
    }

    [Fact]
    public async Task Transcribe_refuses_files_that_are_not_recordings()
    {
        var client = await UserAsync();
        var before = voice.Handler.Calls;
        var res = await client.PostAsync("/api/voice/transcribe", Recording(Encoding.UTF8.GetBytes("this is not audio at all, just text")));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_audio", await res.ErrorCodeAsync());
        Assert.Equal(before, voice.Handler.Calls); // nothing was sent to the paid service
    }

    [Fact]
    public async Task Transcribe_refuses_recordings_that_are_too_big()
    {
        var client = await UserAsync();
        var big = new byte[VoiceService.MaxAudioBytes + 1];
        M4a.CopyTo(big, 0);
        var res = await client.PostAsync("/api/voice/transcribe", Recording(big));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_audio", await res.ErrorCodeAsync());
    }

    [Fact]
    public async Task A_failing_speech_service_gives_a_clear_error_not_a_crash()
    {
        var client = await UserAsync();
        voice.Handler.Status = HttpStatusCode.InternalServerError;
        voice.Handler.ResponseBody = "{\"error\":\"boom\"}";
        try
        {
            var res = await client.PostAsync("/api/voice/transcribe", Recording(M4a));
            Assert.Equal(HttpStatusCode.BadGateway, res.StatusCode);
            Assert.Equal("voice_service_failed", await res.ErrorCodeAsync());
        }
        finally
        {
            voice.Handler.Status = HttpStatusCode.OK;
            voice.Handler.ResponseBody = "{\"text\":\"گائے بیچنی ہے\"}";
        }
    }

    [Fact]
    public async Task Without_a_key_transcribe_says_it_is_not_set_up()
    {
        using var off = new ApiFactory();
        var client = off.CreateClient();
        client.WithToken((await client.RegisterAsync()).AccessToken);
        var res = await client.PostAsync("/api/voice/transcribe", Recording(M4a));
        Assert.Equal(HttpStatusCode.NotImplemented, res.StatusCode);
        Assert.Equal("voice_not_configured", await res.ErrorCodeAsync());
    }
}
