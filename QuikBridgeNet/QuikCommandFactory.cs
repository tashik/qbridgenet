using Newtonsoft.Json;
using QuikBridgeNet.Entities.CommandData;

namespace QuikBridgeNet;

internal static class QuikCommandFactory
{
    public static JsonReqData CreateInvoke(string function, string[]? arguments = null, object? obj = null)
    {
        return new JsonReqData
        {
            method = "invoke",
            function = function,
            arguments = arguments,
            obj = obj
        };
    }

    public static JsonCommandDataSubscribeQuotes CreateQuotesSubscription(string method, string classCode, string secCode)
    {
        return new JsonCommandDataSubscribeQuotes
        {
            method = method,
            cl = classCode,
            security = secCode
        };
    }

    public static JsonCommandDataSubscribeParam CreateParamSubscription(string method, string classCode, string secCode, string paramName)
    {
        return new JsonCommandDataSubscribeParam
        {
            method = method,
            cl = classCode,
            security = secCode,
            param = paramName
        };
    }

    public static JsonCommandDataCallback CreateCallbackRegistration(string callback)
    {
        return new JsonCommandDataCallback
        {
            method = "register",
            callback = callback
        };
    }

    public static string[] CreateQuotedArguments(params string[] values)
    {
        return [string.Join(",", values.Select(value => JsonConvert.ToString(value)))];
    }

    public static string[] CreateMixedArguments(params object[] values)
    {
        return [string.Join(",", values.Select(FormatArgument))];
    }

    private static string FormatArgument(object value)
    {
        return value switch
        {
            string text => JsonConvert.ToString(text) ?? "null",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "null"
        };
    }
}