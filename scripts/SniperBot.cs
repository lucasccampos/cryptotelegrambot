using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.Web3;
using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.Contracts;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using System.Numerics;
using System.IO;
using Nethereum.Hex.HexTypes;
using System.Collections.Generic;
using TradingBot.Events;
using TradingBot.Data;
using TradingBot.Utils;
using Nethereum.JsonRpc.Client;

using Newtonsoft.Json;
using Nethereum.JsonRpc.WebSocketClient;
using TradingBot.Functions;

namespace TradingBot
{
    public class SniperBot : Bot
    {
        public const string EMPTY_ADDRESS = "0x0000000000000000000000000000000000000000";

        public bool DEBUG = false;

        public Web3 web3_http;
        public StreamingWebSocketClient webSocketClient;

        string TOKEN_SWAP { get; set; }
        string TOKEN_PAIR { get; set; }
        string TOKEN_MAINNET { get; set; }
        string BUY_USING_TOKEN { get; set; }

        string ROUTER { get; set; }

        private bool BUY = true;

        int SPLIT { get; set; }
        float TIME_BTW_SPLIT { get; set; }

        private float buyPrice;

        public ConfigData configData { get; private set; }

        private WebSocketClient wsClient;

        public PrivateData privateData { get; private set; }
        public SniperData botData { get; private set; }

        private Account account;
        private Contract tokenContract;
        private Contract dexContract;

        private Dex dex;

        private string[] pathToBuy;

        private HexBigInteger gasPrice;
        private HexBigInteger gasLimit;
        private BigInteger AMOUNT_TO_BUY;
        private BigInteger AMOUNT_TO_RECEIVE;

        bool ignoreLiquidity = true;
        bool ignorePrice = true;

        private Function funcAddLiquidityETH;
        private Function funcAddLiquidity;
        private EthNewPendingTransactionObservableSubscription Subscription;

        public bool loopRunning { get; private set; }
        public bool forceStop { get; private set; }

        private string logName { get; set; }

        private void Log(string message)
        {
            Logger.WriteLog(message, this.logName);
        }

        public SniperBot(ConfigData configData, PrivateData privateData, SniperData botData, long userID, long botID, string logName)
        {
            this.BotType = BotTypeEnum.Sniper;

            this.ID = botID;
            this.UserID = userID;
            this.logName = logName;

            BotState = BotStateEnum.Not_Started;

            this.LoadNewPrivateData(privateData);
            this.LoadNewConfigData(configData);
            this.LoadNewBotData(botData);

            this.dex = new Dex(this.dexContract);

            funcAddLiquidityETH = this.dexContract.GetFunction("addLiquidityETH");
            funcAddLiquidity = this.dexContract.GetFunction("addLiquidity");

            Log("Created");
        }

        #region LoadData

        private void LoadNewPrivateData(PrivateData privateData)
        {
            this.privateData = privateData;

            this.account = new Account(this.privateData.PRIVATE_KEY, 56);

        }

        private void LoadNewConfigData(ConfigData configData)
        {
            this.configData = configData;

            // this.web3_http = new Web3(account, this.configData.RPC_HTTP);
            wsClient = new WebSocketClient(this.configData.RPC_WS);
            this.web3_http = new Web3(account, wsClient);
            web3_http.TransactionManager.UseLegacyAsDefault = true;

            this.webSocketClient = new StreamingWebSocketClient(this.configData.RPC_WS);
        }

        private void LoadNewBotData(SniperData botData)
        {
            this.botData = botData;

            this.TOKEN_SWAP = this.botData.TOKEN;
            this.TOKEN_PAIR = this.botData.LIQUIDITY_TOKEN;
            this.TOKEN_MAINNET = this.botData.MAIN_TOKEN_RPC;
            this.BUY_USING_TOKEN = this.botData.BUY_USING_TOKEN;

            if (this.BUY_USING_TOKEN == null) this.BUY_USING_TOKEN = this.TOKEN_PAIR;

            this.SPLIT = this.botData.SPLIT;
            this.TIME_BTW_SPLIT = this.botData.TIME_BTW_SPLIT;

            if (SPLIT <= 0) SPLIT = 1;

            this.buyPrice = this.botData.MAX_PRICE_BUY;


            if (this.buyPrice <= 0f) ignorePrice = true;

            if (this.botData.MIN_LIQUIDY <= 0f)
            {
                ignoreLiquidity = true;
            }

            this.ROUTER = this.botData.dexData.ROUTER;

            this.dexContract = web3_http.Eth.GetContract(ABI.DEX_ABI, this.ROUTER);
            this.tokenContract = this.web3_http.Eth.GetContract(ABI.ERC20_ABI, this.TOKEN_SWAP);

            List<string> path = new List<string>() { this.TOKEN_PAIR, this.TOKEN_SWAP };

            if (this.TOKEN_PAIR != this.BUY_USING_TOKEN)
            {
                path.Insert(0, this.BUY_USING_TOKEN);
            }

            this.pathToBuy = path.ToArray();

            this.gasPrice = new HexBigInteger(Web3.Convert.ToWei(this.botData.GAS_PRICE, UnitConversion.EthUnit.Gwei));
            this.gasLimit = new HexBigInteger(this.botData.GAS_LIMIT);

            //TODO : check if amount to buy is igual 0 and throw error
            this.AMOUNT_TO_BUY = Web3.Convert.ToWei(this.botData.AMOUNT_TO_BUY);
            this.AMOUNT_TO_RECEIVE = Web3.Convert.ToWei(this.botData.MIN_AMOUNT_TO_RECEIVE);

            if (SPLIT > 1)
            {
                this.AMOUNT_TO_BUY = this.AMOUNT_TO_BUY / this.SPLIT;
                this.AMOUNT_TO_RECEIVE = this.AMOUNT_TO_RECEIVE / this.SPLIT;
            }
        }

