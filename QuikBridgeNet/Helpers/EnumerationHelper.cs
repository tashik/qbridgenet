using System.ComponentModel;
using System.Reflection;

namespace QuikBridgeNet.Helpers;

public static class EnumerationHelper
{
    /// <summary>
    /// Returns the value of the Description attribute associated with an enum value.
    /// </summary>
    /// <param name="enumValue">The enum value.</param>
    /// <returns>The Description attribute, or the enum value as a string if the attribute is not found.</returns>
    public static string GetDescription(this Enum enumValue)
    {
        var fieldInfo = enumValue.GetType().GetField(enumValue.ToString());
        var description = fieldInfo?.GetCustomAttribute<DescriptionAttribute>()?.Description;
        return description ?? enumValue.ToString();
    }
}