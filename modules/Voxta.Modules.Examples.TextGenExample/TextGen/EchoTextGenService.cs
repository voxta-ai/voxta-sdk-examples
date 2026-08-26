using Microsoft.Extensions.Logging;
using Voxta.Abstractions.Diagnostics;
using Voxta.Modules.Examples.TextGenExample.Clients;
using Voxta.Shared.LLMUtils.Services.TextGen;

namespace Voxta.Modules.Examples.TextGenExample.TextGen;

/// <summary>
/// A text generation service, in full.
/// <para>
/// <c>TextGenServiceBase</c> implements <c>ITextGenService</c> on top of an
/// <c>ILLMClient&lt;TRequest&gt;</c>: it reads the token budget, the reply length and the text
/// processing options off the preset, asks the prompt builder for the right prompt for a reply, a
/// narration or a one-off question, opens the thinking block where the template has one, records
/// everything for the Inference Logs page, and runs the tool loop — permission prompts, results,
/// repeated-call detection and the round cap included.
/// </para>
/// <para>
/// What is left for you is <see cref="EchoClient"/>. Implement <c>ITextGenService</c> directly only
/// if your backend genuinely does not fit that shape.
/// </para>
/// <para>
/// Anything the base gets wrong for your backend is virtual. <c>SupportsToolCalling</c> and
/// <c>CanProcessAttachments()</c> are the usual ones to override, and <c>OnBeforeGenerateReply</c>
/// is the hook for per-turn state.
/// </para>
/// </summary>
public class EchoTextGenService(
    EchoClient client,
    IInferenceLoggersManager inferenceLoggersManager,
    ILogger<EchoTextGenService> logger
) : TextGenServiceBase<EchoClient, EchoRequest>(client, inferenceLoggersManager, logger);
