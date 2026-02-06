using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services;

/// <summary>
/// Validates whether two sockets can be connected.
/// </summary>
public sealed class ConnectionValidator : IConnectionValidator
{
    private readonly ISocketTypeResolver _typeResolver;

    public ConnectionValidator(ISocketTypeResolver typeResolver)
    {
        _typeResolver = typeResolver;
    }

    public bool CanConnect(SocketData source, SocketData target)
    {
        // Rule 1: Output -> Input only
        if (source.IsInput || !target.IsInput)
        {
            return false;
        }

        // Rule 2: Execution sockets connect only to execution sockets
        if (source.IsExecution != target.IsExecution)
        {
            return false;
        }

        // Rule 3: Data types must be compatible (or object)
        if (!source.IsExecution)
        {
            return IsTypeCompatible(source.TypeName, target.TypeName);
        }

        return true;
    }

    private bool IsTypeCompatible(string? sourceTypeName, string? targetTypeName)
    {
        if (string.IsNullOrWhiteSpace(sourceTypeName) || string.IsNullOrWhiteSpace(targetTypeName))
        {
            return false;
        }

        if (IsObjectType(sourceTypeName) || IsObjectType(targetTypeName))
        {
            return true;
        }

        if (IsNumericCompatible(sourceTypeName, targetTypeName))
        {
            return true;
        }

        var sourceType = _typeResolver.Resolve(sourceTypeName);
        var targetType = _typeResolver.Resolve(targetTypeName);

        if (sourceType is not null && targetType is not null)
        {
            return targetType.IsAssignableFrom(sourceType) || sourceType.IsAssignableFrom(targetType);
        }

        return string.Equals(sourceTypeName, targetTypeName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsObjectType(string typeName)
    {
        return string.Equals(typeName, "object", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly IReadOnlyDictionary<string, int> NumericRank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["System.Byte"] = 1,
        ["System.Int16"] = 2,
        ["System.Int32"] = 3,
        ["System.Int64"] = 4,
        ["System.Single"] = 5,
        ["System.Double"] = 6,
        ["System.Decimal"] = 7
    };

    private static bool IsNumericCompatible(string sourceTypeName, string targetTypeName)
    {
        if (!NumericRank.TryGetValue(sourceTypeName, out var sourceRank))
        {
            return false;
        }

        if (!NumericRank.TryGetValue(targetTypeName, out var targetRank))
        {
            return false;
        }

        // Allow widening numeric conversions only (e.g., int -> double)
        return sourceRank <= targetRank;
    }
}
