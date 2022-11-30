using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;
using System.Timers;
// using MihaZupan;
using Nethereum.Contracts;
using Nethereum.RPC.Fee1559Suggestions;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json.Linq;
using TradingBot;
using static Nethereum.Util.UnitConversion;
using Timer = System.Timers.Timer;

public static class PegaxyBotManager
{
    private const string GOOGLE_KEY = "6LfT49ocAAAAANbrj7pDsLtlFe5maqwvceH8k3Dr"; //Pegaxy captcha key
    private const string CAPTCHA_PAGE = "https://play.pegaxy.io/renting?tab=share-profit";

    private static List<PegaxyBot> botList; //Talvez transformar em TempList
    private static TempList<BigInteger> ignoreHorsesList;
    public static TempList<String> CaptchaTokenList { get; private set; }
    public static int MaxCaptchaTokens = 3;
    public static TimeSpan CaptchaTokenExpirationTime = TimeSpan.FromSeconds(90);
    public static TimeSpan BotExpirationTime = TimeSpan.FromDays(5);
    public static float IgnorePercentage = 86f; // Adicionar um comando pra alterar esse valor
    public static float MaxPercentage = 35f; // Adicionar um comando pra alterar esse valor
    public static DateTimeOffset lastClaim;

    public static int len_botList { get; private set; }

    private static bool looping = false;
    public static bool canOpenOrder = false;
    public static string stopMessage;
    // private const string pegaxyAPISharedProfit = "https://api.pegaxy.io/rent/0?rentMode=SHARE_PROFIT";
    private const string pegaxyAPISharedProfit = "http://127.0.0.1:5000/rent_shared/0";
    private const string pegaxyAPIPayRent = "http://127.0.0.1:5000/rent_fee/0";
    public const string pegaxyClaimAPI = "http://127.0.0.1:5000/claimhash/";
    private const string gasTrackerAPI = "https://gpoly.blockscan.com/gasapi.ashx?apikey=key&method=gasoracle";

    public static int loopDelay_ms = 10;

    public static bool Paused { get; private set; }
    public static DateTimeOffset PausedTime;
    public static bool AutoGas = true;
    public static bool AutoGasLimit = true;
    public static bool AutoMultiplier = true;
    public static int GasLimitPegaxy = 480000;

    public static int TimeToTryCheckTransactionState = 15;

    public static float GasMultipler = 1.5f;
    public static float GasLowerMultipler = 1f;
    public static float GasHighPorceMultipler = 1.5f;

    public static bool DebugMode = false;
    public static Fee1559 ActualGas { get; private set; }
    public static FeeData ActualFeeData { get; private set; }
    public static int TotalCaptchaSolved = 0;
    public static int TotalHorsesBought = 0;
    public static int MaxOrdersPerAddress = 2;
    public static int MaxTriesOrder = 12;
    public static int IgnoreHorseMin = 25;

    // private static System.Net.Http.HttpClient getHorsesHttpClient;
    private static System.Net.Http.HttpClient captchaHttpClient;
    private static System.Net.Http.HttpClient getHorseHttp;
    private static System.Net.Http.HttpClient getClaimHashHttp;
    private static Timer captchaTokenTimer;
    private static Timer ExpireTimer;
    private static Timer gasTrackerTimer;

    public static Dictionary<string, int> DictOrderPerAddress { get; private set; }
    private static List<PegaxyBot> DesactivedBots;

    private static string lastStatusMsg;
    private static TimeSpan timeBtwStatusRequest = TimeSpan.FromMinutes(1);
    private static DateTimeOffset lastStatusUpdate;
    private static string lastQueueResultMsg;
    private static TimeSpan timeBtwQueueRequest = TimeSpan.FromMinutes(3);
    private static DateTimeOffset lastQueueUpdate;

    private static String managerLogName = "PegaxyBotManager";
    private static List<ProxyContainer> proxyClaimHashList;
    private static List<ProxyContainer> proxyGetHorseList;
    private static int currentProxyClaimHashIndex = 0;
    private static int currentProxyGetHorseIndex = 0;

    public static int ProcessingCaptcha { get; private set; }
    private static Timer proxyRequestTimer;

    private static bool RentSharedMode = true;

    // private static Web3 web3_http;
    // private static Function func_RentWithPassword;

