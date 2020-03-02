using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Bot.NamebaseClient;
using Bot.NamebaseClient.Responses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Bot
{
    public static class Program
    {
        private const int STD_INPUT_HANDLE = -10;

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CancelIoEx(IntPtr handle, IntPtr lpOverlapped);

        private static Task<string?> ReadKey(CancellationToken token)
        {
            try
            {
                return Task.Run(() =>
                {
                    try
                    {
                        token.Register(() =>
                        {
                            var handle = GetStdHandle(STD_INPUT_HANDLE);
                            CancelIoEx(handle, IntPtr.Zero);
                        });
                        var key = Console.ReadKey(false);
                        return key.KeyChar.ToString();
                    }
                    catch
                    {
                        Console.WriteLine("No input");
                        return null;
                    }
                });
            }
            catch
            {
                return Task.FromResult(null as string);
            }
        }

        private static async Task Main(string[] args)
        {
            var devEnvironmentVariable = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT");
            var isDev = string.IsNullOrEmpty(devEnvironmentVariable) ||
                        devEnvironmentVariable.ToLower() == "development";

            var homePath =
                Environment.OSVersion.Platform == PlatformID.MacOSX ||
                Environment.OSVersion.Platform == PlatformID.Unix
                    ? Environment.GetEnvironmentVariable("HOME")
                    : Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");

            var configPath = $"{homePath}{Path.DirectorySeparatorChar}tradeBot.json";

            if (args.Length == 1)
            {
                if (!File.Exists(args[0]))
                {
                    Console.WriteLine($"Unable to load configuration at {args[0]}");
                    Environment.Exit(1);
                }

                configPath = args[0];
            }

            var builder = new ConfigurationBuilder().AddJsonFile(configPath, false, true);

            if (isDev) builder.AddUserSecrets<Auth>();

            var configuration = builder.Build();

            var reloadToken = configuration.GetReloadToken();

            if (!Enum.TryParse<LogLevel>(configuration["LogLevel"], out var logLevel))
            {
                logLevel = LogLevel.None;
            }

            var authSection = configuration.GetSection("Auth");

            var auth = new Auth
            {
                Key = authSection["Api:key"],
                Secret = authSection["Api:secret"]
            };

            var socketHandler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = 30,
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            };

            var httpClient = new HttpClient(socketHandler);

            var services = new ServiceCollection().AddSingleton<ILogger>(new ListLogger
                {
                    Level = logLevel,
                }).AddSingleton(auth).AddSingleton<Client>()
                .AddSingleton(httpClient);

            var serviceProvider = services.BuildServiceProvider();

            var client = serviceProvider.GetService<Client>();

            var botConfiguration = LoadFromConfig(configuration);

            var bot = await TradingBot.CreateInstance(client, botConfiguration, serviceProvider.GetService<ILogger>());

            reloadToken.RegisterChangeCallback(o => { bot._config = LoadFromConfig(configuration); }, null);

            await bot.Reset();
            await bot.Display();
        }

        private static TradingBot.Configuration LoadFromConfig(IConfiguration configuration)
        {
            var defaults = new TradingBot.Configuration("Bot");

            var botSection = configuration.GetSection("Bot");
            var balanceSection = botSection.GetSection("Balances").GetChildren();

            if (balanceSection == null)
            {
                Console.WriteLine("Configuration must have a balances section");
                Environment.Exit(1);
            }

            var hnsSection = balanceSection.First(x => x["Currency"] == "HNS");
            var btcSection = balanceSection.First(x => x["Currency"] == "BTC");

            if (hnsSection == null || btcSection == null)
            {
                Console.WriteLine("Configuration must have a currency definition in Balances");
                Environment.Exit(1);
            }

            return new TradingBot.Configuration(configuration["Name"])
            {
                NumberOrders = int.Parse(botSection["NumberOrders"] ?? defaults.NumberOrders.ToString()),
                MinDistanceFromCenter = decimal.Parse(botSection["MinDistanceFromCenter"] ?? defaults.MinDistanceFromCenter.ToString()),
                SellBottomChange = decimal.Parse(botSection["CenterChangeThreshold"] ?? defaults.SellBottomChange.ToString()),
                SellTopChange = decimal.Parse(botSection["ResistanceChangeThreshold"] ?? defaults.SellTopChange.ToString()),
                BtcRisk = decimal.Parse(btcSection["MaximumRisk"] ?? defaults.BtcRisk.ToString()),
                BtcZero = decimal.Parse(btcSection["Zero"] ?? defaults.BtcZero.ToString()),
                HnsRisk = decimal.Parse(hnsSection["MaximumRisk"] ?? defaults.HnsRisk.ToString()),
                HnsZero = decimal.Parse(hnsSection["Zero"] ?? defaults.HnsZero.ToString()),
                UpdatePeriod = double.Parse(botSection["UpdatePeriod"] ?? defaults.UpdatePeriod.ToString()),
                BtcRatio = decimal.Parse(btcSection["Ratio"] ?? defaults.BtcRatio.ToString()),
                HnsRatio = decimal.Parse(hnsSection["Ratio"] ?? defaults.HnsRatio.ToString()),
            };
        }

        public static long ToUnixTime(this DateTime self)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return (long) (self - epoch).TotalMilliseconds;
        }

        public static DateTime ToDateTime(this long self)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return epoch + TimeSpan.FromMilliseconds(self);
        }

        public static string ToJson(this object self)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Converters = new List<JsonConverter> {new StringEnumConverter()}
            };

            return JsonConvert.SerializeObject(self, settings);
        }

        private enum Modes
        {
            MainMenu,
            SellMenu,
            BuyMenu,
            Auto
        }

        public class Auth
        {
            public string Key { get; set; }
            public string Secret { get; set; }
        }
    }
}