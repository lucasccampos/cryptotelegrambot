using Newtonsoft.Json;

namespace TradingBot.Data
{
    public class TradeData
    {
        [JsonConverter(typeof(TokenBSCJsonConverter))]
        public string TOKEN = "";

        public float GAS_PRICE = 5f;
        public int GAS_LIMIT = 500000;

        [JsonConverter(typeof(BooleanJsonConverter))]
        public bool APPROVE_TOKEN_AFTER_BUY = false;
        [JsonConverter(typeof(BooleanJsonConverter))]
        public bool CHECK_RUG = false;
        public float MIN_LIQUIDY = 1f;
        public float WAIT_SECONDS = 0.0f;

        public int SPLIT = 1;
        public float TIME_BTW_SPLIT = 0f;

        [JsonConverter(typeof(BooleanJsonConverter))]
        public bool BUY = false;
        public float AMOUNT_TO_BUY = 0.00f;
        public float MAX_PRICE_BUY = 0.0f;

        [JsonConverter(typeof(BooleanJsonConverter))]
        public bool SELL = false;
        public float AMOUNT_TO_SELL = 0.0f;
        public float MIN_PRICE_SELL = 100000f;
        public float MIN_AMOUNT_TO_RECEIVE = 0f;
        public float TXN_MAX_DURATION_MINUTES = 10f;

        [JsonConverter(typeof(TokenBSCJsonConverter))]
        public string BUY_USING_TOKEN = null;
        [JsonConverter(typeof(TokenBSCJsonConverter))]
        public string RECEIVE_IN_TOKEN = null;
        [JsonConverter(typeof(TokenBSCJsonConverter))]
        public string MAIN_TOKEN_RPC = Tokens.WBNB;
        [JsonConverter(typeof(TokenBSCJsonConverter))]
        public string LIQUIDITY_TOKEN = Tokens.WBNB;

        [JsonConverter(typeof(TokenBSCJsonConverter))]
        public string USD_TOKEN = Tokens.USDT;

        [JsonConverter(typeof(DexBSCJsonConverter))]
        public DexData dexData = DexBSCJsonConverter.PANCAKESWAP;
    }
}
