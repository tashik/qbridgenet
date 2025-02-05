namespace QuikBridgeNetDomain;

public class QuikBridgeConfig
{
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 57777;

    public bool UseExtendedLogging { get; set; } = false;
    public bool UseExtendedEventLogging { get; set; } = false;
}