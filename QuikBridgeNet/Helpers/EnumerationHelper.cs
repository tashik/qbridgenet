using System.ComponentModel;

namespace QuikBridgeNet.Helpers;

public static class EnumerationHelper
{
    #region Methods

    /// <summary> Метод возвращает значение атрибута Description </summary>
    /// <param name="enumm"> Перечисление </param>
    /// <returns> Атрибут Description </returns>
    public static string GetDescription(this Enum enumm)
    {
        var fieldInfo = enumm.GetType().GetField(enumm.ToString("F"));
        var attribArray = fieldInfo.GetCustomAttributes(false);
        if (attribArray.Length == 0)
            return enumm.ToString("F");
        var attrib = attribArray.OfType<DescriptionAttribute>().FirstOrDefault();
        return attrib != null ? attrib.Description : string.Empty;
    }

    #endregion
}