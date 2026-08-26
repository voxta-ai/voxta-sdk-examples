using Microsoft.Extensions.Logging;
using Voxta.Abstractions.Registration;
using Voxta.Abstractions.Repositories;
using Voxta.Abstractions.Services;
using Voxta.Abstractions.Services.Agents;
using Voxta.Abstractions.Services.ChatAugmentations;
using Voxta.Abstractions.Utils;
using Voxta.Model.Shared;
using Voxta.Modules.Examples.ToolsExample.Configuration;

namespace Voxta.Modules.Examples.ToolsExample.ChatAugmentations;

/// <summary>
/// A module that serves tools implements <see cref="IToolsChatAugmentationsService"/> rather than the
/// plain <see cref="IChatAugmentationsService"/>. It declares no members — it is a marker — and it is
/// said on the service so that whoever wants tools and nothing else can skip every other module rather
/// than construct it, fail, and report a module that was never going to offer any.
///
/// The two-step shape is the same as any other augmentation: <see cref="PlanAsync"/> names what this
/// module would contribute and must stay cheap, because it runs for a chat the user is only looking at.
/// The real work waits in <c>ActivateAsync</c>.
/// </summary>
public class ChatAugmentationsService(
    ILogger<ChatAugmentationsService> logger,
    ILoggerFactory loggerFactory,
    IUnitOfWorkFactory unitOfWorkFactory,
    ICommonFolders folders
) : ServiceBase(logger), IToolsChatAugmentationsService
{
    /// <summary>
    /// Augmentations are declared in two steps.
    ///
    /// <see cref="PlanAsync"/> lists augmentations that will be enabled for a given chat. Keep this light,
    /// this runs before the chat is started to allow users to opt out of them.
    ///
    /// Then, <c>create</c> is invoked to instantiate the augmentation and attach it to the chat.
    /// </summary>
    public Task<AugmentationPlanItem[]> PlanAsync(
        IChatAugmentationPlanContext context,
        CancellationToken cancellationToken
    )
    {
        var chatStyles = ChatToolSettingsContributions.GetChatStyles(ModuleConfiguration);
        // Name, label and description come off the module's own table rather than being written again
        // here: the picker's catalog is built from that table, and a plan that spelled them out itself
        // would be a second copy to keep in step.
        var augmentation = ToolsExampleConstants.Augmentations.Get(ToolsExampleConstants.AugmentationKey);

        return Task.FromResult<AugmentationPlanItem[]>([
            new AugmentationPlanItem
            {
                Key = augmentation.Key,
                Label = augmentation.Label,
                Description = augmentation.Description,

                // Naming the tools up front is what lets the server warn that two modules both offer
                // roll_dice before the chat starts, rather than after one of them silently lost.
                Tools =
                [
                    new AugmentationToolDeclaration(ToolsExampleConstants.RollDiceToolName, ToolsExampleConstants.ContextKey),
                    new AugmentationToolDeclaration(ToolsExampleConstants.WriteNoteToolName, ToolsExampleConstants.ContextKey),
                ],

                // OptIn: it runs only where somebody asked for the key. Automatic is for augmentations
                // that belong to the setup rather than to the character — reply prefixing, the speech
                // replacements a voice needs — and a tool is never one of those.
                Activation = AugmentationActivation.OptIn,

                // Offered up front in the chat this is actually for, and nowhere else. Suggesting
                // everything is the same as suggesting nothing.
                Suggested = context.ChatStyle == ChatStyles.Assistant,

                // A character asking for every augmentation ("*") does not get tools. A catch-all is a
                // convenience, not consent to let the model write to disk.
                IncludeInCatchAll = false,

                ActivateAsync = (session, _) =>
                {
                    var notesFolder = Path.Combine(folders.GetDataFolder(), ModuleConfiguration.GetRequired(ModuleConfigurationProvider.NotesFolder));
                    Directory.CreateDirectory(notesFolder);
                    return Task.FromResult<IChatAugmentationServiceInstanceBase?>(
                        new ToolsExampleChatAugmentationInstance(
                            session,
                            notesFolder,
                            chatStyles,
                            ChatToolSettingsContributions.IsToolSelected(ModuleConfiguration,
                                ModuleConfigurationProvider.Tools, ToolsExampleConstants.RollDiceToolName),
                            ChatToolSettingsContributions.IsToolSelected(ModuleConfiguration,
                                ModuleConfigurationProvider.Tools, ToolsExampleConstants.WriteNoteToolName),
                            AlwaysAllowed(),
                            new ToolsExampleGrantWriter(
                                session.Auth.UserId,
                                InstanceSettings.ModuleId,
                                unitOfWorkFactory,
                                loggerFactory.CreateLogger<ToolsExampleGrantWriter>()
                            ),
                            loggerFactory.CreateLogger<ToolsExampleChatAugmentationInstance>()
                        )
                    );
                },
            },
        ]);
    }

    /// <summary>The tools the user has already said "yes, always" to, as stored by the grant writer.</summary>
    private HashSet<string> AlwaysAllowed() =>
    [
        .. ModuleConfiguration.GetRequired(ModuleConfigurationProvider.AlwaysAllow)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
    ];
}
