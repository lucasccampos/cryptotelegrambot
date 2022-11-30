using System;
using System.Threading.Tasks;
using Nethereum.Contracts;
using System.Numerics;
using System.Collections.Generic;

namespace TradingBot.Utils
{
    public class TokenTracker : ITokenTracker
    {
        public double CurrentPrice { get; private set; }
        public string BaseToken { get; private set; }
        public string SwapToken { get; private set; }
        public string Dex { get; private set; }
        public bool trackPrice { get; private set; }
        public bool IsWorking => trackPrice;
        public string Symbol => symbol;

        private Contract DexContract { get; set; }
        private BigInteger AmountIn { get; set; }
        private int DelayMs { get; set; }

        public int decimals;
        private string symbol = "TOKEN";

        private string logName { get; set; }

        private void Log(string message)
        {
            Logger.WriteLog(message, this.logName);
        }

        public TokenTracker(string baseToken, string swapToken, string dex, Contract tokenContract, string logName, int? decimals = null)
        {
            this.logName = logName;

            this.BaseToken = baseToken;
            this.SwapToken = swapToken;
            this.Dex = dex;

            if (decimals.HasValue == false)
            {
                this.FindDecimals(tokenContract).Wait();
            }
            else
            {
                this.decimals = decimals.Value;
            }

            this.FindSymbol(tokenContract);
        }

        public async Task FindDecimals(Contract tokenContract)
        {

            int tries = 3;
            while (tries > 0)
            {
                try
                {
                    var func_decimals = tokenContract.GetFunction("decimals");
                    this.decimals = (int)await func_decimals.CallAsync<BigInteger>(new object[0]);
                    tries = 0;
                    break;
                }
                catch (Exception e)
                {
                    tries -= 1;
                    Log($"ERROR FINDING DECIMALS : {e.StackTrace}");
                }
            }

        }

        public async Task FindSymbol(Contract tokenContract)
        {

            int tries = 3;
            while (tries > 0)
            {
                try
                {
                    var func_symbol = tokenContract.GetFunction("symbol");
                    this.symbol = await func_symbol.CallAsync<string>(new object[0]);
                    tries = 0;
                    break;
                }
                catch (Exception e)
                {
                    tries -= 1;
                    Log($"ERROR FINDING SYMBOL : {e.StackTrace}");
                }
            }

        }

        public async void TrackPrice(Contract dexContract, BigInteger amountIn, int delayMs)
        {
            this.trackPrice = true;
            if (dexContract == null)
            {
                throw new Exception("DexContract == null");
            }
            DexContract = dexContract;
            AmountIn = amountIn;
            DelayMs = delayMs;

            Log($"Tracking started\nBase:{BaseToken}\nSwapToken:{SwapToken}\nDex:{Dex}");
            var func_getAmountsOut = dexContract.GetFunction("getAmountsOut");
            var func_getAmountsIn = dexContract.GetFunction("getAmountsIn");
            object[] paramters = new object[2] { amountIn, new string[2] { BaseToken, SwapToken } };

            double decimal_normalize = MathF.Pow(10, 18 - this.decimals);

            while (this.trackPrice)
            {
                try
                {
                    List<BigInteger> amountsResult = await func_getAmountsOut.CallAsync<List<BigInteger>>(paramters);
                    this.CurrentPrice = ((double)amountsResult[0] / decimal_normalize) / (double)amountsResult[1];

                    // Log(CurrentPrice.ToString());
                }
                catch (Nethereum.JsonRpc.Client.RpcResponseException e)
                {
                    Log($"ERROR(trackPrice) {e}");
                    Log(e.StackTrace);

                    await Task.Delay(100);
                    try
                    {
                        await func_getAmountsIn.CallAsync<List<BigInteger>>(paramters);
                        Log("Call amount in");

                    }
                    catch (System.Exception ee)
                    {
                        Log($"ERROR2(trackPrice) {ee}");
                        Log(ee.StackTrace);
                    }
                }
                catch (DivideByZeroException e)
                {
                    Console.WriteLine("Exception caught: {0}", e.StackTrace);
                    Log("aaa");
                }
                catch (Exception e)
                {
                    Log($"ERROR(trackPrice) {e}");
                    Log(e.StackTrace);

                    await Task.Delay(100);
                    await func_getAmountsIn.CallAsync<List<BigInteger>>(paramters);
                    Log("Call amount in");
                }

                await Task.Delay(delayMs);
            }
            Log("Tracking Price has stopped");
        }

        public void StopTrackingPrice()
        {
            this.trackPrice = false;
            this.CurrentPrice = 0f;
        }

        public void Restart()
        {
            if (IsWorking == false)
            {
                Log("Restarted");
                TrackPrice(DexContract, AmountIn, DelayMs);
            }
        }
    }

    public class TokenTrackerFixed : ITokenTracker
    {
        public double CurrentPrice { get; private set; }
        public string BaseToken { get; private set; }
        public string SwapToken { get; private set; }
        public string Dex { get; private set; }
        public string Symbol => "USD";
        public bool IsWorking => true;

        public TokenTrackerFixed(string baseToken, string swapToken, string dex, double fixedPrice)
        {
            this.BaseToken = baseToken;
            this.SwapToken = swapToken;
            this.CurrentPrice = fixedPrice;
        }

        // public bool trackPrice { get; private set; }



        public void TrackPrice(Contract dexContract, BigInteger amountIn, int delayMs) { }
        public void StopTrackingPrice() { }
        public void Restart() { }
    }

    public interface ITokenTracker
    {
        string BaseToken { get; }
        string SwapToken { get; }
        double CurrentPrice { get; }
        string Symbol { get; }
        string Dex { get; }
        bool IsWorking { get; }

        void TrackPrice(Contract dexContract, BigInteger amountIn, int delayMs);
        void StopTrackingPrice();
        void Restart();
    }
}
