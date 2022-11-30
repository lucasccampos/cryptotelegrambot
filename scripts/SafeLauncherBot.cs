using Nethereum.Web3;
using System;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Nethereum.Hex.HexTypes;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Numerics;
using TradingBot.Data;

using Newtonsoft.Json;
using TradingBot.Functions;
using System.Threading;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using TradingBot.Utils;

namespace TradingBot
{
    public class SafeLauncherBot : Bot
    {
        private Contract launchContract;

        private Account account;
        public Web3 web3_http;

        private Function func_Contribute;
        private Function func_Purchase;
        private Function func_getPurchaseLimit;
        private Function func_StartAt;
        private Function func_GetMaxContribution;
        private Function func_GetTotalRaised;

        private HexBigInteger gasPrice;
        private HexBigInteger gasLimit;

        private float baseWaitSeconds = 300f;
        private float waitSeconds;
        private DateTimeOffset launchDate;
        private BigInteger AMOUNT_TO_BUY;
        private BigInteger SALE_ID;
        private bool forceMinBuy = false;
        private bool buyMax = false;

        private TimeSpan loop_delay;

        private bool Running = false;
        private string logName;

        private CancellationTokenSource token_source_loop;
        private CancellationToken ct_loop;
        private CancellationTokenSource token_source_wait;
        private CancellationToken ct_wait;

        private SafeLaunchData data;
        private bool forceStop = false;

        private int delayCount = 0;
        private int spamCount = 0;

        public SafeLauncherBot(ConfigData configData, PrivateData privateData, SafeLaunchData botData, long userID, long botID, string logName)
        {
            BotType = BotTypeEnum.PancakePrediction;

            this.ID = botID;
            this.UserID = userID;
            this.logName = logName;
            this.data = botData;

            BotState = BotStateEnum.Not_Started;

            if (data.CONTRACT == null || data.CONTRACT.ToLower().StartsWith("0x") == false)
            {
                throw new Exception("Endereço de Contrato invalido");
            }

            if (data.AMOUNT < 0f)
            {
                buyMax = true;
            }
            else
            {
                AMOUNT_TO_BUY = Web3.Convert.ToWei(data.AMOUNT);
            }

            SALE_ID = data.SALE_ID;
            forceMinBuy = data.FORCE_BUY_MIN;
            waitSeconds = data.WAIT_SECONDS;
            loop_delay = TimeSpan.FromSeconds(data.LOOP_DELAY);
            launchDate = data.DATE;
            Log($"loopdelay - {loop_delay}");
            //verificar a data antes de vir pro bot

            account = new Account(privateData.PRIVATE_KEY);
            MakeWeb3Connection();

            this.gasPrice = new HexBigInteger(Web3.Convert.ToWei(data.GAS_PRICE, UnitConversion.EthUnit.Gwei));
            this.gasLimit = new HexBigInteger(data.GAS_LIMIT);

            Log(ExtractBotData());
        }

        private void MakeWeb3Connection()
        {
            Log("Making Web3 new Connection");
            web3_http = new Web3(account, OrdersManager.configData.RPC_HTTP);
            web3_http.TransactionManager.UseLegacyAsDefault = true;

            launchContract = web3_http.Eth.GetContract(ABI.SUPERLAUNCH_ABI, data.CONTRACT);

            if (data.NFT)
            {
                func_Purchase = launchContract.GetFunction("purchase");
                // func_getPurchaseLimit = launchContract.GetFunction("getPurchaseLimit");
                func_getPurchaseLimit = launchContract.GetFunction("getPurchaseLimit");
            }
            else
            {
                func_Contribute = launchContract.GetFunction("contribute");
                func_GetMaxContribution = launchContract.GetFunction("getMaxContribution");
                func_GetTotalRaised = launchContract.GetFunction("totalRaised");
                func_StartAt = launchContract.GetFunction("startedAt");
            }

        }

        private async Task<DateTimeOffset> GetStartTime()
        {
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds((long)await func_StartAt.CallAsync<BigInteger>());
            }
            catch (Exception e)
            {
                Log("Error Getting Started Time");
                Log(e.Message);
                Log(e.StackTrace);
                MakeWeb3Connection();
                return await GetStartTime();
            }
        }

