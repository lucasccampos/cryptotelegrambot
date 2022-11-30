using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Web3;
using System;

namespace TradingBot.Functions
{
    [Function("allowance", "uint256")]
    public class GetAllowance : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("address", "spender", 2)]
        public virtual string Spender { get; set; }
    }

    [Function("0x313ce567", "uint8")]
    public class Test : FunctionMessage
    {
    }

    // [Function("rounds", "uint256")]
    [FunctionOutput]
    public class OracleData : IFunctionOutputDTO
    {
        [Parameter("uint80", "roundId", 1)]
        public virtual BigInteger roundId { get; set; }

        [Parameter("int256", "answer", 2)]
        public virtual BigInteger answer { get; set; }

        [Parameter("uint256", "startedAt", 3)]
        public virtual BigInteger startedAt { get; set; }

        [Parameter("uint256", "updatedAt", 4)]
        public virtual BigInteger updatedAt { get; set; }

        [Parameter("uint80", "answeredInRound", 5)]
        public virtual BigInteger answeredInRound { get; set; }

    }

    public interface IRound
    {
        BigInteger epoch { get; set; }

        BigInteger startTimestamp { get; set; }
        DateTimeOffset StartTimestamp => DateTimeOffset.FromUnixTimeSeconds((long)startTimestamp);

        BigInteger lockTimestamp { get; set; }
        DateTimeOffset LockTimestamp => DateTimeOffset.FromUnixTimeSeconds((long)lockTimestamp);

        BigInteger closeTimestamp { get; set; }
        DateTimeOffset CloseTimestamp => DateTimeOffset.FromUnixTimeSeconds((long)closeTimestamp);

        BigInteger lockPrice { get; set; }
        // decimal LockPrice => Web3.Convert.FromWei(lockPrice);

        BigInteger closePrice { get; set; }
        // decimal ClosePrice => Web3.Convert.FromWei(closePrice);

        BigInteger totalAmount { get; set; }
        decimal TotalAmount => Web3.Convert.FromWei(totalAmount);

        BigInteger bullAmount { get; set; }
        decimal BullAmount => Web3.Convert.FromWei(bullAmount);

        BigInteger bearAmount { get; set; }
        decimal BearAmount => Web3.Convert.FromWei(bearAmount);

        BigInteger rewardBaseCalAmount { get; set; }
        decimal RewardBaseCalAmount => Web3.Convert.FromWei(rewardBaseCalAmount);

        BigInteger rewardAmount { get; set; }
        decimal RewardAmount => Web3.Convert.FromWei(rewardAmount);

        decimal RewardMultiplier => rewardBaseCalAmount > 0 ? RewardAmount / RewardBaseCalAmount : GetBiggerMultiplier();

        // decimal GetWinnerMultiplier() => closePrice > lockPrice ? PredictionSide.BULL : PredictionSide.BEAR;
        PredictionSide GetWinnerSide() => closePrice > lockPrice ? PredictionSide.BULL : PredictionSide.BEAR;
        PredictionSide GetWinner(BigInteger price) => price > lockPrice ? PredictionSide.BULL : PredictionSide.BEAR;
        PredictionSide GetMostProfitbleSide() => bearAmount > bullAmount ? PredictionSide.BULL : PredictionSide.BEAR;
        decimal GetBiggerMultiplier()
        {
            if (totalAmount == 0 || bearAmount == 0 || bullAmount == 0)
            {
                return 1;
            }

            if (GetMostProfitbleSide() == PredictionSide.BULL)
            {
                return (TotalAmount / BullAmount);
            }
            else
            {
                return (TotalAmount / BearAmount);
            }
        }

        bool isClosed { get; }
        string StringData => $"epoch:{this.epoch}/bullAmount:{this.BullAmount}/bearAmount:{this.BearAmount}/lockPrice:{this.lockPrice}/closePrice:{this.closePrice}/startTime:{this.startTimestamp}/lockTime:{this.lockTimestamp}/closeTime:{this.closeTimestamp}";
    }

    [FunctionOutput]
    public class RoundDataPancake : IFunctionOutputDTO, IRound
    {
        [Parameter("uint256", "epoch", 1)]
        public virtual BigInteger epoch { get; set; }

        [Parameter("uint256", "startTimestamp", 2)]
        public virtual BigInteger startTimestamp { get; set; }
        // public virtual DateTimeOffset StartTimestamp => DateTimeOffset.FromUnixTimeSeconds((long)startTimestamp);

        [Parameter("uint256", "lockTimestamp", 3)]
        public virtual BigInteger lockTimestamp { get; set; }
        // public virtual DateTimeOffset LockTimestamp => DateTimeOffset.FromUnixTimeSeconds((long)lockTimestamp);

        [Parameter("uint256", "closeTimestamp", 4)]
        public virtual BigInteger closeTimestamp { get; set; }
        // public virtual DateTimeOffset CloseTimestamp => DateTimeOffset.FromUnixTimeSeconds((long)closeTimestamp);

        [Parameter("uint256", "lockPrice", 5)]
        public virtual BigInteger lockPrice { get; set; }
        // public virtual decimal LockPrice => Web3.Convert.FromWei(lockPrice);

        [Parameter("uint256", "closePrice", 6)]
        public virtual BigInteger closePrice { get; set; }
        // public virtual decimal ClosePrice => Web3.Convert.FromWei(closePrice);

        [Parameter("uint256", "lockOracleId", 7)]
        public virtual BigInteger lockOracleId { get; set; }

        [Parameter("uint256", "closeOracleId", 8)]
        public virtual BigInteger closeOracleId { get; set; }

        [Parameter("uint256", "totalAmount", 9)]
        public virtual BigInteger totalAmount { get; set; }
        // public virtual decimal TotalAmount => Web3.Convert.FromWei(totalAmount);

        [Parameter("uint256", "bullAmount", 10)]
        public virtual BigInteger bullAmount { get; set; }
        // public virtual decimal BullAmount => Web3.Convert.FromWei(bullAmount);

        [Parameter("uint256", "bearAmount", 11)]
        public virtual BigInteger bearAmount { get; set; }
        // public virtual decimal BearAmount => Web3.Convert.FromWei(bearAmount);

        [Parameter("uint256", "rewardBaseCalAmount", 12)]
        public virtual BigInteger rewardBaseCalAmount { get; set; }

        [Parameter("uint256", "rewardAmount", 13)]
        public virtual BigInteger rewardAmount { get; set; }

        [Parameter("bool", "oracleCalled", 14)]
        public virtual bool oracleCalled { get; set; }

        public bool isClosed => oracleCalled && closePrice > 0;

        public override string ToString()
        {
            return ((IRound)this).StringData;
        }

        // public PredictionSide GetWinnerSide() => closePrice > lockPrice ? PredictionSide.BULL : PredictionSide.BEAR;
        // public PredictionSide GetWinner(BigInteger price) => price > lockPrice ? PredictionSide.BULL : PredictionSide.BEAR;
        // public PredictionSide GetMostProfitbleSide() => bearAmount > bullAmount ? PredictionSide.BULL : PredictionSide.BEAR;
        // public decimal GetBiggerMultiplier() => GetMostProfitbleSide() == PredictionSide.BULL ? (BullAmount / TotalAmount) : (BearAmount / TotalAmount);
    }

    [FunctionOutput]
    public class RoundDataCandleGenie : IFunctionOutputDTO, IRound
    {
        [Parameter("uint256", "epoch", 1)]
        public virtual BigInteger epoch { get; set; }

        [Parameter("uint256", "bullAmount", 2)]
        public virtual BigInteger bullAmount { get; set; }
        // public virtual decimal BullAmount => Web3.Convert.FromWei(bullAmount);

        [Parameter("uint256", "bearAmount", 3)]
        public virtual BigInteger bearAmount { get; set; }
        // public virtual decimal BearAmount => Web3.Convert.FromWei(bearAmount);

        [Parameter("uint256", "rewardBaseCalAmount", 4)]
        public virtual BigInteger rewardBaseCalAmount { get; set; }

        [Parameter("uint256", "rewardAmount", 5)]
        public virtual BigInteger rewardAmount { get; set; }

        [Parameter("int256", "lockPrice", 6)]
        public virtual BigInteger lockPrice { get; set; }
        // public virtual decimal LockPrice => Web3.Convert.FromWei(lockPrice);

        [Parameter("int256", "closePrice", 7)]
        public virtual BigInteger closePrice { get; set; }
        // public virtual decimal ClosePrice => Web3.Convert.FromWei(closePrice);

        [Parameter("uint32", "startTimestamp", 8)]
        public virtual BigInteger startTimestamp { get; set; }
        // public virtual DateTimeOffset StartTimestamp => DateTimeOffset.FromUnixTimeSeconds((long)startTimestamp);

        [Parameter("uint32", "lockTimestamp", 9)]
        public virtual BigInteger lockTimestamp { get; set; }
        // public virtual DateTimeOffset LockTimestamp => DateTimeOffset.FromUnixTimeSeconds((long)lockTimestamp);

        [Parameter("uint32", "closeTimestamp", 10)]
        public virtual BigInteger closeTimestamp { get; set; }
        // public virtual DateTimeOffset CloseTimestamp => DateTimeOffset.FromUnixTimeSeconds((long)closeTimestamp);

        [Parameter("uint32", "lockPriceTimestamp", 11)]
        public virtual BigInteger lockPriceTimestamp { get; set; }

        [Parameter("uint32", "closePriceTimestamp", 12)]
        public virtual BigInteger closePriceTimestamp { get; set; }

        [Parameter("bool", "closed", 13)]
        public virtual bool closed { get; set; }
        public bool isClosed => closed;

        [Parameter("bool", "cancelled", 14)]
        public virtual bool cancelled { get; set; }

        public virtual BigInteger totalAmount
        {
            get { return bearAmount + bullAmount; }
            set { }
        }

        public override string ToString()
        {
            return ((IRound)this).StringData;
        }
        // public virtual decimal TotalAmount => Web3.Convert.FromWei(totalAmount);

        // public PredictionSide GetWinnerSide() => closePrice > lockPrice ? PredictionSide.BULL : PredictionSide.BEAR;
        // public PredictionSide GetWinner(BigInteger price) => price > lockPrice ? PredictionSide.BULL : PredictionSide.BEAR;
        // public PredictionSide GetMostProfitbleSide() => bearAmount > bullAmount ? PredictionSide.BULL : PredictionSide.BEAR;
        // public decimal GetBiggerMultiplier() => GetMostProfitbleSide() == PredictionSide.BULL ? (BullAmount / TotalAmount) : (BearAmount / TotalAmount);
    }

    [Function("addLiquidityETH", "uint256")]
    public class AddLiquidityETHFunction : FunctionMessage
    {
        [Parameter("address", "token", 1)]
        public virtual string token { get; set; }

        [Parameter("uint256", "amountTokenDesired", 2)]
        public virtual BigInteger amountTokenDesired { get; set; }

        [Parameter("uint256", "amountTokenMin", 3)]
        public virtual BigInteger amountTokenMin { get; set; }

        [Parameter("uint256", "amountETHMin", 4)]
        public virtual BigInteger amountETHMin { get; set; }

        [Parameter("address", "to", 5)]
        public virtual string to { get; set; }

        [Parameter("uint256 ", "deadline", 6)]
        public virtual BigInteger deadline { get; set; }

        public AddLiquidityETHFunction() { }
        public AddLiquidityETHFunction(List<Nethereum.ABI.FunctionEncoding.ParameterOutput> lista)
        {
            this.token = (string)lista[0].Result;
            this.amountTokenDesired = (BigInteger)lista[1].Result;
            this.amountTokenMin = (BigInteger)lista[2].Result;
            this.amountETHMin = (BigInteger)lista[3].Result;
            this.to = (string)lista[4].Result;
            this.deadline = (BigInteger)lista[5].Result;
        }
    }

    [Function("addLiquidity", "uint256")]
    public class AddLiquidityFunction : FunctionMessage
    {
        [Parameter("address", "tokenA", 1)]
        public virtual string tokenA { get; set; }

        [Parameter("address", "tokenB", 2)]
        public virtual string tokenB { get; set; }

        [Parameter("uint256", "amountADesired", 3)]
        public virtual BigInteger amountADesired { get; set; }

        [Parameter("uint256", "amountBDesired", 4)]
        public virtual BigInteger amountBDesired { get; set; }

        [Parameter("uint256", "amountAMin", 5)]
        public virtual BigInteger amountAMin { get; set; }

        [Parameter("uint256", "amountBMin", 6)]
        public virtual BigInteger amountBMin { get; set; }

        [Parameter("address", "to", 7)]
        public virtual string to { get; set; }

        [Parameter("uint256 ", "deadline", 8)]
        public virtual BigInteger deadline { get; set; }

        public AddLiquidityFunction() { }
        public AddLiquidityFunction(List<Nethereum.ABI.FunctionEncoding.ParameterOutput> lista)
        {
            this.tokenA = (string)lista[0].Result;
            this.tokenB = (string)lista[1].Result;
            this.amountADesired = (BigInteger)lista[2].Result;
            this.amountBDesired = (BigInteger)lista[3].Result;
            this.amountAMin = (BigInteger)lista[4].Result;
            this.amountBMin = (BigInteger)lista[5].Result;
            this.to = (string)lista[6].Result;
            this.deadline = (BigInteger)lista[7].Result;
        }
    }

    [FunctionOutput]
    public class UserRounds : IFunctionOutputDTO
    {
        [Parameter("uint256[]", 1)]
        public virtual List<BigInteger> rounds { get; set; }

        [Parameter("tuple[]", 2)]
        public virtual List<BetInfo> roundsInfo { get; set; }

        [Parameter("uint256", 3)]
        public virtual BigInteger quantity { get; set; }
    }

    public class BetInfo
    {
        [Parameter("uint8", "position", 1)]
        public virtual int position { get; set; }
        [Parameter("uint256", "amount", 2)]
        public virtual BigInteger amount { get; set; }
        [Parameter("bool", "claimed", 3)]
        public virtual bool claimed { get; set; }

    }

    public class TryAggregate
    {
        [Parameter("bool", "requireSuccess", 1)]
        public virtual bool RequireSuccess { get; set; }

        [Parameter("tuple[]", "calls", 2)]
        public virtual List<Call> Calls { get; set; }
    }

    public class Call
    {
        [Parameter("address", "target", 1)]
        public virtual string Target { get; set; }

        [Parameter("bytes", "callData", 2)]
        public virtual byte[] CallData { get; set; }
    }
}