using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using QuikBridgeNet;
using QuikOptionDeskApp;
using Serilog;

class Program
{
    public static List<decimal> SubscribedStrikes { get; set; } = new();

    public static ConcurrentDictionary<string, Guid> _subsriptions = new();
    
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
        
        globalEventAggregator.SubscribeToInstrumentParameterUpdate( async eventArgs =>
        {
            if (eventArgs is { ClassCode: "SPBFUT", ParamName: "LAST" })
            {
                var asset = baseAssets.FirstOrDefault(a => a.AssetSecCode == eventArgs.SecCode);
                if (asset == null)
                {
                    return;
                } 
                if (centralStrike == 0 || Math.Abs(centralStrike - decimal.Parse(eventArgs.ParamValue!, CultureInfo.InvariantCulture)) > asset.StrikeStep / 2)
                {
                    await UpdateCentralStrikeAsync(asset, centralStrike);
                }
            }
            Log.Information("Instrument parameter {Name} current value {Val} ", eventArgs.ParamName, eventArgs.ParamValue);
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
        
        var strikesToUnsubscribe = SubscribedStrikes.Except(newStrikes).ToList();
        var strikesToSubscribe = newStrikes.Except(SubscribedStrikes).ToList();

        foreach (var strike in strikesToSubscribe)
        {
            
        }
    }
    
    public static string GetOptionCode(DateTime series, BaseAsset baseAsset, decimal strike, bool isCall)
    {
        var optionCode = baseAsset.NameHead;
        var optionCodeTail = GetOptionCodeTail(series, optionCode, isCall);
        var codeLetter = baseAsset.IsSpotBased ? 'C' : 'B';
        
        var strikeStr = strike.ToString("F" + baseAsset.Digits, CultureInfo.InvariantCulture).Replace(',', '.');
       
        optionCode += strikeStr + codeLetter + optionCodeTail;
        return optionCode;
    }

    private static string GetOptionCodeTail(DateTime series, string optionCode, bool isCall)
    {
        char[] callLetters = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L'];
        char[] putLetters = ['M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y'];
        char[] weekLetters = ['A', 'B', 'C', 'D', 'E'];

        var strYear = (series.Year % 10).ToString();
        var strMonth = isCall ? callLetters[series.Month - 1] : putLetters[series.Month - 1];
        var week = "";
        var numExpWeek = NumberWeekOfMonth(series);
        var totalWeeksInMonth = TotalWeeksInMonth(series);

        switch (numExpWeek)
        {
            case 1: week = "A"; break;
            case 2: week = "B"; break;
            case 3: 
                if (optionCode.StartsWith("BR") && (int)series.DayOfWeek == 4)
                {
                    week = "C";
                } else
                {
                    week = "";
                }
                break;
            case 4:
                if (totalWeeksInMonth == 4 && (int) series.DayOfWeek != 4 && optionCode.StartsWith("BR"))
                {
                    break;
                }
                week = "D";
                break;
            case 5:
                if ( (int)series.DayOfWeek != 4 && optionCode.StartsWith("BR"))
                {
                    break;
                }
                week = "E";
                break;
            default: 
                week = "";
                break;
        }

        return strMonth + strYear + week;
    }
    
    public static int TotalWeeksInMonth(DateTime currentDate)
    {
        int daysInMonth = DateTime.DaysInMonth(currentDate.Year, currentDate.Month);
        DateTime firstOfMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
        //days of week starts by default as Sunday = 0
        int firstDayOfMonth = (int)firstOfMonth.DayOfWeek;
        int weeksInMonth = (int)Math.Floor((firstDayOfMonth + daysInMonth) / 7.0);
        return weeksInMonth;
    }

    public static bool IsLastWeekInMonth(DateTime currentDate)
    {
        int weeksInMonth = TotalWeeksInMonth(currentDate);

        return weeksInMonth == 4;
    }

    public static int NumberWeekOfMonth(DateTime currentDate)
    {
        int numberWeekOfTheMonth = 0;
        try
        {
            var firstDayOfTheMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
            var numberDayOfWeek = Convert.ToInt16(firstDayOfTheMonth.DayOfWeek.ToString("D"));
            numberWeekOfTheMonth = (currentDate.Day + numberDayOfWeek - 2) / 7 + 1;

            if (numberDayOfWeek == 5 || numberDayOfWeek == 6 || numberDayOfWeek == 7)
            {
                numberWeekOfTheMonth -= 1;
            }
        }
        catch (Exception ex)
        {
            // add to log
        }
        return numberWeekOfTheMonth;
    }
}