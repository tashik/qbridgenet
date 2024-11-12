using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using QuikBridgeNet;
using QuikOptionDeskApp;
using Serilog;

class Program
{
    public static List<decimal> SubscribedStrikes { get; set; } = new();
    
    static async Task Main(string[] args)
    {
        string host = "127.0.0.1";
        int port = 57777;

        var serviceCollection = new ServiceCollection();
        QuikBridgeServiceConfiguration.ConfigureServices(serviceCollection);

        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Resolve the client
        var client = serviceProvider.GetRequiredService<QuikBridge>();
        client.IsExtendedLogging = false;

        var baseAssets = new List<BaseAsset>()
        {
            new()
            {
                AssetClassCode = "SPBFUT",
                AssetSecCode = "SiZ4",
                StrikeStep = 500m,
                OptionSeries =
                [
                    DateTime.ParseExact("2024-11-21 18:50:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    DateTime.ParseExact("2024-12-19 18:50:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                ]
            }
        };
        
        CancellationTokenSource cts = new CancellationTokenSource();
        
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        
        var globalEventAggregator = serviceProvider.GetRequiredService<QuikBridgeEventAggregator>();

        decimal centralStrike = 0m;
        
        globalEventAggregator.SubscribeToInstrumentParameterUpdate( eventArgs =>
        {
            if (eventArgs is { ClassCode: "SPBFUT", ParamName: "LAST" })
            {
                var asset = baseAssets.FirstOrDefault(a => a.AssetSecCode == eventArgs.SecCode);
                if (asset == null)
                {
                    return Task.CompletedTask;
                } 
                if (centralStrike == 0 || Math.Abs(centralStrike - decimal.Parse(eventArgs.ParamValue!, CultureInfo.InvariantCulture)) > asset.StrikeStep / 2)
                {
                    return await UpdateCentralStrikeAsync();
                }
            }
            Log.Information("Instrument parameter {Name} current value {Val} ", eventArgs.ParamName, eventArgs.ParamValue);
            return Task.CompletedTask;
        });
    }

    static async Task UpdateCentralStrikeAsync(BaseAsset asset, decimal centralStrike)
    {
        var newStrikes = new List<decimal>();
        var range = asset.NumStrikes;
        for (int i = -range; i <= range; i++)
        {
            decimal strike = centralStrike + (i * asset.StrikeStep);
            newStrikes.Add(strike);
        }
    }
}