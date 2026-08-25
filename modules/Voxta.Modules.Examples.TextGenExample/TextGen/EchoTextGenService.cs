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

    /// <summary>
    /// Whether the model thinks before answering, and how that thinking is delimited. Voxta needs
    /// this to keep the thinking out of the reply: the character should not speak it, and it should
    /// not be stored in the chat history.
    ///
    /// <see cref="ReasoningStyle.InOutputTokens"/> means the thinking arrives inside the same
    /// stream as the answer, wrapped in the tags given by <c>Tags</c> (for example
    /// <c>new ThinkingFormat("&lt;think&gt;", "&lt;/think&gt;")</c>).
    /// <see cref="ReasoningStyle.InResponseObjects"/> means the backend returns it as a separate
    /// field, and the service marks those tokens with <c>Thinking = true</c> instead.
    ///
    /// This service does not reason, so it declares <see cref="ReasoningInfo.None"/>.
    /// </summary>
    public ReasoningInfo Reasoning => ReasoningInfo.None;

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

    /// <summary>
    /// Set once the user picked "allow for the session" on the simulated tool, so the next reply
    /// does not ask again. Safe as a field because there is one service instance per session.
    /// </summary>
    private bool _clockToolAllowedForSession;

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

    /// <summary>
    /// The character's next line in a normal conversation.
    ///
    /// <paramref name="toolPermissionPrompter"/> is how a tool call asks the user for consent
    /// before it runs. Voxta passes it in only for replies — a summarization or an action inference
    /// has nobody to ask — and it is null when the chat runs unattended. Pass it along on the
    /// request so the code that actually calls tools can reach it.
    /// </summary>
    public async IAsyncEnumerable<LLMOutputToken> GenerateReplyAsync(
        IChatInferenceData chat,
        ICharacterInferenceData character,
        IPromptBuilder promptBuilder,
        string? prefix,
        GenerateConstraintRequest constraintRequest,
        IToolPermissionPrompter? toolPermissionPrompter,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        // Recording the inference makes the prompt and the answer visible in the server's
        // Inference Logs page, which is the first place to look when a module misbehaves.
        using var observer = inferenceLoggersManager.Record(ServiceType, InstanceSettings.ServiceName, InferenceLogActionNames.TextGen);

        var constraints = await GetConstraintsAsync(chat, character, constraintRequest, cancellationToken);
        var request = (await promptBuilder.CreateReplyRequest(chat, character, constraints, prefix, cancellationToken))
            with { ToolPermissionPrompter = toolPermissionPrompter };

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

        // A reply can carry tool calls as well as text. They travel in the same stream, as tokens
        // of a different type, so a client can show a call while it is still running.
        if (request.ToolPermissionPrompter != null && ServiceSettings.GetRequired(ServiceSettingsProvider.SimulateToolCall))
        {
            await foreach (var token in CallClockToolAsync(request.ToolPermissionPrompter, cancellationToken))
            {
                yield return token;
            }
        }

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
    /// Stands in for a tool the backend would expose. It shows the three pieces a real tool call
    /// needs: ask the user, announce the call, then report its result.
    ///
    /// The same tool use id ties all three together. Voxta forwards the call and the result to the
    /// client as they arrive, and stores them on the message once the reply is done.
    /// </summary>
    private async IAsyncEnumerable<LLMOutputToken> CallClockToolAsync(
        IToolPermissionPrompter prompter,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        const string toolName = "get_current_time";
        var toolUseId = Guid.NewGuid().ToString("N");

        // This waits for the user to answer in their client, so it can take a while.
        var decision = _clockToolAllowedForSession
            ? ToolPermissionDecision.Allow
            : await prompter.RequestAsync(new ToolPermissionRequest
            {
                ToolUseId = toolUseId,
                ToolName = toolName,
                DisplayName = "Check the time",
                Description = "Reads the clock of the machine running the server.",
            }, cancellationToken);

        // "Allow for the session" is the service's job to remember, not Voxta's.
        if (decision == ToolPermissionDecision.AllowSession)
            _clockToolAllowedForSession = true;

        yield return new LLMToolCallToken(
            Id: toolUseId,
            Name: toolName,
            Summary: "Checking the time",
            Arguments: "{}",
            // Set when a tool is called from within another one, so a client can nest them.
            ParentId: null
        );

        // A denied call still needs a result: the model has to be told why nothing came back.
        yield return decision == ToolPermissionDecision.Deny
            ? new LLMToolCallResultToken(toolUseId, toolName, "The user refused this call.", IsError: true, ParentId: null)
            : new LLMToolCallResultToken(toolUseId, toolName, $"It is {DateTime.Now:HH:mm}.", IsError: false, ParentId: null);
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
