using System;
using Newtonsoft.Json;


namespace TradingBot.Data
{
    public class SafeLaunchData
    {
        [JsonConverter(typeof(BooleanJsonConverter))]
        public bool PRIVATE_DEAL = false;
        [JsonConverter(typeof(BooleanJsonConverter))]
        public bool NFT = false;
        [JsonConverter(typeof(BooleanJsonConverter))]
        public bool SPAM_MODE = false;

        public string CONTRACT = null;

        [JsonConverter(typeof(DateJsonConverter))]
        public DateTimeOffset DATE;

        public float WAIT_SECONDS = 0f;
        public float LOOP_DELAY = 1f;

        public float AMOUNT = -1f;

        public float GAS_PRICE = 14f;
        public int GAS_LIMIT = 225000;

        public int SALE_ID = 0;

        [JsonConverter(typeof(BooleanJsonConverter))]
        public bool FORCE_BUY_MIN = false;
        
        [JsonConverter(typeof(BooleanJsonConverter))]
        public bool DEBUG = false;
    }
}