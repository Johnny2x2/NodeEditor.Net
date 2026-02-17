using System.Reflection;
using NodeEditor.Net.Models;

namespace NodeEditor.Blazor.Tests;

public sealed class ModelUiIsolationTests
{
    [Fact]
    public void Models_DoNotExpose_UiFrameworkTypes()
    {
        var modelTypes = typeof(Point2D).Assembly
            .GetTypes()
            .Where(type => type.IsClass || type.IsValueType)
            .Where(type => type.Namespace == typeof(Point2D).Namespace)
            .ToArray();

        foreach (var modelType in modelTypes)
        {
            AssertNoUiTypes(modelType);
        }
    }

    private static void AssertNoUiTypes(Type type)
    {
        var members = type
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Where(member => member.MemberType is MemberTypes.Property or MemberTypes.Field)
            .ToArray();

        foreach (var member in members)
        {
            var memberType = member switch
            {
                PropertyInfo property => property.PropertyType,
                FieldInfo field => field.FieldType,
                _ => null
            };

            if (memberType is null)
            {
                continue;
            }

            var typeNamespace = memberType.Namespace ?? string.Empty;
            Assert.False(IsUiNamespace(typeNamespace), $"{type.Name}.{member.Name} uses UI type {memberType.FullName}");
        }
    }

    private static bool IsUiNamespace(string typeNamespace)
    {
        return typeNamespace.StartsWith("System.Drawing", StringComparison.Ordinal)
            || typeNamespace.StartsWith("System.Windows.Forms", StringComparison.Ordinal)
            || typeNamespace.StartsWith("Microsoft.Maui", StringComparison.Ordinal);
    }
}