    static PegaxyBotManager()
    {
        botList = new List<PegaxyBot>();
        DictOrderPerAddress = new Dictionary<string, int>();
        DesactivedBots = new List<PegaxyBot>();
        ignoreHorsesList = new TempList<BigInteger>(delayMs: 10000);
        len_botList = 0;

        captchaHttpClient = new System.Net.Http.HttpClient();
        getHorseHttp = new System.Net.Http.HttpClient();
        getClaimHashHttp = new System.Net.Http.HttpClient();

        string proxyGetHorseJson = File.ReadAllText(RentSharedMode ? "pegaxy/proxy2.json" : "pegaxy/proxy3.json");
        // proxyGetHorseList = JObject.Parse(proxyGetHorseJson)["ACCOUNTS"].ToList().Select(x => x.ToObject<ProxyContainer>()).ToList();
        proxyGetHorseList = new List<ProxyContainer>();
        proxyClaimHashList = new List<ProxyContainer>();

        for (int i = 0; i < proxyGetHorseList.Count; i++)
        {
            proxyGetHorseList[i].withSignature = false;
            if (RentSharedMode == false)
            {
            }
            proxyGetHorseList[i].CreateHttpClient().Wait();
        }
        Console.WriteLine("Proxy Get Horse carregados");

        if (RentSharedMode)
        {
            string proxyClaimHashJson = File.ReadAllText("pegaxy/proxy.json");
            // proxyClaimHashList = JObject.Parse(proxyClaimHashJson)["ACCOUNTS"].ToList().Select(x => x.ToObject<ProxyContainer>()).ToList();
            proxyClaimHashList = new List<ProxyContainer>();

            CaptchaTokenList = new TempList<String>(delayMs: 5000);


            for (int i = 0; i < proxyClaimHashList.Count; i++)
            {
                proxyClaimHashList[i].CreateHttpClient().Wait();
            }
            Console.WriteLine("Proxy ClaimHash carregados");




            captchaTokenTimer = new Timer(CaptchaTokenExpirationTime.TotalMilliseconds / 3);
            captchaTokenTimer.AutoReset = true;
            captchaTokenTimer.Elapsed += new ElapsedEventHandler((object sender, ElapsedEventArgs e) => captchaTokenTimerLoop());
            captchaTokenTimer?.Start();
        }

        gasTrackerTimer = new Timer(TimeSpan.FromMinutes(1).TotalMilliseconds);
        gasTrackerTimer.AutoReset = true;
        gasTrackerTimer.Elapsed += new ElapsedEventHandler((object sender, ElapsedEventArgs e) => GasTrackerUpdate());

        proxyRequestTimer = new Timer(TimeSpan.FromMinutes(1.5).TotalMilliseconds);
        proxyRequestTimer.AutoReset = true;
        proxyRequestTimer.Elapsed += new ElapsedEventHandler((object sender, ElapsedEventArgs e) => ProxyTimerUpdate());

        gasTrackerTimer?.Start();
        proxyRequestTimer?.Start();

        CreateExpireTimer();
        ExpireTimer?.Start();

        GasTrackerUpdate();
        Log("Static Constructor created");
    }

    private static HttpClient GetProxyClaimHashContainer()
    {
        return getClaimHashHttp;
        ProxyContainer container = proxyClaimHashList[currentProxyClaimHashIndex];
        if (DebugMode)
        {
            Log($"Proxy ClaimHash Selecionado >: {container.PROXY_IP}");
        }
        currentProxyClaimHashIndex = (currentProxyClaimHashIndex + 1) % proxyClaimHashList.Count;
        return container.HttpClient;
    }

    private static HttpClient GetProxyGetHorseContainer()
    {
        return getHorseHttp;
        ProxyContainer container = proxyGetHorseList[currentProxyGetHorseIndex];
        if (DebugMode)
        {
            Log($"Proxy GetHorse Selecionado >: {container.PROXY_IP}");
        }
        currentProxyGetHorseIndex = (currentProxyGetHorseIndex + 1) % proxyGetHorseList.Count;
        return container.HttpClient;
    }

    private static void CreateExpireTimer()
    {
        ExpireTimer = new Timer(CaptchaTokenExpirationTime.TotalMilliseconds); // Set the time (5 mins in this case)
        ExpireTimer.AutoReset = true;
        ExpireTimer.Elapsed += new ElapsedEventHandler((object sender, ElapsedEventArgs e) => RemoveAllExpired());
    }

    private static void ProxyTimerUpdate()
    {
        if (Paused) return;


        if (botList.Count >= 1)
        {
            bool allPaused = false;
            foreach (var bot in botList.ToArray())
            {
                if (bot != null)
                {
                    allPaused = bot.Paused;
                    if (allPaused == false)
                    {
                        break;
                    }
                }
            }

            if (allPaused) { 
                looping = false;
            }
        }

        foreach (var item in proxyGetHorseList)
        {
            if (Paused) return;

            item.HttpClient.GetAsync("http://jsonip.com");
        }

        if (RentSharedMode == false) return;

        foreach (var item in proxyClaimHashList)
        {
            if (Paused) return;

            item.HttpClient.GetAsync("http://jsonip.com");
        }
    }

    private async static void GasTrackerUpdate()
    {
        if (Paused) return;

        try
        {
            if (AutoGasLimit)
            {
                Random rnd = new Random();
                GasLimitPegaxy += rnd.Next(-3, 5);
                // GasLimitPegaxy = (int)(await func_RentWithPassword.EstimateGasAsync(new object[]{650505, "1a9xckF0MUAq0BAh"})).Value; 
            }
        }
        catch (System.Exception ex)
        {

            Log("Error GasTracker getting gas limit");
            Log(ex);
            Log(ex.Message);
        }


        if (AutoGas == false) return;
        int lowerGas = 0;
        if (AutoMultiplier)
        {
            try
            {
                var response = await (await captchaHttpClient.GetAsync(gasTrackerAPI)).Content.ReadAsStringAsync();
                var responseParsed = JObject.Parse(response);
                if (responseParsed["message"].Value<string>() == "OK")
                {
                    lowerGas = (int)(float.Parse(responseParsed["result"]["SafeGasPrice"].Value<string>()) * GasLowerMultipler);
                }
            }
            catch (System.Exception ex)
            {
                lowerGas = 0;
                Log("Error GasTracker getting fee api");
                Log(ex);
                Log(ex.Message);
                // throw;
            }
        }

        try
        {
            var web3_http = new Web3("https://polygon-rpc.com");
            Fee1559 fastFee = (await (new Nethereum.RPC.Fee1559Suggestions.TimePreferenceFeeSuggestionStrategy(web3_http.Client)).SuggestFeesAsync())[0];

            Fee1559 newFee = new Fee1559();

            if (AutoMultiplier)
            {
                if (lowerGas > 0)
                {
                    if ((float)Web3.Convert.FromWei(fastFee.MaxFeePerGas.Value, EthUnit.Gwei) < (float)lowerGas)
                    {
                        GasMultipler = (float)lowerGas / (float)Web3.Convert.FromWei(fastFee.MaxFeePerGas.Value, EthUnit.Gwei);
                    }
                    else
                    {
                        GasMultipler = 1f;
                    }
                }
            }

            newFee.BaseFee = new BigInteger((double)fastFee.BaseFee.Value * GasMultipler);
            newFee.MaxFeePerGas = new BigInteger((double)fastFee.MaxFeePerGas.Value * GasMultipler);
            newFee.MaxPriorityFeePerGas = new BigInteger((double)fastFee.MaxPriorityFeePerGas.Value * GasMultipler);

            ActualGas = newFee;
            ActualFeeData = new FeeData(ActualGas);
        }
        catch (System.Exception ex)
        {
            Log("Error GasTracker getting fee network");
            Log(ex);
            Log(ex.Message);
            // throw;
        }

        try
        {
            foreach (var bot in DesactivedBots.ToArray())
            {
                if (bot.ForcePause == false && (bot.MaxGas >= (float)ActualFeeData.MaxFeePerGas || bot.Paused == false))
                {
                    ActiveBot(bot);
                }
            }

            foreach (var bot in botList.ToArray())
            {
                if (bot == null) continue;
                if (bot.Paused == true && DesactivedBots.Contains(bot) == false)
                {
                    DesactiveBot(bot);
                }
            }
        }
        catch (System.Exception ex)
        {
            Log("Error ActiveBot Loop");
            Log(ex);
            Log(ex.Message);
            // throw;
        }

        return;
    }