        private async Task<BigInteger> GetMinContribution()
        {
            try
            {
                var call = new Nethereum.RPC.Eth.DTOs.CallInput(data: "0xa2076b6f", addressTo: launchContract.Address);
                return HexBigIntegerConvertorExtensions.HexToBigInteger(await web3_http.Eth.Transactions.Call.SendRequestAsync(call), false);
            }
            catch (Exception e)
            {
                Log("Error Getting Min Contribution");
                Log(e.Message);
                Log(e.StackTrace);
                MakeWeb3Connection();
                return await GetMinContribution();
            }
        }

        private async Task<BigInteger> GetMaxContribution()
        {
            try
            {
                return await func_GetMaxContribution.CallAsync<BigInteger>(new object[] { account.Address });
            }
            catch (Exception e)
            {
                Log("Error Getting Max Contribution");
                Log(e.Message);
                Log(e.StackTrace);
                MakeWeb3Connection();
                return await GetMaxContribution();
            }
        }

        private async Task<BigInteger> GetPurchaseLimit(BigInteger saleId)
        {
            try
            {
                // Log(account.Address);
                // Log(await func_getPurchaseLimit.CallAsync<BigInteger>(new object[] { saleId, account.Address }));

                return await func_getPurchaseLimit.CallAsync<BigInteger>(new object[] { saleId, account.Address });
            }
            catch (Exception e)
            {
                Log("Error Geting Purchase Limit");
                Log(e.Message);
                Log(e.StackTrace);
                MakeWeb3Connection();
                return await GetPurchaseLimit(saleId);
            }
        }

        private async Task<BigInteger> GetTotalRaised()
        {
            try
            {
                return await func_GetTotalRaised.CallAsync<BigInteger>(new object[] { });
            }
            catch (Exception e)
            {
                Log("Error Getting Total Raised");
                Log(e.Message);
                Log(e.StackTrace);
                MakeWeb3Connection();
                return await GetMaxContribution();
            }
        }

        private async Task<BigInteger> GetMaxRaiseLimit()
        {
            try
            {
                var call = new Nethereum.RPC.Eth.DTOs.CallInput(data: "0xd837157a", addressTo: launchContract.Address);
                return HexBigIntegerConvertorExtensions.HexToBigInteger(await web3_http.Eth.Transactions.Call.SendRequestAsync(call), false);
            }
            catch (Exception e)
            {
                Log("Error Getting Max Raise Limit");
                Log(e.Message);
                Log(e.StackTrace);
                MakeWeb3Connection();
                return await GetMinContribution();
            }
        }

        public async Task<bool> StartBot()
        {
            if (Running == false)
            {
                Running = true;
                token_source_loop = new CancellationTokenSource();
                token_source_wait = new CancellationTokenSource();
                ct_wait = token_source_wait.Token;
                ct_loop = token_source_loop.Token;

                await ApproveBUSD(new HexBigInteger(Web3.Convert.ToWei(5, UnitConversion.EthUnit.Gwei)));

                Task.Run(MainLoop, ct_loop);

                return true;
            }
            else
            {
                Log("Bot já foi iniciado");
            }

            return false;
        }

