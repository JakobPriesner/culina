using System.Text.Json;
using Infrastructure.Assistance;

namespace IntegrationTests.Assistance;

/// <summary>
/// That what a provider actually sends reaches the properties that read it.
/// </summary>
/// <remarks>
/// <para>
/// In the integration project because these records are internal to
/// Infrastructure, which is the assembly this one can see. Nothing here needs a
/// database; it is here for visibility rather than for a fixture.
/// </para>
/// <para>
/// Worth a test of its own because this is the failure with no symptom. The
/// shared options are <c>JsonSerializerDefaults.Web</c>, which bridge case and
/// nothing else — so a field written <c>b64_json</c> lands on nothing, silently,
/// and the property keeps its default. A picture becomes null and reads as an
/// answer that could not be used; a token count becomes zero and reads as a
/// call that cost nothing. Neither throws, and neither is visible until
/// somebody wonders why a month of assistance was free.
/// </para>
/// <para>
/// The payloads below are trimmed copies of what the providers document, with
/// the field names left exactly as they write them.
/// </para>
/// </remarks>
public class ProviderWireFormatTests
{
    [Fact]
    public void OpenAiImage_ShouldCarryThePicture_WrittenAsTheProviderWritesIt()
    {
        // Arrange
        const string body = """
            {
              "created": 1698116662,
              "data": [{ "b64_json": "aGVsbG8=" }],
              "usage": { "input_tokens": 12, "output_tokens": 34, "total_tokens": 46 }
            }
            """;

        // Act
        var reply = JsonSerializer.Deserialize<OpenAiImageReply>(body, AssistantHttp.Json);

        // Assert
        // The whole of a drawing is this one field. Unbound, every picture
        // OpenAI ever returned was thrown away as unreadable.
        Assert.Equal("aGVsbG8=", reply?.Data?[0].B64Json);
    }

    [Fact]
    public void OpenAiUsage_ShouldCountTokens_SoTheBudgetIsSpent()
    {
        // Arrange
        const string body = """
            {
              "output": [{ "type": "message", "content": [{ "type": "output_text", "text": "{}" }] }],
              "usage": { "input_tokens": 120, "output_tokens": 340, "total_tokens": 460 }
            }
            """;

        // Act
        var reply = JsonSerializer.Deserialize<OpenAiReply>(body, AssistantHttp.Json);

        // Assert
        Assert.Equal(120, reply?.Usage?.InputTokens);
        Assert.Equal(340, reply?.Usage?.OutputTokens);
    }

    [Fact]
    public void OpenAiModels_ShouldReadTheCatalogue()
    {
        // Arrange
        const string body = """
            {
              "object": "list",
              "data": [
                { "id": "gpt-image-1", "object": "model" },
                { "id": "gpt-5", "object": "model" }
              ]
            }
            """;

        // Act
        var reply = JsonSerializer.Deserialize<OpenAiModelList>(body, AssistantHttp.Json);

        // Assert
        Assert.Equal(["gpt-image-1", "gpt-5"], reply?.Data?.Select(model => model.Id));
    }

    [Fact]
    public void OllamaReply_ShouldCountTokensAndCarryItsReason()
    {
        // Arrange
        const string body = """
            {
              "message": { "role": "assistant", "content": "hello" },
              "done": true,
              "done_reason": "stop",
              "prompt_eval_count": 26,
              "eval_count": 298
            }
            """;

        // Act
        var reply = JsonSerializer.Deserialize<OllamaReply>(body, AssistantHttp.Json);

        // Assert
        Assert.Equal("stop", reply?.DoneReason);
        Assert.Equal(26, reply?.PromptEvalCount);
        Assert.Equal(298, reply?.EvalCount);
    }

    [Fact]
    public void GeminiUsage_ShouldCountAWholeTurn()
    {
        // Arrange
        // The Interactions API accounts for the turn rather than the message,
        // and writes the fields in snake_case like the other two.
        const string body = """
            {
              "status": "completed",
              "steps": [
                { "type": "model_output", "content": [{ "type": "text", "text": "{}" }] }
              ],
              "usage": {
                "total_input_tokens": 940,
                "total_output_tokens": 210,
                "total_thought_tokens": 64,
                "total_tokens": 1214
              }
            }
            """;

        // Act
        var reply = JsonSerializer.Deserialize<GeminiReply>(body, AssistantHttp.Json);

        // Assert
        // Zero here is what every row in a real instance's ledger showed, on
        // calls that had plainly done work.
        Assert.Equal(940, reply?.Usage?.InputTokens);
        Assert.Equal(210, reply?.Usage?.OutputTokens);
    }

    [Fact]
    public void GeminiModels_ShouldReadNameAndDisplayName()
    {
        // Arrange
        // Google writes camelCase, which the web defaults do bridge — this is
        // here so that the one provider needing no names is on the record as
        // having been checked rather than assumed.
        const string body = """
            {
              "models": [
                {
                  "name": "models/gemini-3-pro-image-preview",
                  "displayName": "Nano Banana Pro",
                  "supportedGenerationMethods": ["generateContent"]
                }
              ]
            }
            """;

        // Act
        var reply = JsonSerializer.Deserialize<GeminiModelList>(body, AssistantHttp.Json);

        // Assert
        Assert.Equal("models/gemini-3-pro-image-preview", reply?.Models?[0].Name);
        Assert.Equal("Nano Banana Pro", reply?.Models?[0].DisplayName);
        Assert.Equal(["generateContent"], reply?.Models?[0].SupportedGenerationMethods);
    }
}
