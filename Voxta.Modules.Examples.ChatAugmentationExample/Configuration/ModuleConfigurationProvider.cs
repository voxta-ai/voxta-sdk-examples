using System.Diagnostics.CodeAnalysis;
using Voxta.Abstractions.Registration;
using Voxta.Abstractions.Security;
using Voxta.Model.Shared.Forms;

namespace Voxta.Modules.Examples.ChatAugmentationExample.Configuration;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "Normalize fields visibility for consistency")]
[SuppressMessage("ReSharper", "RedundantArgumentDefaultValue", Justification = "Service default values may match global default values")]
public class ModuleConfigurationProvider : ModuleConfigurationProviderBase, IModuleConfigurationProvider
{
    public static string[] FieldsRequiringReload => [];

    public static readonly FormBooleanField SampleActionInference = new()
    {
        Name = "SampleActionInference",
        Label = "Action Inference Example Enabled",
        Text = "Enable the sample action inference augmentation that can provide date and time information.",
        DefaultValue = true,
    };
    
    public static readonly FormBooleanField SampleScriptEvent = new()
    {
        Name = "SampleScriptEvent",
        Label = "Script Event Example Enabled",
        Text = "Enable the sample script event augmentation that logs script events to the server log.",
        DefaultValue = true,
    };
    
    public static readonly FormBooleanField SamplePreprocessor = new()
    {
        Name = "SamplePreprocessor",
        Label = "Preprocessor Example Enabled",
        Text = "Enable the sample preprocessor augmentation that adds a note to each user message.",
        DefaultValue = true,
    };
    
    public Task<FormField[]> GetModuleConfigurationFieldsAsync(
        IAuthenticationContext auth,
        ISettingsSource settings,
        CancellationToken cancellationToken
    )
    {
        var fields = FormBuilder.Build(
            FormTitleField.Create("Chat Augmentations Examples"),
            SampleActionInference
        );
        return Task.FromResult(fields);
    }
    }