    private static void RemoveAllExpired()
    {
        if (Paused) return;

        try
        {
            var currentDateTime = DateTimeOffset.UtcNow;
            // ToArray prevents modifying an iterated collection.
            foreach (var bot in botList.ToArray())
            {
                if (bot == null)
                {
                    try
                    {
                        botList.Remove(bot);
                    }
                    catch { }
                    continue;
                }
                if (bot.StartDate != default(DateTimeOffset) && bot.Running == true && ((currentDateTime - bot.StartDate) > BotExpirationTime))
                {
                    ComunicationManager.SendMessageAsync($"Ordem ({bot.ID}) Expirou", bot.UserID);
                    bot?.ForceStop();
                }
                else if (bot?.Running == false)
                {
                    Remove(bot);
                }
            }
        }
        catch (System.Exception e)
        {
            // Console.WriteLine(e);
            Log("Error Expired Manager");
            Log(e);
            // throw e;
        }
    }

    public async static void captchaTokenTimerLoop()
    {
        if (Paused) return;
        if (looping == false) return;

        int captchaCompleted = CaptchaTokenList.List.Count;

        if ((captchaCompleted + ProcessingCaptcha) < MaxCaptchaTokens)
        {
            if (DateTimeOffset.UtcNow - lastClaim > CaptchaTokenExpirationTime)
            {
                await AddCaptchaToken();
                lastClaim = DateTimeOffset.UtcNow;
            }
            else if (len_botList >= (captchaCompleted + ProcessingCaptcha))
            {
                await AddCaptchaToken();
            }
        }

        if (captchaCompleted == 0 && ProcessingCaptcha == 0)
        {
            await AddCaptchaToken();
        }
    }

