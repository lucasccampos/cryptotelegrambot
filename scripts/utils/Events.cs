using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace TradingBot.Events
{
    [Event("PairCreated")]
    public class PairCreatedEventDTO : IEventDTO
    {
        [Parameter("address", "token0", 1, true)]
        public virtual string Token0 { get; set; }

        [Parameter("address", "token1", 2, true)]
        public virtual string Token1 { get; set; }

        [Parameter("address", "pair", 3, false)]
        public virtual string Pair { get; set; }

        [Parameter("uint256", "", 4, false)]
        public virtual BigInteger sla { get; set; }
    }

    [Event("Swap")]
    public class SwapEventDTOBase : IEventDTO
    {
        [Parameter("address", "sender", 1, true)]
        public virtual string Sender { get; set; }
        [Parameter("uint256", "amount0In", 2, false)]
        public virtual BigInteger Amount0In { get; set; }
        [Parameter("uint256", "amount1In", 3, false)]
        public virtual BigInteger Amount1In { get; set; }
        [Parameter("uint256", "amount0Out", 4, false)]
        public virtual BigInteger Amount0Out { get; set; }
        [Parameter("uint256", "amount1Out", 5, false)]
        public virtual BigInteger Amount1Out { get; set; }
        [Parameter("address", "to", 6, true)]
        public virtual string To { get; set; }
    }
}