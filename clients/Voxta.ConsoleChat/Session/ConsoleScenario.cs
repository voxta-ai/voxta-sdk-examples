using System.Reflection;
using Voxta.Client;
using Voxta.Model.Shared;

namespace Voxta.ConsoleChat.Session;

// The app's own scenario (Resources/scenario.json): ensures it is deployed to the server and
// exposes its id for starting chats. Returns null when it could not be published.
public sealed class ConsoleScenario(VoxtaAppDeployer deployer)
{
    // Must match the "Id"/"Version" in Resources/scenario.json.
    private static readonly Guid Id = Guid.Parse("b6f3c2a1-0d4e-4c8b-9a7e-1f2b3c4d5e6f");
    private const string Version = "1.0.0";

    public async Task<Guid?> PublishAsync(CancellationToken cancellationToken)
    {
        var status = await deployer.GetResourceStatusAsync(ChatResourceKind.Scenario, Id, Version, cancellationToken);
        if (status == ChatResourceAvailabilityStatus.Exists)
            return Id;

        var deploy = await deployer.DeployAsync(Id, "scenario.json", ReadEmbeddedJson(), cancellationToken);
        return deploy.Success ? Id : null;
    }

    private static byte[] ReadEmbeddedJson()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("scenario.json", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Embedded scenario.json was not found");
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
