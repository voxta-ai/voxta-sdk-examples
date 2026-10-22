using Voxta.Abstractions.Registration;
using Voxta.Abstractions.Repositories;
using Voxta.Abstractions.Security;
using Voxta.Model.Shared.Forms;

namespace Voxta.Modules.Examples.GameExample.Configuration;

public class ModuleConfigurationProvider(IUnitOfWorkFactory unitOfWorkFactory)
    : ModuleConfigurationProviderBase, IModuleConfigurationProvider
{
    public static readonly FormChoicesField CharacterId = new()
    {
        Name = "CharacterId",
        Label = "Character",
        Text = "Who answers when the game does not name a character.",
    };

    public static readonly FormPathField GamePath = new()
    {
        Name = "GamePath",
        Label = "Game",
        Kind = FormPathKind.File,
        Required = false,
        // Empty is not missing: the stores find the game.
        Placeholder = "Auto",
        Text = "Found on Steam, GOG or Epic. Pick the game's executable only for a copy installed some other way.",
    };

    public async Task<FormField[]> GetModuleConfigurationFieldsAsync(
        IAuthenticationContext auth,
        ISettingsSource settings,
        CancellationToken cancellationToken
    )
    {
        var characters = await unitOfWorkFactory.ReadAsync(
            auth.UserId,
            x => x.Characters.GetItemsByUserAsync(GetCharactersQueryParameters.All, cancellationToken)
        );

        return FormBuilder.Build(
            FormTitleField.Create("Examples: Game Integration"),
            // The published id: it is what ClientStartChatMessage.CharacterId takes.
            CharacterId.WithChoices([.. characters.Select(c => new FormChoice { Value = c.PublishedId.ToString(), Label = c.Name })]),
            GamePath
        );
    }
}