        #endregion

        public async void main()
        {
            BotState = BotStateEnum.Running;
            Task.Run(InvokeChangeEvent);
            this.loopRunning = true;
            try
            {

                await NewPendingTransactions();

            }
            catch (Exception e)
            {
                Log("Error main loop");
                Log(e.Message);
                Log(e.StackTrace);
            }
            // InvokeChangeEvent();
            // Log("Main loop finalizado");
            // ForceStop();
        }

        public async Task NewPendingTransactions()
        {
            try
            {
                if (Subscription != null)
                {
                    Log("Subscription já existia !!!");
                    Console.WriteLine("Subscription já existia !!!");
                    return;
                }

                Subscription = new EthNewPendingTransactionObservableSubscription(webSocketClient);

                Subscription.GetSubscribeResponseAsObservable().Subscribe(subscriptionId =>
                {
                    Console.WriteLine("Pending transactions subscription Id: " + subscriptionId);
                    Log("Pending transactions subscription Id: " + subscriptionId);
                });

                Subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(PedingTransactionHandler);


                Subscription.GetUnsubscribeResponseAsObservable().Subscribe(response =>
                {
                    Console.WriteLine("Pending transactions unsubscribe result: " + response);
                    Log("Pending transactions unsubscribe result: " + response);
                });

                await webSocketClient.StartAsync();

                await Subscription.SubscribeAsync();

                // wait for unsubscribe 
                while ((int)Subscription?.SubscriptionState < 3)
                {
                    if (this.forceStop)
                    {
                        ForceStop();
                        break;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1));
                }

                // unsubscribe
                await Subscription?.UnsubscribeAsync();

                ForceStop();
            }
            catch (Exception e)
            {
                Log("Error Pending Loop");
                Log(e.Message);
                Log(e.StackTrace);
            }
        }

