using System.Collections.Generic;
using System.IO;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json;
using TradingBot.Data;
using TradingBot.Utils;


public static class TokenTrackerManager
{
    private static Dictionary<string, Dictionary<string, SharedToken>> dictTokens;
    private static System.Numerics.BigInteger amountIn;
    private static int delayTrackMs;
    private static string RPC_HTTP;

    private static Account account;

    static TokenTrackerManager()
    {
        dictTokens = new Dictionary<string, Dictionary<string, SharedToken>>();
        amountIn = Web3.Convert.ToWei(0.0001);

        string configString = File.ReadAllText("data/config.json");
        var config = JsonConvert.DeserializeObject<ConfigData>(configString);
        delayTrackMs = config.TRACK_TOKEN_PRICE_MS;
        RPC_HTTP = config.RPC_HTTP;
    }

    public static void StartManager()
    {
        var stableList = new string[] { Tokens.BUSD, Tokens.USDT };

        string dex = DexBSCJsonConverter.PANCAKESWAP.ROUTER;
        foreach (var stableToken in stableList)
        {
            string key = GenerateKey(stableToken, Tokens.WBNB, dex);
            AddToDict(dex, key, new SharedToken(new TokenTrackerFixed(Tokens.WBNB, stableToken, dex, 1.0), 10));
        }

        System.Console.WriteLine("Bnb price disabled");
        // GetToken(Tokens.WBNB, Tokens.USDT, dex, 10);
    }

    private static void AddToDict(string dex, string key, SharedToken sharedToken)
    {
        if (dictTokens.TryGetValue(dex, out var sharedDict))
        {
            sharedDict.Add(key, sharedToken);
        }
        else
        {
            sharedDict = new Dictionary<string, SharedToken>();
            sharedDict.Add(key, sharedToken);
            dictTokens.Add(dex, sharedDict);
        }
    }

    private static string GenerateKey(string token, string pair, string dex)
    {
        return token + pair + dex;
    }

    public static ITokenTracker GetToken(string token, string pair, string dex, int count = 1)
    {
        string key = GenerateKey(token, pair, dex);

        if (!dictTokens.TryGetValue(dex, out var sharedDict))
        {
            sharedDict = new Dictionary<string, SharedToken>();
            dictTokens.Add(dex, sharedDict);
        }

        SharedToken sharedToken;
        if (!sharedDict.TryGetValue(key, out sharedToken))
        {
            var web3 = new Web3(RPC_HTTP);
            web3.TransactionManager.UseLegacyAsDefault = true;
            var tracker = new TokenTracker(pair, token, dex, web3.Eth.GetContract(ABI.ERC20_ABI, token), $"TRACKER_{key}");
            tracker.TrackPrice(web3.Eth.GetContract(ABI.DEX_ABI, dex), amountIn, 3);
            sharedToken = new SharedToken(tracker);
            sharedDict.Add(key, sharedToken);
        }
        else
        {
            if (sharedToken.Tracker.IsWorking == false)
            {
                sharedToken.Tracker.Restart();
            }
        }

        sharedToken.BotsUsing += count;
        return sharedToken.Tracker;
    }

    public static void RemoveToken(ITokenTracker tracker)
    {
        if (tracker == null)
        {
            System.Console.WriteLine("TokenTrackerManager: Tracker null");
            return;
        }
        RemoveToken(tracker.SwapToken, tracker.BaseToken, tracker.Dex);
    }

    public static void RemoveToken(string token, string pair, string dex)
    {
        string key = GenerateKey(token, pair, dex);

        if (dex == null)
        {
            System.Console.WriteLine("TokenTrackerManager: Dex null");
            return;
        }

        if (dictTokens.TryGetValue(dex, out var sharedDict))
        {
            if (key == null)
            {
                System.Console.WriteLine("TokenTrackerManager: Key null");
                return;
            }
            if (sharedDict.TryGetValue(key, out var sharedToken))
            {
                if (sharedToken != null)
                {
                    sharedToken.BotsUsing -= 1;

                    if (sharedToken.BotsUsing <= 0)
                    {
                        sharedToken.Tracker?.StopTrackingPrice();
                        sharedToken = null;
                        sharedDict.Remove(key);
                    }
                }
            }
        }
    }

    public class SharedToken
    {
        public int BotsUsing;
        public ITokenTracker Tracker;

        public SharedToken(ITokenTracker tracker)
        {
            this.Tracker = tracker;
            this.BotsUsing = 0;
        }

        public SharedToken(ITokenTracker tracker, int botsUsing)
        {
            this.Tracker = tracker;
            this.BotsUsing = botsUsing;
        }

    }
}