using Newtonsoft.Json;

namespace TradingBot.Data
{
    public class ConfigData
    {
        [JsonConverter(typeof(BooleanJsonConverter))]
        public bool USE_BOTH_RPC = true;
        [JsonConverter(typeof(BooleanJsonConverter))]
        public bool USE_WS = true;
        public string RPC_WS = "ws://127.0.0.1:8546";
        public string RPC_HTTP = "http://127.0.0.1:8545";
        public string POLYGON_RPC_HTTP = "https://polygon-rpc.com";
        public int TRACK_BNB_PRICE_MS = 350;
        public int TRACK_TOKEN_PRICE_MS = 5;
        public int MAIN_LOOP_MS = 5;
        // public int FIND_PAIR_MS = 30;
        // TRACK_NONCE_MS = 2.5;
        // MINIMAL= false;
        // START_AFTER_ENTER=false;
    }
}