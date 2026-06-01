namespace QuikBridgeNetDomain;

public class QuikBridgeConfig
{
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 57777;

    public bool UseExtendedLogging { get; set; } = false;
    public bool UseExtendedEventLogging { get; set; } = false;
    public int EventQueueCapacity { get; set; } = 10000;
    public int EventHandlerQueueCapacity { get; set; } = 1000;
    public int DataSourceQueueCapacity { get; set; } = 10000;
    public int RequestTimeoutMs { get; set; } = 0;
    public int RequestExpirySweepIntervalMs { get; set; } = 1000;
    public int EventQueueWarningThreshold { get; set; } = 0;
    public int EventHandlerBacklogWarningThreshold { get; set; } = 0;
}