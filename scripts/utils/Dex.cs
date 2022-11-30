using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;
using TradingBot.Functions;

namespace TradingBot.Utils
{
    public class Dex
    {

        public Dex(Contract dexContract)
        {
            CreateFunctions(dexContract);
        }

        private Function func_swapExactETHForTokensSupportingFee;
        private Function func_swapExactTokensForTokensSupportingFee;

        public void CreateFunctions(Contract dexContract)
        {
            func_swapExactETHForTokensSupportingFee = dexContract.GetFunction("swapExactETHForTokensSupportingFeeOnTransferTokens");
            func_swapExactTokensForTokensSupportingFee = dexContract.GetFunction("swapExactTokensForTokensSupportingFeeOnTransferTokens");
        }

        public async Task<string> swapExactETHForTokensSupportingFeeOnTransferTokens(
            BigInteger amountIN, BigInteger amountOutMin, string[] path, string from, float deadLineMinutes, HexBigInteger gasLimit, HexBigInteger gasPrice)
        {
            if (func_swapExactETHForTokensSupportingFee == null) return null;

            object[] paramters = new object[] {
                amountOutMin,
                path,
                from,
                new BigInteger(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (deadLineMinutes*60))
            };

            return await func_swapExactETHForTokensSupportingFee.SendTransactionAsync(from: from, gas: gasLimit, gasPrice: gasPrice, value: new HexBigInteger(amountIN), paramters);
        }

        public async Task<string> swapExactTokensForTokensSupportingFeeOnTransferTokens(
            BigInteger amountIN, BigInteger amountOutMin, string[] path, string from, float deadLineMinutes, HexBigInteger gasLimit, HexBigInteger gasPrice)
        {
            if (func_swapExactTokensForTokensSupportingFee == null) return null;

            object[] paramters = new object[] {
                amountIN,
                amountOutMin,
                path,
                from,
                new BigInteger(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (deadLineMinutes*60))
            };

            return await func_swapExactTokensForTokensSupportingFee.SendTransactionAsync(from: from, gas: gasLimit, gasPrice: gasPrice, value: new HexBigInteger(0), paramters);
        }

    }
}