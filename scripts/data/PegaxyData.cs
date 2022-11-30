using Nethereum.RPC.Fee1559Suggestions;
using Newtonsoft.Json;


namespace TradingBot.Data
{
    public class PegaxyData
    {
        public float PORCENTAGEM = 1f;
        public int COUNT = 1;
        public int DURATION = 0;
        public int PGX = 0;
        public int ENERGY = 0;
        public Fee1559 FEE;
        public int GAS_LIMIT = 400000;

        [JsonConverter(typeof(BooleanJsonConverter))]
        public bool DEBUG = false;

        public ContaData ContaData;
    }
}