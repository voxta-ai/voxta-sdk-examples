using Voxta.Abstractions.Diagnostics;
using Voxta.Abstractions.Services.Agents;
using Voxta.Model.Shared;
using Voxta.Shared.LLMUtils.Utils;

namespace Voxta.Modules.Examples.TextGenExample.Clients;

/// <summary>
/// The request body exactly as your backend wants to receive it. This one is shaped like a chat
/// completions API — a list of messages — because that is what most backends take; a completion
/// backend would carry a single rendered prompt string instead.
/// <para>
/// It implements <see cref="IDisplayable"/> so the Inference Logs page can show what was sent:
/// <see cref="GetMessages"/> is rendered as the conversation, and <see cref="GetDisplayDict"/> as
/// the parameter table beside it.
/// </para>
/// </summary>
public class EchoRequest : IDisplayable
{
    /// <summary>
    /// Mutable, because <see cref="EchoClient.AppendToolInteraction"/> adds to it between tool
    /// rounds: the same body is sent again with the exchange appended.
    /// </summary>
    public required List<SimpleMessageData> Messages { get; init; }

    public string[]? StopWords { get; init; }

    public int MaxLength { get; set; }

    /// <summary>What the model may call. Empty when the chat offered nothing.</summary>
    public IReadOnlyList<ChatTool> Tools { get; init; } = [];

    /// <summary>
    /// Set once a tool round has been appended. A real backend does not need this — the model
    /// decides when it is done calling tools. This example has no model, so it needs a way to
    /// answer on the second pass instead of calling the same tool forever.
    /// </summary>
    public bool ToolsAlreadyCalled { get; set; }

    public SimpleMessageData[] GetMessages() => [.. Messages];

    /// <summary>
    /// Reflects over the properties and formats the scalar ones. Skip whatever is already shown as
    /// the conversation, and anything that is internal bookkeeping rather than part of the request.
    /// </summary>
    public Dictionary<string, string> GetDisplayDict() =>
        GenerationRequestDisplayStringUtil.ToDisplayDictionary(this, nameof(Messages), nameof(Tools), nameof(ToolsAlreadyCalled));
}