        private async Task MainLoop()
        {
            try
            {
                BotState = BotStateEnum.Running;
                InvokeChangeEvent();
                Log("Bot running\n");

                while (Running)
                {
                    ThrowIfStopped();

                    var remaningTime = launchDate - DateTimeOffset.UtcNow;

                    // Checar se falta 30 segundos pro launch time
                    if (remaningTime <= TimeSpan.FromSeconds(45))
                    {
                        if (data.NFT)
                        {
                            if (data.SPAM_MODE)
                            {
                                if (remaningTime <= TimeSpan.FromSeconds(waitSeconds))
                                {
                                    ThrowIfStopped();

                                    string hash = await Purchase(SALE_ID, 1);
                                    Log($"Purchased $ {SALE_ID} NFT\nHash: https://bscscan.com/tx/{hash}", true);

                                    spamCount += 1;
                                    gasPrice.Value += 1;

                                    if (spamCount >= (int)waitSeconds)
                                    {
                                        Running = false;
                                        BotState = BotStateEnum.Completed;

                                        Log($"COMPLETED");

                                        if (BotState == BotStateEnum.Completed)
                                        {
                                            this.ForceStop();
                                        }

                                        return;
                                    }
                                    else
                                    {
                                        await Task.Delay(loop_delay);
                                    }

                                }

                            }
                            else
                            {
                                if ((await GetPurchaseLimit(SALE_ID)) > 0)
                                {
                                    ThrowIfStopped();

                                    string hash = await Purchase(SALE_ID, 1);
                                    Log($"Purchased $ {SALE_ID} NFT\nHash: https://bscscan.com/tx/{hash}", true);

                                    Running = false;
                                    BotState = BotStateEnum.Completed;

                                    Log($"COMPLETED");

                                    if (BotState == BotStateEnum.Completed)
                                    {
                                        this.ForceStop();
                                    }

                                    return;
                                }
                                await Task.Delay((int)data.LOOP_DELAY);

                            }
                            continue;
                        }

                        BigInteger maxValueContribute = await GetMaxContribution();

                        while (maxValueContribute <= 0)
                        {
                            try
                            {
                                await Task.Delay(loop_delay, ct_wait);
                                ThrowIfStopped();
                                maxValueContribute = await GetMaxContribution();
                            }
                            catch (System.Exception e)
                            {
                                Log("Error");
                                Log(e);
                                Log($"{await WebUtils.GetBlockNumberAsync(web3_http)} - delay");

                                ThrowIfStopped();
                            }
                        }

                        // var startAtTime = await GetStartTime();
                        BigInteger minValueContribute = await GetMinContribution();
                        Log($"Max Contri - {maxValueContribute}");
                        Log($"Min Contri - {minValueContribute}");

                        // // Ficar verificando se o evento já começou
                        // if (startAtTime.Year != 1 && startAtTime.Year != 1970)
                        // {
                        //     Log($"Started time - {startAtTime}");
                        await ApproveBUSD();

                        if (buyMax)
                        {
                            if (maxValueContribute > 0)
                            {
                                AMOUNT_TO_BUY = maxValueContribute - Web3.Convert.ToWei(0.1f);
                            }
                            else
                            {
                                AMOUNT_TO_BUY = minValueContribute;
                            }
                        }

                        if (AMOUNT_TO_BUY < minValueContribute)
                        {
                            if (forceMinBuy)
                            {
                                AMOUNT_TO_BUY = minValueContribute;
                            }
                            else
                            {
                                Log("Valor de compra abaixo do minimo exigido", true);
                                throw new Exception("Valor de compra abaixo do minimo exigido");
                            }
                        }

                        if (AMOUNT_TO_BUY > maxValueContribute && maxValueContribute > 0)
                        {
                            AMOUNT_TO_BUY = maxValueContribute - Web3.Convert.ToWei(0.1f);
                        }

                        ThrowIfStopped();

                        string contribute_hash = await Contribute(AMOUNT_TO_BUY);
                        Log($"Contributed $ {Web3.Convert.FromWei(AMOUNT_TO_BUY)} BUSD\nHash: https://bscscan.com/tx/{contribute_hash}", true);

                        Running = false;
                        BotState = BotStateEnum.Completed;

                        Log($"COMPLETED");

                        if (BotState == BotStateEnum.Completed)
                        {
                            this.ForceStop();
                        }

                        return;
                        // }
                        // else
                        // {
                        //     await Task.Delay(loop_delay, ct_wait);
                        //     delayCount += 1;
                        //     if (delayCount % 5 == 0)
                        //     {
                        //         Log($"{await WebUtils.GetBlockNumberAsync(web3_http)} - delay");
                        //     }
                        // }

                    }
                    else
                    {
                        var waitingTime = remaningTime - TimeSpan.FromSeconds(29);
                        if (waitingTime < TimeSpan.Zero) waitingTime = TimeSpan.Zero;
                        Log($"Waiting {waitingTime}");
                        await Task.Delay(waitingTime, ct_wait);
                    }
                }
            }
            catch (OperationCanceledException e)
            {
                Log("Token Stopped");
                Log($"{nameof(OperationCanceledException)} thrown with message: {e.Message}");
            }
            catch (Exception e)
            {
                Log("Error Launcher loop");
                Log(e.Message);
                Log(e.StackTrace);
            }
            finally
            {
                ForceStop();
            }

            void ThrowIfStopped()
            {
                if (Running == false)
                {
                    ForceStop();
                    throw new Exception("Bot foi parado");
                }
                ct_loop.ThrowIfCancellationRequested();
                ct_wait.ThrowIfCancellationRequested();
            }
        }

