using Newtonsoft.Json;


namespace TradingBot.Data
{
    public class PredictionData
    {
        public float INICIAL_BET = 0.001f;
        public float MULTIPLIER_LOSS = 2f;
        public int MAX_SEQUENCIAL = 4;
        public float SECONDS_BEFORE_BET = 7f;
        public float SECONDS_AFTER_ROUND_CLOSE = 50f;
        public float GAS_PRICE = 14f;
        public int GAS_LIMIT = 225000;
        public int WINS_PER_DAY = 1;

        [JsonConverter(typeof(PredictionBSCJsonConverter))]
        public string PREDICTION_ADDRESS = "0x18B2A687610328590Bc8F2e5fEdDe3b582A49cdA";

        [JsonConverter(typeof(BooleanJsonConverter))]
        public bool START_AT_END_DAY = false;
        [JsonConverter(typeof(BooleanJsonConverter))]
        public bool RESET_BET_END_DAY = true;

        [JsonConverter(typeof(BooleanJsonConverter))]
        public bool DEBUG = false;
    }
}