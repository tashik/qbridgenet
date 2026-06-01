namespace QuikBridgeNet;

internal enum SessionResourceKind
{
    OrderBookSubscription,
    QuoteParameterSubscription,
    Datasource
}

internal sealed record SessionResourceDefinition(
    SessionResourceKind Kind,
    string ClassCode,
    string SecCode,
    string ResourceKey,
    string? ParamName = null,
    string? Interval = null);