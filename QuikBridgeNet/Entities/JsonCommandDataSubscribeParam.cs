namespace QuikBridgeNet.Entities;

public class JsonCommandDataSubscribeParam: JsonCommandData
{
    public string security { get; set; } = "";
    public string cl { get; set; } = "";
    public string param { get; set; } = "";
}