        private async Task<string> Contribute(BigInteger amountToBuy)
        {
            Log($"Contribuing... {Web3.Convert.FromWei(amountToBuy)}");
            if (data.DEBUG)
            {
                return "hash";
            }

            object[] paramters = new object[] { amountToBuy };

            return await func_Contribute.SendTransactionAsync(from: account.Address, gas: gasLimit, gasPrice: gasPrice, value: new HexBigInteger(0), functionInput: paramters);
        }

        private async Task<string> Purchase(BigInteger saleId, BigInteger numItems)
        {
            Log($"Purchasing... {saleId}");
            if (data.DEBUG)
            {
                return "hash";
            }
            object[] paramters = new object[] { saleId, numItems };

            return await func_Purchase.SendTransactionAsync(from: account.Address, gas: gasLimit, gasPrice: gasPrice, value: new HexBigInteger(0), functionInput: paramters);
        }

        private async Task ApproveBUSD(HexBigInteger sub_gasPrice = null)
        {
            Contract busdContract = web3_http.Eth.GetContract(ABI.ERC20_ABI, Tokens.BUSD);
            if (await WebUtils.CheckAllowance(web3_http, busdContract, launchContract.Address, account.Address) == false)
            {
                if (data.DEBUG)
                {
                    Log("Need Approve");
                    return;
                }
                string approve_hash = await WebUtils.Approve(busdContract, launchContract.Address, account.Address, sub_gasPrice != null ? sub_gasPrice : gasPrice);
                Log($"Approved Launchpad\nHash: https://bscscan.com/tx/{approve_hash}", true);
            }
            else
            {
                Log("Already Approved");
            }
        }

        private void Log(object obj)
        {
            Log(obj.ToString());
        }

        private void Log(string message, bool sendToTelegram = false)
        {
            Logger.WriteLog(message, logName);
            if (sendToTelegram)
            {
                message = $"BOT({ID})\n{message}";
                ComunicationManager.SendMessage(message, UserID);
            }
        }

        public override string ExtractBotData()
        {
            return JsonConvert.SerializeObject(data);
        }

        public override string BotDetail()
        {
            return "" +
            $"State: {BotState}\n" +
            $"Is Private Deal: {data.PRIVATE_DEAL}\n" +
            $"Launch Date: {launchDate.ToString(DateJsonConverter.fmt)}\n" +
            $"Contract: {launchContract.Address}\n" +
            $"Amount to buy: {(buyMax ? "MAX" : data.AMOUNT.ToString())}\n" +
            $"Force Buy Min: {forceMinBuy}\n" +
            $"Waiting seconds to buy: {waitSeconds} sec\n" +
            $"Gas Price: {data.GAS_PRICE}\n" +
            $"Gas Limit: {data.GAS_LIMIT}\n" +
            "";
        }

        public override void ForceStop(bool fromTelegram = false)
        {
            BotState = BotStateEnum.Stopped;

            try
            {
                if (this.forceStop)
                {
                    Log("ForceStop já foi dado");
                    return;
                }
                this.forceStop = true;
                this.Running = false;

                if (ct_loop.CanBeCanceled)
                {
                    try
                    {
                        token_source_loop?.Cancel();
                        token_source_loop?.Dispose();
                        token_source_loop = null;
                    }
                    catch (System.Exception e)
                    {
                        Log(e);
                    }
                }

                if (ct_wait.CanBeCanceled)
                {
                    token_source_wait?.Cancel();
                    token_source_wait?.Dispose();
                    token_source_wait = null;
                }

                Log($"Safe Launch Bot Stop Working", true);

                if (BotState != BotStateEnum.Completed)
                {
                    BotState = BotStateEnum.Stopped;
                }

                InvokeChangeEvent();
                changedUpdateEvent = null;
                Log("Forcing Stop");

                if (fromTelegram == false)
                {
                    Task.Run(() => OrdersManager.Instance.RemoveBot(UserID, (int)ID, true));
                }

            }
            catch (System.Exception e)
            {
                Log($"Error - Force Stop - {e}");
            }
        }

        protected override void InvokeChangeEvent()
        {
            try
            {
                changedUpdateEvent?.Invoke(this);
                Log($"Invoke {BotState}");
            }
            catch (System.Exception e)
            {
                Log(e);
            }
        }
    }
}