        private async void PedingTransactionHandler(string txn_hash)
        {
            try
            {
                if (this.loopRunning == false || this.forceStop == true) return;

                if (this.BUY == false) return;

                var transaction = await web3_http.Eth.Transactions.GetTransactionByHash.SendRequestAsync(txn_hash);
                if (transaction == null) return;
                if (transaction.To == this.ROUTER)
                {
                    if (botData.LIQUIDITY_TOKEN != TOKEN_MAINNET)
                    {
                        if (transaction.Input.StartsWith("0xe8e33700"))
                        {
                            var result = new AddLiquidityFunction(funcAddLiquidity.DecodeInput(transaction.Input));
                            if (result.tokenA == TOKEN_SWAP && result.tokenB == botData.LIQUIDITY_TOKEN)
                            {
                                Console.WriteLine(txn_hash);
                                if (ignoreLiquidity == false)
                                {
                                    // Check Liquidity
                                    if ((float)result.amountBMin < botData.MIN_LIQUIDY) return;
                                }

                                if (ignorePrice == false)
                                {
                                    decimal priceToken;
                                    try
                                    {
                                        priceToken = Web3.Convert.FromWei(result.amountBMin) / Web3.Convert.FromWei(result.amountAMin);
                                    }
                                    catch (System.Exception e)
                                    {
                                        priceToken = 0;
                                        Log($"Error {e}");
                                        Log($"{e.StackTrace}");
                                    }
                                    // Check Token Price 
                                    if ((float)priceToken > buyPrice) return;
                                }

                                BuyMethod().Wait();
                            }
                        }

                    }
                    if (transaction.Input.StartsWith("0xf305d719"))
                    {
                        var result = new AddLiquidityETHFunction(funcAddLiquidityETH.DecodeInput(transaction.Input));
                        if (result.token == TOKEN_SWAP)
                        {
                            Console.WriteLine(txn_hash);
                            if (ignoreLiquidity == false)
                            {
                                // Check Liquidity BNB 
                                if ((float)result.amountETHMin < botData.MIN_LIQUIDY) return;
                            }

                            if (ignorePrice == false)
                            {
                                decimal priceToken;
                                try
                                {
                                    priceToken = Web3.Convert.FromWei(result.amountETHMin) / Web3.Convert.FromWei(result.amountTokenMin);
                                }
                                catch (System.Exception e)
                                {
                                    priceToken = 0;
                                    Log($"Error {e}");
                                    Log($"{e.StackTrace}");
                                }
                                // Check Token Price 
                                if ((float)priceToken > buyPrice) return;
                            }

                            BuyMethod().Wait();
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                ForceStop();
                Console.WriteLine(e);
                Log("Error Txn_Handler");
                Log(e.Message);
                Log(e.StackTrace);
            }
        }

        private async Task BuyMethod()
        {
            try
            {
                if (BUY == false) return;

                this.BUY = false;

                if (this.botData.WAIT_SECONDS > 0f)
                {
                    // Log($"Waiting to buy...");
                    await Task.Delay((int)(this.botData.WAIT_SECONDS * 1000));
                }


                for (int i = 0; i < this.SPLIT; i++)
                {
                    if (this.forceStop)
                    {
                        return;
                    }

                    string hash;
                    if (this.BUY_USING_TOKEN != this.TOKEN_MAINNET)
                    {
                        // HANDLE ERROR NO SUFICIENT BALANCE
                        hash = await dex.swapExactTokensForTokensSupportingFeeOnTransferTokens(this.AMOUNT_TO_BUY, this.AMOUNT_TO_RECEIVE, this.pathToBuy, this.account.Address, this.botData.TXN_MAX_DURATION_MINUTES, this.gasLimit, this.gasPrice);
                    }
                    else
                    {
                        hash = await dex.swapExactETHForTokensSupportingFeeOnTransferTokens(this.AMOUNT_TO_BUY, this.AMOUNT_TO_RECEIVE, this.pathToBuy, this.account.Address, this.botData.TXN_MAX_DURATION_MINUTES, this.gasLimit, this.gasPrice);
                    }

                    Log($"Hash >: {hash}");
                    ComunicationManager.SendMessage($"BOT ({ID}) BOUGHT - https://bscscan.com/tx/{hash}", UserID);

                    if (this.TIME_BTW_SPLIT > 0f)
                    {
                        await Task.Delay((int)(this.TIME_BTW_SPLIT * 1000));

                    }
                }
                BotState = BotStateEnum.Completed;
                Log($"COMPLETED");

                if (BotState == BotStateEnum.Completed)
                {
                    this.ForceStop();
                }
            }
            catch (System.Exception e)
            {
                Log(e.ToString());
                ForceStop();
            }
            // Log($"Price: {tokenUsdPrice.ToString("0.##################")} - {this.tokenTrackerSwap.CurrentPrice} - {this.tokenTrackerUSD.CurrentPrice}");
        }

        public override async void ForceStop(bool fromTelegram = false)
        {
            try
            {
                if (this.forceStop)
                {
                    Log("ForceStop já foi dado");
                    return;
                }
                this.forceStop = true;
                this.loopRunning = false;

                wsClient?.Dispose();

                // unsubscribe
                await Subscription?.UnsubscribeAsync();
                Subscription = null;

                await webSocketClient?.StopAsync();
                webSocketClient = null;

                ComunicationManager.SendMessage($"{ID} Bot Stop Working", UserID);

                if (BotState != BotStateEnum.Completed)
                {
                    BotState = BotStateEnum.Stopped;
                }

                InvokeChangeEvent();
                changedUpdateEvent = null;
                Log("Forcing Stop");
                // Task.Run(() => BotManager.AutoDestroyBot(UserID, ID));

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
            changedUpdateEvent?.Invoke(this);
            Log($"Invoke {BotState}");
        }

        public override string BotDetail()
        {
            return "" +
            $"State: {BotState}\n" +
            $"Token: {TOKEN_SWAP}\n" +
            $"Buying at: {buyPrice}\n" +
            $"Buying with: {BUY_USING_TOKEN}\n" +
            $"Paying: {Web3.Convert.FromWei(AMOUNT_TO_BUY)}\n" +
            $"Liquidity Token: {TOKEN_PAIR}" +
            "";
        }

        public override string ExtractBotData()
        {
            return JsonConvert.SerializeObject(this.botData);
        }

    }
}
