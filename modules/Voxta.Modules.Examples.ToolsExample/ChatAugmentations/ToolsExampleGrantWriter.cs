using Microsoft.Extensions.Logging;
using Voxta.Abstractions.Repositories;
using Voxta.Abstractions.Services.Agents;
using Voxta.Modules.Examples.ToolsExample.Configuration;

namespace Voxta.Modules.Examples.ToolsExample.ChatAugmentations;

/// <summary>
/// Where "yes, always" is written down when the user grants a tool for good, and erased when they take it back.
/// </summary>
public sealed class ToolsExampleGrantWriter(
    Guid userId,
    Guid moduleId,
    IUnitOfWorkFactory unitOfWorkFactory,
    ILogger<ToolsExampleGrantWriter> logger
) : IToolPermissionGrantWriter
{
    public Task RememberAlwaysAllowAsync(string toolName, CancellationToken cancellationToken) =>
        UpdateAlwaysAllowAsync(toolName, allow: true, cancellationToken);

    public Task ForgetAlwaysAllowAsync(string toolName, CancellationToken cancellationToken) =>
        UpdateAlwaysAllowAsync(toolName, allow: false, cancellationToken);

    private async Task UpdateAlwaysAllowAsync(string toolName, bool allow, CancellationToken cancellationToken)
    {
        try
        {
            // One user, named up front: since 1.10 the unit of work carries the encryption scope, and
            // the module's configuration is part of what that key protects.
            await using var uow = unitOfWorkFactory.CreateUnitOfWork(userId);

            var module = await uow.Modules.GetByIdAsync(moduleId, cancellationToken);
            if (module == null)
            {
                logger.LogWarning("Could not update {Tool}: module {ModuleId} is gone.", toolName, moduleId);
                return;
            }

            var field = ModuleConfigurationProvider.AlwaysAllow.Name;
            var current = module.Configuration.GetValueOrDefault(field);
            var tools = current?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
            if (tools.Contains(toolName, StringComparer.Ordinal) == allow) return;

            var updated = allow ? tools.Append(toolName) : tools.Where(t => !string.Equals(t, toolName, StringComparison.Ordinal));
            module.Configuration[field] = string.Join(", ", updated);

            await uow.Modules.UpdateAsync(module);
            await uow.SaveChangesAsync(cancellationToken);

            logger.LogInformation(allow ? "{Tool} is now always allowed." : "{Tool} is no longer always allowed.", toolName);
        }
        catch (Exception exc)
        {
            logger.LogWarning(exc, "Could not update the permission for {Tool}.", toolName);
        }
    }
}
