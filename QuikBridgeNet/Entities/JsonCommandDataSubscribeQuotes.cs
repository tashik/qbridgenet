namespace QuikBridgeNet.Entities;

public class JsonCommandDataSubscribeQuotes : JsonCommandData
{
    public string security { get; set; } = "";
    public string cl { get; set; } = "";
}