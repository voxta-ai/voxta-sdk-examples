using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Voxta.Abstractions.Chats.Objects.Characters;
using Voxta.Abstractions.Chats.Objects.Chats;
using Voxta.Abstractions.Diagnostics;
using Voxta.Abstractions.Prompting;
using Voxta.Abstractions.Services;
using Voxta.Abstractions.Services.TextGen;
using Voxta.Abstractions.TextGenerationStreaming;
using Voxta.Abstractions.Tokenizers;
using Voxta.Model.Shared;
using Voxta.Modules.Examples.TextGenExample.Configuration;

namespace Voxta.Modules.Examples.TextGenExample.TextGen;

/// <summary>
/// A text generation service that does not generate anything: it echoes back the last user
/// message, one word at a time. Everything a real service does is here except the call to a
/// model, which happens in <see cref="GenerateStreamingAsync"/>.
///
/// Voxta never asks for a reply as plain text. It hands over the chat, the character, and a
/// <see cref="IPromptBuilder"/>, and expects a stream of <see cref="LLMOutputToken"/> back. The
/// prompt builder is what turns the chat into the messages your backend will see: it applies the
/// character card, the scenario, memories, summaries, and the token budget you declare below.
/// That is why the three Generate* methods all look the same — build the request, stream it.
///
/// Inherit <see cref="ServiceBase"/> to get <c>ModuleConfiguration</c>, <c>ServiceSettings</c>,
/// <c>Auth</c> and <c>InstanceSettings</c>. One instance is created per user session, so
/// per-session state is safe to hold in fields.
/// </summary>
public class EchoTextGenService(
    IInferenceLoggersManager inferenceLoggersManager,
    ILogger<EchoTextGenService> logger
) : ServiceBase(logger), ITextGenService
{
    /// <summary>
    /// Used by Voxta to fit the chat history into the prompt. A real module returns the
    /// tokenizer matching the loaded model; the null one counts zero, which is fine here
    /// because nothing is ever truncated.
    /// </summary>
    public ITokenizer Tokenizer => NullTokenizer.Instance;

    /// <summary>Whether your backend can enforce a JSON schema on the answer.</summary>
    public bool SupportsStructuredOutput => false;

    // These come from the preset the user selected (ServiceSettings), not from the module
    // configuration, so they are read on every access rather than cached: editing a preset updates
    // a live session in place, and a cached value would keep the old one until the next restart.

    /// <summary>Size of the context window, i.e. how much prompt the model can be given.</summary>
    public int MaxWindowTokens => ServiceSettings.GetRequired(ServiceSettingsProvider.MaxContextTokens);

    /// <summary>How many tokens a single reply may use.</summary>
    public int MaxTokens => ServiceSettings.GetRequired(ServiceSettingsProvider.MaxNewTokens);

    /// <summary>Share of the window memories are allowed to take.</summary>
    public int MaxMemoryTokens => MaxWindowTokens / 10;

    /// <summary>
    /// Post-processing applied to the generated text. <see cref="TextProcessingOptions.Roleplay"/>
    /// splits *actions* from spoken text so only speech is sent to text to speech.
    /// </summary>
    public TextProcessingOptions TextProcessing => ServiceSettings.GetRequired(ServiceSettingsProvider.TextProcessing);

    /// <summary>
    /// True only if the prompt asked the model to mark tone with angle brackets, so that those
    /// markers are read as markers rather than spoken aloud. Requires a matching prompt.
    /// </summary>
    public bool ProsodyTags => false;

    // How this service wants to receive the system prompt, plus optional additions to it.
    // Real modules usually expose these as preset settings so users can tune them per model.
    public SystemPromptOverrideTypes SystemPromptOverrideType => SystemPromptOverrideTypes.AddOn;
    public string? SystemPromptAddon => null;
    public string? ContextPromptAddon => null;
    public string? ReplyPrefix => null;

    /// <summary>Limits applied to the generated text before it reaches the chat.</summary>
    public TextGenPreprocessingSettings CreateTextGenPreprocessingSettings(int maxSentences = 0, bool? allowMultipleLines = null) => new()
    {
        MaxSentences = maxSentences,
        MaxWordsInAsterisks = 0,
        AllowMultipleLines = allowMultipleLines ?? false,
        TextProcessing = TextProcessing,
        ProsodyTags = ProsodyTags,
    };

    /// <summary>Whether images can be included in the prompt (i.e. is the model multimodal).</summary>
    public bool CanProcessAttachments() => false;

    /// <summary>
    /// Called before the first message so a local model can be loaded ahead of time. Nothing to
    /// warm up here.
    /// </summary>
    public Task WarmupAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// The budget and formatting the prompt builder must respect. Real modules derive these from
    /// the user's preset; the numbers are hardcoded here to keep the example short.
    /// </summary>
    public ValueTask<GenerateReplyConstraints> GetConstraintsAsync(
        IChatInferenceData chat,
        ICharacterInferenceData character,
        GenerateConstraintRequest constraintRequest,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(new GenerateReplyConstraints
        {
            MaxInputTokens = MaxWindowTokens - MaxTokens,
            MaxNewTokens = MaxTokens,
            MaxMemoryTokensRatio = (double)MaxMemoryTokens / MaxWindowTokens,
            TextProcessing = TextProcessing,
            Multimodal = CanProcessAttachments(),
        });
    }

    /// <summary>The character's next line in a normal conversation.</summary>
    public async IAsyncEnumerable<LLMOutputToken> GenerateReplyAsync(
        IChatInferenceData chat,
        ICharacterInferenceData character,
        IPromptBuilder promptBuilder,
        string? prefix,
        GenerateConstraintRequest constraintRequest,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        // Recording the inference makes the prompt and the answer visible in the server's
        // Inference Logs page, which is the first place to look when a module misbehaves.
        using var observer = inferenceLoggersManager.Record(ServiceType, InstanceSettings.ServiceName, InferenceLogActionNames.TextGen);

        var constraints = await GetConstraintsAsync(chat, character, constraintRequest, cancellationToken);
        var request = await promptBuilder.CreateReplyRequest(chat, character, constraints, prefix, cancellationToken);

        await foreach (var token in GenerateStreamingAsync(observer, request, cancellationToken))
        {
            yield return token;
        }

        observer.Done();
    }

    /// <summary>Narration of an event, written in the third person rather than spoken.</summary>
    public async IAsyncEnumerable<LLMOutputToken> GenerateStoryAsync(
        IChatInferenceData chat,
        string eventDescription,
        IPromptBuilder promptBuilder,
        string? prefix,
        GenerateConstraintRequest constraintRequest,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        using var observer = inferenceLoggersManager.Record(ServiceType, InstanceSettings.ServiceName, InferenceLogActionNames.StoryGen);

        var constraints = await GetConstraintsAsync(chat, chat.GetMainCharacter(), constraintRequest, cancellationToken);
        var request = await promptBuilder.CreateStoryWriterRequest(chat, eventDescription, constraints, prefix, cancellationToken);

        await foreach (var token in GenerateStreamingAsync(observer, request, cancellationToken))
        {
            yield return token;
        }

        observer.Done();
    }

    /// <summary>
    /// A one-off question asked about the chat, used by scripts and augmentations. The answer
    /// goes to the caller, not to the chat.
    /// </summary>
    public async IAsyncEnumerable<LLMOutputToken> GenerateInferAsync(
        IChatInferenceData chat,
        string text,
        IPromptBuilder promptBuilder,
        GenerateConstraintRequest constraintRequest,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        using var observer = inferenceLoggersManager.Record(ServiceType, InstanceSettings.ServiceName, InferenceLogActionNames.TextGen);

        var constraints = await GetConstraintsAsync(chat, chat.GetMainCharacter(), constraintRequest, cancellationToken);
        var request = await promptBuilder.CreateInferRequest(chat, text, constraints, constraintRequest.IncludeChatHistory, cancellationToken);

        await foreach (var token in GenerateStreamingAsync(observer, request, cancellationToken))
        {
            yield return token;
        }

        observer.Done();
    }

    /// <summary>
    /// Where a real module would call its backend: send <paramref name="request"/>.Messages to
    /// an API or a local model, and yield each token as it arrives. Streaming matters — Voxta
    /// starts speaking the first sentence while the rest is still being written.
    /// </summary>
    public async IAsyncEnumerable<LLMOutputToken> GenerateStreamingAsync(
        InferenceLogger observer,
        TextGenGenerateRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        // Attaching the request is what makes the prompt readable in the Inference Logs page.
        observer.Request = new RawDisplayable(string.Join("\n\n", request.Messages.Select(m => $"### {m.Role}\n{m.Value}")));

        var delay = ModuleConfiguration.GetRequired(ModuleConfigurationProvider.TokenDelayMs);

        foreach (var word in BuildReply(request).Split(' '))
        {
            if (delay > 0) await Task.Delay(delay, cancellationToken);

            // Tokens are appended as-is, so keep the separators you want in the text.
            var token = new LLMOutputToken(word + ' ');

            // Recording tokens on the observer is what shows the answer next to the prompt.
            observer.AddToken(token);
            yield return token;
        }
    }

    /// <summary>
    /// Non-streaming variant, used where the whole answer is needed at once (summarization,
    /// action inference, scripts). Real modules usually call their backend without streaming
    /// here rather than reassembling tokens.
    /// </summary>
    public async ValueTask<string> GenerateAsync(
        TextGenGenerateRequest request,
        InferenceLogger observer,
        CancellationToken cancellationToken = default
    )
    {
        var chunks = new List<string>();
        await foreach (var token in GenerateStreamingAsync(observer, request, cancellationToken))
        {
            chunks.Add(token.Text);
        }
        return string.Concat(chunks).Trim();
    }

    /// <summary>
    /// Stands in for the model. The prompt builder has already turned the chat into messages,
    /// so the last user message is the closest thing to "what was just said".
    /// </summary>
    private string BuildReply(TextGenGenerateRequest request)
    {
        var lastUserMessage = request.Messages.LastOrDefault(m => m.Role == ChatMessageRole.User)?.Value;

        if (string.IsNullOrWhiteSpace(lastUserMessage))
        {
            logger.LogDebug("No user message in the prompt, falling back to a greeting");
            return "Hello! I only know how to repeat what you say.";
        }

        var prefix = ServiceSettings.GetRequired(ServiceSettingsProvider.ReplyPrefix);
        return $"{prefix} {lastUserMessage}".Trim();
    }
}
