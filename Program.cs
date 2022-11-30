using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
// using MihaZupan;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json.Linq;
using TradingBot.Telegram;

namespace TradingBot
{
    public class Program
    {
        public static TelegramBotServer TelegramBotServer { get; private set; }

        public static bool Running { get; private set; }
        public static MongoCRUD Database { get; private set; }
        public static OrdersManager OrdersManager { get; private set; }
        public static UsersManager UsersManager { get; private set; }
        public static LicenseManager LicenseManager { get; private set; }
        private static ManualResetEvent _quitEvent = new ManualResetEvent(false);

        public const bool PredictionProgram = false;

        public static string CurrentFolder = System.AppDomain.CurrentDomain.BaseDirectory;

        private static bool DEBUG = true;

        static void Main(string[] args)
        {
            try
            {
                Running = true;
                Console.WriteLine("Iniciando...");

                string databaseIP = File.ReadAllText("database.txt");
                Database = new MongoCRUD("Novo", databaseIP);
                OrdersManager = new OrdersManager("orders", Database);
                UsersManager = new UsersManager("users", Database);
                LicenseManager = new LicenseManager("licenses", Database);


                string telegramKey = File.ReadAllText("telegram.txt");

                TelegramBotServer = new TelegramBotServer();
                TelegramBotServer.StartBot(telegramKey).Wait();
                Console.WriteLine("TelegramServer iniciado...");
                Console.WriteLine(PegaxyBotManager.IgnorePercentage);



                if (PredictionProgram == false)
                {
                    Console.WriteLine("Trading mode");
                    TokenTrackerManager.StartManager();
                    TraderBotManager.StartLoop();
                }
                else
                {
                    Console.WriteLine("Prediction mode");
                }

                _quitEvent.WaitOne();

                Console.WriteLine("Programa finalizou...");
                Logger.WriteLog("Programa finalizou", "main");
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex);
                Logger.WriteLog($"Error(Program) {ex}", "main");
                Logger.WriteLog($"{ex.StackTrace}", "main");
            }
            Logger.WriteLog($"closed", "main");
            Console.WriteLine("closed");
            Task.Delay(TimeSpan.FromDays(1)).Wait();
        }
    }
}