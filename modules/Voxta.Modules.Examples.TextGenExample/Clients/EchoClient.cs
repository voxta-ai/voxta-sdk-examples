using System.Runtime.CompilerServices;
using Voxta.Abstractions.Diagnostics;
using Voxta.Abstractions.Prompting;
using Voxta.Abstractions.Services.TextGen;
using Voxta.Abstractions.Tokenizers;
using Voxta.Model.Shared;
using Voxta.Modules.Examples.TextGenExample.Configuration;
using Voxta.Shared.LLMUtils.Clients;
using Voxta.Shared.LLMUtils.Tokenizers;

namespace Voxta.Modules.Examples.TextGenExample.Clients;

/// <summary>
/// The half of a text generation module you actually have to write: what your backend can do, how to
/// build its request body, and how to stream its answer back.
/// <para>
/// Everything above this — the prompt, the token budget, the thinking block, the tool loop, the
/// inference logs — belongs to <c>TextGenServiceBase</c>. See
/// <see cref="TextGen.EchoTextGenService"/>, which is all that is left of the service once this
/// class exists.
/// </para>
/// </summary>
public class EchoClient(IPromptFormatterFactory promptFormatterFactory)
    : LLMServiceClientBase(promptFormatterFactory), ILLMClient<EchoRequest>
{
    // ---- What this backend can do -------------------------------------------------------------
    // Every one of these is opted into rather than assumed, so a backend that quietly ignores a
    // feature is treated as unable to do it rather than trusted to have done it.

    /// <summary>Whether images can be sent along with the prompt.</summary>
    public override bool Multimodal => false;

    /// <summary>
    /// Whether the backend can hold the answer to a JSON schema. Claiming this when it cannot is
    /// worse than saying no: callers ask for JSON they then cannot parse.
    /// </summary>
    public override bool SupportsStructuredOutput => false;

    /// <summary>
    /// Whether the model reasons, and where the thought arrives.
    /// <see cref="ReasoningStyle.InOutputTokens"/> is a thought wrapped in tags inside the same
    /// stream; <see cref="ReasoningStyle.InResponseObjects"/> is a separate field, which the client
    /// yields with <c>Thinking = true</c> on the token.
    /// </summary>
    public override ReasoningStyle ReasoningStyle => ReasoningStyle.None;

    /// <summary>
    /// Whether instructions may be placed after the conversation rather than before it. Some
    /// backends allow it and it keeps the instructions close to the reply; most do not.
    /// </summary>
    public override bool SupportPostHistory => false;

    /// <summary>
    /// Whether this backend takes a tools list and answers with tool calls, so Voxta can run the
    /// loop for you. False for a backend with no tool support, and equally false for a harness that
    /// runs its own loop — in both cases there is nothing here for Voxta to drive.
    /// </summary>
    public bool SupportsNativeToolCalling => true;

    /// <summary>
    /// Whether a turn from earlier in the chat can carry the calls it made as structured data. When
    /// false, the exchange is written into the character's line as a sentence instead — which a
    /// model reads as prose to imitate rather than as a tool to call.
    /// </summary>
    public bool CarriesToolCallsInHistory => true;

    /// <summary>
    /// Whether <see cref="AppendToolInteraction"/> leaves the model mid-reply rather than at the top
    /// of a fresh turn. False here: this backend takes no prefill, so the base class asks the model
    /// in words to carry on instead of starting the paragraph over.
    /// </summary>
    public bool ContinuesTurnAfterTools => false;

    // ---- Setup ---------------------------------------------------------------------------------

    protected override async Task InitializeAsync(CancellationToken cancellationToken)
    {
        // Builds the tokenizer from CreateTokenizerAsync below. Always call this first.
        await InitializeLLMBaseDependenciesAsync();

        // Only a completion backend needs a prompt formatter, to render the conversation into one
        // string using the model's own chat template. A chat backend like this one sends the
        // messages as they are and leaves PromptFormatter null:
        //
        //     await CreatePromptFormatterAsync(defaultPromptFormattingTemplateId: null, model: null, cancellationToken);
    }

    /// <summary>
    /// Used to keep the prompt inside the context window. <see cref="AverageTokenizer"/> estimates
    /// from character counts, which is enough for a backend that counts tokens on its own side; ship
    /// the model's real tokenizer when the budget has to be exact.
    /// </summary>
    protected override Task<ITokenizer> CreateTokenizerAsync() =>
        Task.FromResult<ITokenizer>(AverageTokenizer.Instance);

    /// <summary>Called before the first message, so a local model can be loaded ahead of time.</summary>
    public Task WarmupAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // ---- The request ---------------------------------------------------------------------------

    /// <summary>
    /// Turns Voxta's request into your backend's request. The budget, the stop sequences and the
    /// tools list have all been resolved from the preset by this point, so read them off
    /// <paramref name="request"/> rather than going back to the settings.
    /// </summary>
    public ValueTask<EchoRequest> BuildRequestBodyAsync(
        TextGenGenerateRequest request,
        CancellationToken cancellationToken
    )
    {
        var body = new EchoRequest
        {
            Messages = [.. request.Messages],
            StopWords = request.StoppingStrings,
            Tools = request.Tools ?? [],
        };

        // A value of -1 means "no cap" — the preset says so, or an Assistant chat lets the model
        // stop on its own. Leave the field off the wire entirely rather than sending a negative.
        if (request.MaxNewTokens > 0) body.MaxLength = request.MaxNewTokens;

        return ValueTask.FromResult(body);
    }

    /// <summary>
    /// Puts what the model just said, the calls it made and their results back onto the body, so the
    /// next round continues the same turn. The client owns this because only it knows the wire shape
    /// of a tool exchange.
    /// </summary>
    /// <param name="text">
    /// The part that is easy to forget and impossible to miss once it is wrong: the user has already
    /// read "let me look that up", and a next round that cannot see it writes its own opening again
    /// into the same message.
    /// </param>
    public void AppendToolInteraction(
        EchoRequest body,
        TextGenGenerateRequest request,
        string? text,
        IReadOnlyList<LLMToolCallToken> calls,
        IReadOnlyList<LLMToolCallResultToken> results
    )
    {
        body.Messages.Add(new SimpleMessageData
        {
            Role = ChatMessageRole.Assistant,
            Value = text ?? "",
            ToolCalls =
            [
                .. calls.Select(call =>
                {
                    var result = results.FirstOrDefault(r => r.Id == call.Id);
                    return new SimpleMessageDataToolCall(
                        call.Id,
                        call.Name,
                        call.Arguments,
                        result?.Result ?? "",
                        result?.IsError ?? false
                    );
                }),
            ],
        });

        body.ToolsAlreadyCalled = true;
    }

    // ---- The answer ----------------------------------------------------------------------------

    /// <summary>
    /// Streams the reply. Yield <see cref="LLMOutputToken"/> for text and
    /// <see cref="LLMToolCallToken"/> when the model asks for a tool — the base class takes it from
    /// there: it asks the user for permission, runs the tool, yields the result, calls
    /// <see cref="AppendToolInteraction"/>, and comes back here for the next round.
    /// </summary>
    public async IAsyncEnumerable<LLMOutputToken> SendStreamingCompletionRequest(
        EchoRequest body,
        IInferenceLogger observer,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        // Attaching the body is what makes the prompt readable in the Inference Logs page.
        observer.Request = body;

        // A real backend calls a tool when the model asks for one, and stops when it has what it
        // needs. This one has no model, so it calls the first tool it was offered, once, to show
        // the shape of a round.
        if (body is { ToolsAlreadyCalled: false, Tools: [var tool, ..] })
        {
            yield return new LLMToolCallToken(
                Id: Guid.NewGuid().ToString("N"),
                Name: tool.Name,
                Summary: $"Trying {tool.Name}",
                // Whatever the tool's InputSchema asks for, as JSON. Empty here, since the example
                // cannot know what the offered tool wants.
                Arguments: "{}",
                // Set when a tool is called from inside another one, so a client can nest them.
                ParentId: null
            );
            yield break;
        }

        var delay = ModuleConfiguration.GetRequired(ModuleConfigurationProvider.TokenDelayMs);

        foreach (var word in BuildReply(body).Split(' '))
        {
            if (delay > 0) await Task.Delay(delay, cancellationToken);

            // Tokens are appended as-is, so keep whatever separators the text needs.
            var token = new LLMOutputToken(word + ' ');

            // Recording chunks on the observer is what shows the answer next to the prompt.
            observer.Chunks.Add(token.Text);
            yield return token;
        }
    }

    /// <summary>
    /// The non-streaming path, used for one-off requests such as a chat title. Same request body,
    /// one answer instead of a stream.
    /// </summary>
    public Task<string> SendCompletionRequest(
        EchoRequest body,
        IInferenceLogger observer,
        CancellationToken cancellationToken
    )
    {
        observer.Request = body;
        var reply = BuildReply(body);
        observer.Chunks.Add(reply);
        return Task.FromResult(reply);
    }

    private string BuildReply(EchoRequest body)
    {
        var lastUserMessage = body.Messages.LastOrDefault(m => m.Role == ChatMessageRole.User)?.Value;
        if (string.IsNullOrWhiteSpace(lastUserMessage))
            return "Hello! I only know how to repeat what you say.";

        var prefix = ServiceSettings.GetRequired(ServiceSettingsProvider.EchoPrefix);
        return $"{prefix} {lastUserMessage}".Trim();
    }
}
