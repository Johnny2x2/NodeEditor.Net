using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Serialization;

public sealed class GraphSchemaMigrator
{
    public GraphDto MigrateToCurrent(GraphDto dto)
    {
        if (dto is null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        if (dto.Version >= GraphSerializer.CurrentVersion)
        {
            return dto;
        }

        var current = dto;
        var version = dto.Version;
        while (version < GraphSerializer.CurrentVersion)
        {
            current = version switch
            {
                0 => UpgradeFromV0(current),
                   1 => UpgradeFromV1(current),
                _ => throw new NotSupportedException($"Unsupported schema version {version}.")
            };
            version = current.Version;
        }

        return current;
    }

    private static GraphDto UpgradeFromV0(GraphDto dto)
    {
        return dto with { Version = 1 };
    }
    private static GraphDto UpgradeFromV1(GraphDto dto)
    {
        return dto with { Version = 2 };
    }
}

