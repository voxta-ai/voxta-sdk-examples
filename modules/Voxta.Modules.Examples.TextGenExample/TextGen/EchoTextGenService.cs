using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Voxta.Abstractions.Chats.Objects.Characters;
using Voxta.Abstractions.Chats.Objects.Chats;
using Voxta.Abstractions.Diagnostics;
using Voxta.Abstractions.Prompting;
using Voxta.Abstractions.Services;
using Voxta.Abstractions.Services.Agents;
using Voxta.Abstractions.Services.TextGen;
using Voxta.Abstractions.TextGenerationStreaming;
using Voxta.Abstractions.Tokenizers;
using Voxta.Model.Shared;
using Voxta.Modules.Examples.TextGenExample.Configuration;

namespace Voxta.Modules.Examples.TextGenExample.TextGen;

/// <summary>
/// Text generation services are responsible for generating text and handling tool calls.
/// </summary>
public class EchoTextGenService(
    IInferenceLoggersManager inferenceLoggersManager,
    ILogger<EchoTextGenService> logger
) : ServiceBase(logger), ITextGenService
{
    /// <summary>
    /// Used to count tokens and keep messages under the model's context window.
    /// </summary>
    public ITokenizer Tokenizer => NullTokenizer.Instance;

    /// <summary>Whether your backend can enforce a JSON schema on the answer.</summary>
    public bool SupportsStructuredOutput => false;

    /// <summary>
    /// Whether the text generation service supports thinking tokens.
    ///
    /// <see cref="ReasoningStyle.InOutputTokens"/>: thinking arrives inside the same
    /// stream as the answer, wrapped in the tags given by <c>Tags</c> (for example
    /// <c>new ThinkingFormat("&lt;think&gt;", "&lt;/think&gt;")</c>).
    /// <see cref="ReasoningStyle.InResponseObjects"/>: returned as a separate
    /// field, and the service marks those tokens with <c>Thinking = true</c> instead.
    /// </summary>
    public ReasoningInfo Reasoning => ReasoningInfo.None;

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
    /// Whether this LLM is capable enough to handle prosody tags, e.g. &lt;loud&gt;.
    /// </summary>
    public bool ProsodyTags => false;

    /// <summary>
    /// Whether this service can run tools.
    /// </summary>
    public bool SupportsToolCalling => true;

    /// <summary>
    /// Whether the LLM drives the tool loop for this service, e.g. Claude Code.
    /// </summary>
    public bool SupportsNativeToolCalling => false;

    // You can allow users to provide a custom system prompt or a custom system prompt addon.
    public SystemPromptOverrideTypes SystemPromptOverrideType => SystemPromptOverrideTypes.AddOn;
    public string? SystemPromptAddon => null;
    public string? ContextPromptAddon => null;
    
    // You can allow users to provide forcing a reply prefix.
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
    /// does not ask again.
    /// </summary>
    private bool _clockToolAllowedForSession;

    /// <summary>Whether images can be included in the prompt (i.e. is the model multimodal).</summary>
    public bool CanProcessAttachments() => false;

    /// <summary>
    /// Called before the first message so a local model can be loaded ahead of time.
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
    /// A reply in a conversation, including tool calling.
    /// </summary>
    public async IAsyncEnumerable<LLMOutputToken> GenerateReplyAsync(
        IChatInferenceData chat,
        ICharacterInferenceData character,
        IPromptBuilder promptBuilder,
        string? prefix,
        GenerateConstraintRequest constraintRequest,
        IAgentToolsContext? agentTools,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        // Records this request for the Inference Logs page.
        using var observer = inferenceLoggersManager.Record(ServiceType, InstanceSettings.ServiceName, InferenceLogActionNames.TextGen);

        var constraints = await GetConstraintsAsync(chat, character, constraintRequest, cancellationToken);
        var request = await promptBuilder.CreateReplyRequest(chat, character, constraints, prefix, cancellationToken)
            with
            {
                AgentTools = agentTools
            };

        await foreach (var token in GenerateStreamingAsync(observer, request, cancellationToken))
        {
            yield return token;
        }

        observer.Done();
    }

    /// <summary>Narration of an event.</summary>
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
    /// A one-off question asked about the chat, used by scripts and augmentations.
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
    /// The workhorse of text generation services, this is what generates text and call tools.
    /// </summary>
    public async IAsyncEnumerable<LLMOutputToken> GenerateStreamingAsync(
        InferenceLogger observer,
        TextGenGenerateRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        // Attaching the request is what makes the prompt readable in the Inference Logs page.
        observer.Request = new RawDisplayable(string.Join("\n\n", request.Messages.Select(m => $"### {m.Role}\n{m.Value}")));

        if (request.AgentTools != null)
        {
            await foreach (var token in CallClockToolAsync(request.AgentTools.Prompter, cancellationToken))
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
    /// Example tool calling
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
    /// Non-streaming variant.
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