    private static async Task AddCaptchaToken(bool count = true)
    {
        if (looping == false)
        {
            if (count == false)
            {
                ProcessingCaptcha -= 1;
            }
            return;
        }
        if (count)
        {
            ProcessingCaptcha += 1;
        }

        try
        {
            string contentString =
            "{" +
                "\"clientKey\": \"" + "[CLIENT KEY CAPMONSTER]" + "\"," + // CLIENT KEY CAPMONSTER PRIVATE
                "\"task\": " + "{" +
                    "\"type\": " + "\"NoCaptchaTaskProxyless\"," +
                    "\"websiteURL\": " + "\"" + CAPTCHA_PAGE + "\"," +
                    "\"websiteKey\": " + "\"" + GOOGLE_KEY + "\"" +
                     "}}";

            var contentData = new StringContent(contentString, System.Text.Encoding.UTF8, "application/json");

            var response = await captchaHttpClient.PostAsync("https://api.capmonster.cloud/createTask", contentData);
            string responseMessage = await response.Content.ReadAsStringAsync();
            if (DebugMode)
            {
                Log(responseMessage);
                Console.WriteLine(responseMessage);
            }
            var jsonParsed = JObject.Parse(responseMessage);

            if (response.IsSuccessStatusCode)
            {
                if (jsonParsed["errorId"]?.Value<int>() != 0)
                {
                    Log("Error na criação do captcha :" + jsonParsed["errorId"]?.Value<int>());
                    AddCaptchaToken(false);
                    return;
                }

                long taskId = jsonParsed["taskId"].Value<long>();
                await Task.Delay(TimeSpan.FromSeconds(15));

                bool run = true;

                contentString =
                    "{" +
                        "\"clientKey\": \"" + "[CLIENT KEY CAPMONSTER]" + "\"," + // CLIENT KEY CAPMONSTER PRIVATE
                        "\"taskId\": " + taskId +
                    "}";

                contentData = new StringContent(contentString, System.Text.Encoding.UTF8, "application/json");

                string captchaToken = null;
                while (run)
                {
                    response = await captchaHttpClient.PostAsync("https://api.capmonster.cloud/getTaskResult", contentData);
                    jsonParsed = JObject.Parse(await response.Content.ReadAsStringAsync());

                    if (jsonParsed == null)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10));
                        continue;
                    }

                    if (jsonParsed.GetValue("solution").HasValues)
                    {
                        captchaToken = jsonParsed["solution"]?["gRecaptchaResponse"]?.Value<string>();
                    }

                    if (String.IsNullOrEmpty(captchaToken))
                    {
                        if (jsonParsed["status"]?.Value<string>().Trim() == "processing")
                        {
                            await Task.Delay(TimeSpan.FromSeconds(10));
                            continue;
                        }
                        else
                        {
                            string errorCode = jsonParsed["errorCode"]?.Value<string>();
                            Log("Error getting captcha result" +
                            $"ErrorID: {jsonParsed["errorId"]?.Value<int>()}" +
                            $"ErrorCode: {jsonParsed["errorCode"]?.Value<string>()}"
                            );
                            if (errorCode != null && errorCode.Contains("ERROR_ZERO_BALANCE"))
                            {
                                SetPauseState(true);
                                for (int i = 0; i < 15; i++)
                                {
                                    ComunicationManager.SendMessageAsync("CAPTCHA SEM GRANA AVISA PRO LUCAS", 1984674073);
                                    ComunicationManager.SendMessageAsync("CAPTCHA SEM GRANA AVISA PRO LUCAS", 1046959851);
                                }

                            }
                            run = false;
                            AddCaptchaToken(false);
                            return;
                        }
                    }
                    else
                    {
                        run = false;
                        ProcessingCaptcha -= 1;
                        TotalCaptchaSolved += 1;
                        CaptchaTokenList.Add(captchaToken, CaptchaTokenExpirationTime);
                        return;
                    }
                }
                AddCaptchaToken(false);
                return;
            }
            else
            {
                Log("Error responseStatus getting captcha result" +
                $"ErrorID: {jsonParsed["errorId"]?.Value<int>()}" +
                $"ErrorCode: {jsonParsed["errorCode"]?.Value<string>()}"
                );

                AddCaptchaToken(false);
                return;
            }
        }
        catch (System.Exception e)
        {
            Log(e);
            AddCaptchaToken(false);
            return;
        }
    }

    public static void SetPauseState(bool state)
    {
        if (state == false && Paused != false)
        {
            if (PausedTime != default(DateTimeOffset) && PausedTime != null)
            {
                TimeSpan timePassed = DateTimeOffset.UtcNow - PausedTime;
                Log($"Pause time added {timePassed}");
                foreach (var bot in botList.ToArray())
                {
                    bot.StartDate += timePassed;
                }
            }
            Paused = false;
            Log($"Unpause");
        }
        else if (state = true && Paused != true)
        {
            Paused = true;
            PausedTime = DateTimeOffset.UtcNow;
            Log($"Paused at {PausedTime}");
        }
    }

    private async static Task<string> GetClaimHash(BigInteger horseId, string captchaToken)
    {
        try
        {
            var claimHashRequest = new HttpRequestMessage()
            {
                Method = HttpMethod.Get
            };
            claimHashRequest.RequestUri = new Uri($"{pegaxyClaimAPI}{horseId}/{captchaToken}");

            HttpClient proxyHttp = GetProxyClaimHashContainer();

            try
            {
                string response = await (await proxyHttp.SendAsync(claimHashRequest)).Content.ReadAsStringAsync();
                string claimHash;
                var responseParsed = JObject.Parse(response.Replace("\\", "").Trim().Trim('"'));
                if (responseParsed["status"].Value<bool>())
                {
                    claimHash = responseParsed["listing"]["claimHash"].ToString();
                    Log($"ClaimHash: {claimHash} / HorseID {horseId}");
                }
                else
                {
                    Log("Error: " + responseParsed["message"]?.ToString());
                    claimHash = null;
                }

                return claimHash;
            }
            catch (HttpRequestException e)
            {
                Log(e);
                SetPauseState(true);
                return null;
            }
            catch (System.Exception e)
            {
                // if (e.Message.Contains("Connection refused"))
                // {
                //     await proxyContainer.CreateHttpClient();
                // }

                Log(e);
                return null;
            }
        }
        catch (System.Exception e)
        {
            Log(e);
            return null;
        }
    }

    public static void Add(PegaxyBot bot)
    {
        if (Paused == true)
        {
            throw new MessageException("Você não pode adicionar uma ordem no momento");
        }

        if (canOpenOrder == false)
        {
            if (UsersManager.Instance.Get(bot.UserID).UserPlan < UserPlanEnum.PegaxyVip)
            {
                throw new MessageException("Você não pode adicionar uma ordem no momento");
            }
        }

        if (DictOrderPerAddress.ContainsKey(bot.Account.Address))
        {
            DictOrderPerAddress[bot.Account.Address] += 1;
        }
        else
        {
            DictOrderPerAddress.Add(bot.Account.Address, 1);
        }
        botList.Add(bot);
        len_botList += 1;

        if (looping == false && Program.Running)
        {
            StartLoop();
        }
    }

    public static void Remove(PegaxyBot bot)
    {
        if (bot == null) return;

        Task.Delay(1).Wait();
        if (botList.Contains(bot))
        {
            if (DictOrderPerAddress.ContainsKey(bot.Account.Address))
            {
                DictOrderPerAddress[bot.Account.Address] -= 1;

                if (DictOrderPerAddress[bot.Account.Address] < 0)
                {
                    DictOrderPerAddress[bot.Account.Address] = 0;
                }
            }

            if (DesactivedBots.Contains(bot))
            {
                for (int i = 0; i < DesactivedBots.Count(x => x == bot); i++)
                {
                    try
                    {
                        DesactivedBots.Remove(bot);
                    }
                    catch (System.Exception ex) { Log("Error removendo da lista de bots desativados"); Log(ex); }
                }
            }

            for (int i = 0; i < botList.Count(x => x == bot); i++)
            {
                try
                {
                    botList.Remove(bot);
                }
                catch (System.Exception ex) { Log("Error removendo da lista de bots"); Log(ex); }
            }

            len_botList -= 1;
            bot?.ForceStop();
            bot = null;
        }
    }

    public static void RemoveAllUserOrdersByID(long userId, long botId)
    {
        try
        {
            var list = botList.ToArray();
            var len_list = list.Length;
            for (int i = 0; i < len_list; i++)
            {
                var bot = list[i];
                if (bot != null)
                {
                    if (bot.ID != botId && bot.UserID == userId)
                    {
                        bot?.ForceStop();
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Log(e);
        }
    }

    public static void RemoveAllUserOrdersByAddress(string address, long botId)
    {
        try
        {
            var list = botList.ToArray();
            var len_list = list.Length;

            for (int i = 0; i < len_list; i++)
            {
                var bot = list[i];
                if (bot != null)
                {
                    if (bot.ID != botId && bot.Account.Address == address)
                    {
                        bot?.ForceStop();
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Log(e);
        }
    }

    public static async void StartLoop()
    {
        if (Paused)
        {
            return;
        }
        looping = true;
        if (RentSharedMode)
        {
            Task.Run(LoopSharedProfit);
        }
        else
        {
            Task.Run(LoopRentPay);
        }
    }

    public static async Task<List<Horse>> GetAllHorsesSharedProfit(int limitPage = 0)
    {
        try
        {
            HttpClient proxyHttp = GetProxyGetHorseContainer();
            try
            {

                var response = await proxyHttp.GetStringAsync(pegaxyAPISharedProfit);
                if (DebugMode)
                {
                    Console.WriteLine(response.Replace("\\", "").Trim().Trim('"'));
                    Log(response);
                }

                var responseJson = JObject.Parse(response.Replace("\\", "").Trim().Trim('"'));
                List<Horse> horses = responseJson["renting"].ToList().Select(x => x.ToObject<Horse>()).ToList();

                int totalHorses = responseJson["total"].Value<int>();

                if (totalHorses > 12)
                {
                    Log($"Lista com muitos cavalos: {totalHorses}");
                    for (int i = 1; i < (int)Math.Ceiling((double)(totalHorses - 12) / 12); i++)
                    {
                        if (limitPage != 0 && i >= limitPage) break;
                        try
                        {
                            await Task.Delay(loopDelay_ms);
                            proxyHttp = GetProxyGetHorseContainer();
                            response = await proxyHttp.GetStringAsync($"http://127.0.0.1:5000/rent_shared/{i}");
                            responseJson = JObject.Parse(response);
                            if (DebugMode)
                            {
                                Log(response);
                            }
                            horses.AddRange(responseJson["renting"].ToList().Select(x => x.ToObject<Horse>()).ToList());
                        }
                        catch (System.Exception ex)
                        {
                            Log(ex);
                            continue;
                        }

                    }
                }

                return horses;
            }
            catch (HttpRequestException e)
            {
                Log(e);
                SetPauseState(true);
                return new List<Horse>();
            }
            catch (System.Exception e)
            {
                // Log($"Proxy: {proxyContainer.PROXY_IP}");
                // if (e.Message.Contains("Connection refused"))
                // {
                //     await proxyContainer.CreateHttpClient();
                // }

                Log(e);
                return new List<Horse>();
            }
        }
        catch (System.Exception e)
        {
            Log("Error geting horses");
            Log(e);
            await Task.Delay(TimeSpan.FromSeconds(6));
            return new List<Horse>();
        }
    }

    public static async Task<List<HorseWithDetails>> GetAllHorsesPayRent(int limitPage = 0)
    {
        try
        {
            HttpClient proxyHttp = GetProxyGetHorseContainer();
            try
            {

                var response = await proxyHttp.GetStringAsync(pegaxyAPIPayRent);
                var responseJson = JObject.Parse(response.Replace("\\", "").Trim().Trim('"'));
                if (DebugMode)
                {
                    Log(response);
                }
                List<HorseWithDetails> horses = responseJson["renting"].ToList().Select(x => x.ToObject<HorseWithDetails>()).ToList();

                int totalHorses = responseJson["total"].Value<int>();

                if (totalHorses > 12 && false) // False
                {
                    Log($"Lista com muitos cavalos: {totalHorses}");
                    int pageHorses = (int)Math.Ceiling((double)(totalHorses - 12) / 12);
                    for (int i = 1; i < pageHorses; i++)
                    {
                        if (limitPage != 0 && i >= limitPage) break;
                        try
                        {
                            await Task.Delay(loopDelay_ms);
                            proxyHttp = GetProxyGetHorseContainer();
                            response = await proxyHttp.GetStringAsync($"http://127.0.0.1:5000/rent_fee/{i}");
                            responseJson = JObject.Parse(response);
                            if (DebugMode)
                            {
                                Log(response);
                            }
                            horses.AddRange(responseJson["renting"].ToList().Select(x => x.ToObject<HorseWithDetails>()).ToList());
                        }
                        catch (System.Exception ex)
                        {
                            Log(ex);
                            continue;
                        }

                    }
                }

                return horses;
            }
            catch (HttpRequestException e)
            {
                Log(e);
                SetPauseState(true);
                return new List<HorseWithDetails>();
            }
            catch (System.Exception e)
            {
                // Log($"Proxy: {proxyContainer.PROXY_IP}");
                // if (e.Message.Contains("Connection refused"))
                // {
                //     await proxyContainer.CreateHttpClient();
                // }

                Log(e);
                Log(e.StackTrace);
                return new List<HorseWithDetails>();
            }
        }
        catch (System.Exception e)
        {
            Console.WriteLine("Error geting horses");
            Log("Error geting horses");
            Log(e);
            await Task.Delay(TimeSpan.FromSeconds(6));
            return new List<HorseWithDetails>();
        }
    }

    private static async Task RentSharedProfitHorseAsync(PegaxyBot bot, Horse horse, string tokenCaptcha)
    {
        try
        {

            string claimHash = await GetClaimHash(horse.id, tokenCaptcha);

            if (claimHash == null)
            {
                Log("Claim null");
                return;
            }

            bot.SubtractCount();
            Task.Run(() => bot.RentSharedProfit(horse.id, horse.porce, claimHash));
        }
        catch (System.Exception e)
        {
            Log("Error: Rent Horse Async");
            Log(e);
        }
    }

    private static async Task RentPayFeeHorseAsync(PegaxyBot bot, Horse horse)
    {
        try
        {
            bot.SubtractCount();
            Task.Run(() => bot.RentPay(horse.id));
        }
        catch (System.Exception e)
        {
            Log("Error: Rent Horse Async");
            Log(e);
        }
    }

    private static async void LoopRentPay()
    {
        if (Paused)
        {
            return;
        }
        Log("Loop pegaxy rent pay - iniciado");


        foreach (var horse in await GetAllHorsesPayRent())
        {
            if (true)
            {
                Log($"{horse.id} - {horse.pgx} - {horse.rentDuration}");
                // Log(horse.id);
            }
            ignoreHorsesList.Add(horse.id, TimeSpan.FromMinutes(IgnoreHorseMin));
        }

        if (DebugMode)
        {
            Log("Passed 1 - debug");
        }

        List<HorseWithDetails> newHorses;
        PegaxyBot[] botArray;
        BigInteger[] ignoreList;
        // List<string> tempCaptchaTokenList;

        while (looping)
        {
            await Task.Delay(loopDelay_ms);
            if (Paused)
            {
                continue;
            }
            if (len_botList == 0)
            {
                looping = false;
                break;
            }

            botArray = botList.ToArray();

            newHorses = await GetAllHorsesPayRent(1);
            ignoreList = ignoreHorsesList.ToArray();
            // tempCaptchaTokenList = CaptchaTokenList.ToList();

            foreach (var horse in newHorses.ToArray())
            {
                if (ignoreList.Contains(horse.id))
                {
                    newHorses.Remove(horse);
                }
            }


            foreach (var horse in newHorses)
            {
                // Console.WriteLine($"{horse.id} - {horse.pgx} - {horse.rentDuration}");
                for (int i = 0; i < botArray.Length; i++)
                {
                    var bot = botArray[i];
                    try
                    {
                        if (bot != null)
                        {
                            if (bot.Paused == false && bot.Running && bot.Count > 0)
                            {
                                if (horse.rentDuration < bot.Duration || horse.pgx > bot.PGX || horse.pega.energy < bot.Energy || bot.LegitGas() == false)
                                {
                                    continue;
                                }

                                // string tokenCaptcha = tempCaptchaTokenList[0];
                                // tempCaptchaTokenList.Remove(tokenCaptcha);
                                // CaptchaTokenList.RemoveItem(tokenCaptcha);

                                Task.Run(() => RentPayFeeHorseAsync(bot, horse));

                                break;
                            }
                        }
                    }

                    catch (System.Exception ex)
                    {
                        Log(ex);
                    }

                }
                ignoreHorsesList.Add(horse.id, TimeSpan.FromMinutes(IgnoreHorseMin));
                await Task.Delay(1);
            }
        }
        Log("Loop pegaxy - finalizado");
    }
    private static async void LoopSharedProfit()
    {
        if (Paused)
        {
            return;
        }
        Log("Loop pegaxy - iniciado");


        foreach (var horse in await GetAllHorsesSharedProfit())
        {
            if (DebugMode)
            {
                Log(horse.id);
            }
            ignoreHorsesList.Add(horse.id, TimeSpan.FromMinutes(IgnoreHorseMin));
        }

        if (DebugMode)
        {
            Log("Passed 1 - debug");
        }

        List<Horse> newHorses;
        PegaxyBot[] botArray;
        BigInteger[] ignoreList;
        List<string> tempCaptchaTokenList;

        while (looping)
        {
            await Task.Delay(loopDelay_ms);
            if (Paused)
            {
                continue;
            }
            if (len_botList == 0)
            {
                looping = false;
                break;
            }

            botArray = botList.ToArray();

            newHorses = await GetAllHorsesSharedProfit();
            ignoreList = ignoreHorsesList.ToArray();
            tempCaptchaTokenList = CaptchaTokenList.ToList();

            foreach (var horse in newHorses.ToArray())
            {
                if (ignoreList.Contains(horse.id))
                {
                    newHorses.Remove(horse);
                }
            }


            foreach (var horse in newHorses)
            {
                if (horse.porce > IgnorePercentage) { continue; }

                for (int i = 0; i < botArray.Length; i++)
                {
                    var bot = botArray[i];
                    try
                    {
                        if (tempCaptchaTokenList.Count <= 0)
                        {
                            continue;
                        }

                        if (bot != null)
                        {
                            if (bot.Paused == false && bot.Running && bot.Count > 0)
                            {
                                if (horse.porce < bot.Porcentagem || bot.LegitGas() == false)
                                {
                                    continue;
                                }

                                string tokenCaptcha = tempCaptchaTokenList[0];
                                tempCaptchaTokenList.Remove(tokenCaptcha);
                                CaptchaTokenList.RemoveItem(tokenCaptcha);

                                Task.Run(() => RentSharedProfitHorseAsync(bot, horse, tokenCaptcha));

                                break;
                            }
                        }
                    }

                    catch (System.Exception ex)
                    {
                        Log(ex);
                    }

                }
                ignoreHorsesList.Add(horse.id, TimeSpan.FromMinutes(IgnoreHorseMin));
                await Task.Delay(1);
            }
        }
        Log("Loop pegaxy - finalizado");
    }

    public static void ActiveBot(PegaxyBot bot)
    {
        bot.UnpauseBot();

        if (DesactivedBots.Contains(bot))
        {
            for (int i = 0; i < DesactivedBots.Count(x => x == bot); i++)
            {
                DesactivedBots.Remove(bot);
            }
        }
    }

    public static void DesactiveBot(PegaxyBot bot)
    {
        bot.PauseBot();
        if (DesactivedBots.Contains(bot))
        {
            return;
        }
        else
        {
            DesactivedBots.Add(bot);
        }

    }

    public static async Task RemakeAllHttpClientConnections()
    {
        SetPauseState(true);
        for (int i = 0; i < proxyGetHorseList.Count; i++)
        {
            proxyGetHorseList[i].CreateHttpClient();
        }
        Log("Proxy Get Horse carregados");

        if (RentSharedMode)
        {
            for (int i = 0; i < proxyClaimHashList.Count; i++)
            {
                proxyClaimHashList[i].CreateHttpClient();
            }
        }
        Log("Waiting 6 minutos to proxy loads");
        await Task.Delay(TimeSpan.FromMinutes(6));
        Log("Proxies carregados");
        SetPauseState(false);
    }

    public static string GetStatus()
    {

        if (String.IsNullOrEmpty(lastStatusMsg) == false)
        {
            if ((DateTimeOffset.UtcNow - lastStatusUpdate) < timeBtwStatusRequest)
            {
                return lastStatusMsg;
            }
        }

        try
        {
            var message = $"Status atualizado a cada {(int)timeBtwStatusRequest.TotalMinutes} min\n";

            message += "\n" +
            $"{(Paused ? "Bot pausado\n" : "")}\n" +
            $"Gas Atual: {(int)ActualFeeData.MaxFeePerGas}\n" +
            $"Total Cavalos Comprados: {TotalHorsesBought}\n" +
            $"Fila: {len_botList}\n" +
            "";

            lastStatusMsg = message;
            lastStatusUpdate = DateTimeOffset.UtcNow;

            return message;
        }
        catch (System.Exception e)
        {
            return $"Error: {e}";
        }
    }

    public static string GetAllQueue()
    {

        if (String.IsNullOrEmpty(lastQueueResultMsg) == false)
        {
            if ((DateTimeOffset.UtcNow - lastQueueUpdate) < timeBtwQueueRequest)
            {
                return lastQueueResultMsg;
            }
        }

        try
        {
            var message = $"Fila Atualizada a cada {timeBtwQueueRequest.TotalMinutes} minutos\n";
            var list = botList.ToArray();
            var len_list = list.Length;

            if (len_list > 0)
            {

                for (int i = 0; i < len_list; i++)
                {
                    var bot = list[i];
                    if (bot != null)
                    {
                        message += "\n" +
                        $"Posição {i + 1} {(bot.Paused ? "(Pausado)" : "")}\n" +
                        $"Porcentagem - {bot.Porcentagem} %\n" +
                        $"Gas: {bot.MaxGas}\n";
                    }
                }
            }
            else
            {
                message += "" +
                "Fila vazia!";
            }
            lastQueueResultMsg = message;
            lastQueueUpdate = DateTimeOffset.UtcNow;

            return message;
        }
        catch (System.Exception e)
        {
            return $"Error: {e}";
        }
    }

    public static string GetAllQueueWithDetails()
    {
        try
        {
            var message = "";
            var list = botList.ToArray();
            var len_list = list.Length;
            if (len_list > 0)
            {

                for (int i = 0; i < len_list; i++)
                {
                    var bot = list[i];
                    if (bot != null)
                    {
                        message += "\n" +
                        $"Queue {i + 1}\n" +
                        $"Paused - {bot.Paused}\n" +
                        $"Perc - {bot.Porcentagem} % / Count - {bot.Count}\n" +
                        $"Gas: {bot.MaxGas}\n" +
                        $"User: {bot.UserID}\n" +
                        $"BotID: {bot.ID}\n";
                    }
                }
            }
            else
            {
                message += "" +
                "Fila vazia!";
            }

            return message;
        }
        catch (System.Exception e)
        {
            return $"Error: {e}";
        }
    }

    public static int PositionQueue(PegaxyBot bot)
    {
        try
        {
            if (botList.Contains(bot))
            {
                return botList.FindIndex(x =>
                {
                    if (x != null)
                    {
                        return x.ID == bot.ID;
                    }
                    return false;
                });
            }
            return -1;
        }
        catch (System.Exception ex)
        {
            Log("Error Getting position Queue");
            Log(ex);
            return -1;
        }
    }

    public static Fee1559 GasPriceNormalizer(int gas, int porce)
    {
        decimal actualMaxFeePerGas = ActualFeeData.MaxFeePerGas * porce >= 15 ? (decimal)GasHighPorceMultipler : 1;
        decimal actualMaxPriorityFeePerGas = ActualFeeData.MaxPriorityFeePerGas * porce >= 15 ? (decimal)GasHighPorceMultipler : 1;
        if (actualMaxFeePerGas > gas)
        {
            gas = (int)(actualMaxFeePerGas + 1);
        }

        Fee1559 fee = new Fee1559();
        fee.MaxFeePerGas = Web3.Convert.ToWei(gas, EthUnit.Gwei);
        // fee.MaxPriorityFeePerGas = Web3.Convert.ToWei(((decimal)gas - actualMaxFeePerGas) + actualMaxPriorityFeePerGas, EthUnit.Gwei);
        fee.MaxPriorityFeePerGas = Web3.Convert.ToWei(gas, EthUnit.Gwei);

        return fee;
    }

    private static void Log(string message)
    {
        Console.WriteLine(message);
        Logger.WriteLog(message, managerLogName);
    }

    private static void Log(object e)
    {
        Console.WriteLine(e);
        Logger.WriteLog(e?.ToString(), managerLogName);
    }
}

public class Horse
{
    public BigInteger id { get; set; }
    public string price { get; set; }

    public float porce => float.Parse(price) / 10000;
    public float pgx => (float)Web3.Convert.FromWei(BigInteger.Parse(price));
}

public class HorseWithDetails : Horse
{
    public int rentDuration { get; set; }
    public Pega pega { get; set; }
}

public class Pega
{
    public int energy { get; set; }
}

public class ProxyContainer
{
    public string NAME { get; set; }
    public string WALLET { get; set; }
    public string SIGN { get; set; }
    public string PROXY_IP { get; set; }
    public string PROXY_PORT { get; set; }
    public string USER_AGENT { get; set; }

    public HttpClient HttpClient = null;

    public bool withSignature = true;

    public async Task CreateHttpClient()
    {
        if (HttpClient != null)
        {
            HttpClient?.CancelPendingRequests();
            HttpClient?.Dispose();
            HttpClient = null;
            await Task.Delay(TimeSpan.FromMinutes(5));
        }

        var proxy = new WebProxy();
        proxy.Address = new Uri($"socks5://{PROXY_IP.Trim()}:{PROXY_PORT.Trim()}");
        // var proxy = new HttpToSocks5Proxy(PROXY_IP, Int32.Parse(PROXY_PORT));
        var handler = new HttpClientHandler { Proxy = proxy };
        this.HttpClient = new HttpClient(handler, true);
        HttpClient.DefaultRequestHeaders.Add("accept", "application/json");
        if (this.withSignature)
        {
            HttpClient.DefaultRequestHeaders.Add("x-user-address", WALLET?.Trim());
            HttpClient.DefaultRequestHeaders.Add("x-user-signature", SIGN?.Trim());
        }
        HttpClient.DefaultRequestHeaders.Add("origin", "https://play.pegaxy.io");
        // HttpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        HttpClient.DefaultRequestHeaders.Add("TE", "trailers");
        HttpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        HttpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-site");
        HttpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
        HttpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
        HttpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US");
        HttpClient.DefaultRequestHeaders.Add("referer", "https://play.pegaxy.io/renting?tab=share-profit");
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(this.USER_AGENT);
        Console.WriteLine($"CONTAINER: {PROXY_IP} CRIADO");
    }
}


public class FeeData
{
    public decimal MaxFeePerGas;
    public decimal MaxPriorityFeePerGas;
    public decimal BaseFee;

    public FeeData(Fee1559 fee)
    {
        this.MaxFeePerGas = Web3.Convert.FromWei(fee.MaxFeePerGas.Value, EthUnit.Gwei);
        this.MaxPriorityFeePerGas = Web3.Convert.FromWei(fee.MaxPriorityFeePerGas.Value, EthUnit.Gwei);
        this.BaseFee = Web3.Convert.FromWei(fee.BaseFee.Value, EthUnit.Gwei);
    }
}

public class TempList<T> : IDisposable
{
    public int DelayMs { get; private set; }
    public List<CacheItem<T>> List { get; private set; }
    private Timer ExpireTimer;

    public TempList(int delayMs = 60000)
    {
        List = new List<CacheItem<T>>();
        DelayMs = delayMs;

        CreateExpireTimer();
        ExpireTimer?.Start();
    }

    public void Add(T value, TimeSpan expireTimeAfter)
    {
        List.Add(new CacheItem<T>(value, expireTimeAfter));
    }

    private void CreateExpireTimer()
    {
        ExpireTimer = new Timer(DelayMs); // Set the time (5 mins in this case)
        ExpireTimer.AutoReset = true;
        ExpireTimer.Elapsed += new ElapsedEventHandler((object sender, ElapsedEventArgs e) => RemoveAllExpired());
    }

    private void RemoveAllExpired()
    {
        try
        {
            var currentDateTime = DateTimeOffset.UtcNow;
            // ToArray prevents modifying an iterated collection.
            foreach (var item in List.ToArray())
            {
                if (currentDateTime - item.CreatedDate > item.ExpiresAfter)
                {
                    RemoveFromCache(item);
                }
            }
        }
        catch (System.Exception e)
        {
            Console.WriteLine(e);
            // throw e;
        }
    }

    public List<T> ToList()
    {
        var returnList = new List<T>();
        foreach (var item in List.ToArray())
        {
            if (item == null) continue;
            returnList.Add(item.Value);
        }

        return returnList;
    }

    public T[] ToArray()
    {
        return ToList().ToArray();
    }

    public void RemoveFromCache(CacheItem<T> obj)
    {
        if (obj == null) return;
        List?.Remove(obj);
    }

    public void RemoveItem(T value)
    {
        foreach (var item in List.ToArray())
        {
            if (item == null) continue;
            if (item.Value.Equals(value))
            {
                RemoveFromCache(item);
                break;
            }
        }
    }

    public void Dispose()
    {
        ExpireTimer?.Stop();
        ExpireTimer = null;
        List.Clear();
        List = null;
    }
}