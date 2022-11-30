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

namespace TradingBot
{
    public class TraderBot : Bot
    {
        public const string EMPTY_ADDRESS = "0x0000000000000000000000000000000000000000";

        public Web3 web3_http;
        public StreamingWebSocketClient webSocketClient;

        string TOKEN_SWAP { get; set; }
        string TOKEN_PAIR { get; set; }
        string TOKEN_MAINNET { get; set; }
        string BUY_USING_TOKEN { get; set; }
        string RECEIVE_IN_TOKEN { get; set; }

        string ROUTER { get; set; }
        string FACTORY { get; set; }

        private bool BUY;
        private bool SELL;

        int SPLIT { get; set; }
        float TIME_BTW_SPLIT { get; set; }

        private float buyPrice;
        private float sellPrice;

        public ConfigData configData { get; private set; }
        public PrivateData privateData { get; private set; }
        public TradeData botData { get; private set; }

        private Account account;
        private Contract tokenContract;
        private Contract dexContract;
        private Contract factoryContract;

        private Dex dex;

        private string[] pathToBuy;
        private string[] pathToSell;
        public ITokenTracker tokenTrackerSwap { get; private set; }
        public ITokenTracker tokenTrackerUSD { get; private set; }
        private HexBigInteger gasPrice;
        private HexBigInteger gasLimit;
        private BigInteger AMOUNT_TO_BUY;
        private BigInteger AMOUNT_TO_SELL;
        private BigInteger AMOUNT_TO_RECEIVE;

        public bool loopRunning { get; private set; }
        public bool forceStop { get; private set; }

        private string logName { get; set; }

        private void Log(string message)
        {
            Logger.WriteLog(message, this.logName);
        }

        public TraderBot(ConfigData configData, PrivateData privateData, TradeData botData, long userID, long botID, string logName)
        {
            this.BotType = BotTypeEnum.Trade;

            this.ID = botID;
            this.UserID = userID;
            this.logName = logName;

            BotState = BotStateEnum.Not_Started;

            this.LoadNewPrivateData(privateData);
            this.LoadNewConfigData(configData);
            this.LoadNewBotData(botData);

            if(this.SELL && this.BUY){
                throw new Exception("SELL AND BUY ENABLED");
            }

            this.dex = new Dex(this.dexContract);

            this.tokenTrackerSwap = TokenTrackerManager.GetToken(TOKEN_SWAP, TOKEN_PAIR, ROUTER);
            if (TOKEN_PAIR == Tokens.BUSD || TOKEN_PAIR == Tokens.USDT || TOKEN_PAIR == Tokens.USDC)
            {
                this.tokenTrackerUSD = TokenTrackerManager.GetToken(TOKEN_PAIR, Tokens.WBNB, DexBSCJsonConverter.PANCAKESWAP.ROUTER);
            }
            else
            {
                this.tokenTrackerUSD = TokenTrackerManager.GetToken(Tokens.WBNB, Tokens.USDT, DexBSCJsonConverter.PANCAKESWAP.ROUTER);
            }

            if (this.SELL)
            {
                //CHECK ALLOWANCE
                if (WebUtils.CheckAllowance(this.web3_http, this.tokenContract, this.ROUTER, this.account.Address).Result == false) // .Result
                {
                    Log($"Approving token...");

                    var approve_hash = WebUtils.Approve(this.tokenContract, this.ROUTER, this.account.Address, this.gasPrice).Result; // .Result
                    Log($"Approve Txn : {approve_hash}");
                    ComunicationManager.SendMessage($"BOT ({ID}) TOKEN APPROVED - https://bscscan.com/tx/{approve_hash}", UserID);
                }
            }

            else if (this.BUY_USING_TOKEN != this.TOKEN_MAINNET)
            {
                var contract = this.web3_http.Eth.GetContract(ABI.ERC20_ABI, this.BUY_USING_TOKEN);
                if (WebUtils.CheckAllowance(this.web3_http, contract, this.ROUTER, this.account.Address).Result == false) // .Result
                {
                    Log($"Approving token...");

                    var approve_hash = WebUtils.Approve(contract, this.ROUTER, this.account.Address, this.gasPrice).Result; // .Result
                    Log($"Approve Txn : {approve_hash}");
                    ComunicationManager.SendMessage($"BOT ({ID}) TOKEN APPROVED - https://bscscan.com/tx/{approve_hash}", UserID);
                }

            }

            Log("Created");
        }

        double currentPrice;
        public void Loop()
        {
            currentPrice = tokenTrackerSwap.CurrentPrice * tokenTrackerUSD.CurrentPrice;

            if (currentPrice == 0 || this.forceStop) return;

            if (this.BUY)
            {
                if (currentPrice <= this.buyPrice)
                {
                    Task.Run(BuyMethod);
                }
            }
            else if (this.SELL)
            {
                if (currentPrice >= this.sellPrice)
                {
                    Task.Run(SellMethod);
                }
            }
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

            this.web3_http = new Web3(account, this.configData.RPC_HTTP);
            web3_http.TransactionManager.UseLegacyAsDefault = true;

            this.webSocketClient = new StreamingWebSocketClient(this.configData.RPC_WS);
        }

        private void LoadNewBotData(TradeData botData)
        {
            this.botData = botData;

            this.TOKEN_SWAP = this.botData.TOKEN;
            this.TOKEN_PAIR = this.botData.LIQUIDITY_TOKEN;
            this.TOKEN_MAINNET = this.botData.MAIN_TOKEN_RPC;
            this.BUY_USING_TOKEN = this.botData.BUY_USING_TOKEN;
            this.RECEIVE_IN_TOKEN = this.botData.RECEIVE_IN_TOKEN;

            if (this.BUY_USING_TOKEN == null) this.BUY_USING_TOKEN = this.TOKEN_PAIR;
            if (this.RECEIVE_IN_TOKEN == null) this.RECEIVE_IN_TOKEN = this.TOKEN_PAIR;

            this.BUY = this.botData.BUY;
            this.SELL = this.botData.SELL;

            this.SPLIT = this.botData.SPLIT;
            this.TIME_BTW_SPLIT = this.botData.TIME_BTW_SPLIT;

            if (SPLIT <= 0) SPLIT = 1;

            this.buyPrice = this.botData.MAX_PRICE_BUY;
            this.sellPrice = this.botData.MIN_PRICE_SELL;

            this.ROUTER = this.botData.dexData.ROUTER;
            this.FACTORY = this.botData.dexData.FACTORY;

            this.dexContract = web3_http.Eth.GetContract(ABI.DEX_ABI, this.ROUTER);
            this.factoryContract = web3_http.Eth.GetContract(ABI.FACTORY_ABI, this.FACTORY);
            this.tokenContract = this.web3_http.Eth.GetContract(ABI.ERC20_ABI, this.TOKEN_SWAP);

            List<string> path = new List<string>() { this.TOKEN_PAIR, this.TOKEN_SWAP };

            if (this.TOKEN_PAIR != this.BUY_USING_TOKEN)
            {
                path.Insert(0, this.BUY_USING_TOKEN);
            }

            this.pathToBuy = path.ToArray();

            path = new List<string>() { this.TOKEN_SWAP, this.TOKEN_PAIR };

            if (this.RECEIVE_IN_TOKEN != this.TOKEN_PAIR)
            {
                path.Add(this.RECEIVE_IN_TOKEN);
            }

            this.pathToSell = path.ToArray();

            this.gasPrice = new HexBigInteger(Web3.Convert.ToWei(this.botData.GAS_PRICE, UnitConversion.EthUnit.Gwei));
            this.gasLimit = new HexBigInteger(this.botData.GAS_LIMIT);

            this.AMOUNT_TO_BUY = Web3.Convert.ToWei(this.botData.AMOUNT_TO_BUY);
            this.AMOUNT_TO_SELL = Web3.Convert.ToWei(this.botData.AMOUNT_TO_SELL);
            this.AMOUNT_TO_RECEIVE = Web3.Convert.ToWei(this.botData.MIN_AMOUNT_TO_RECEIVE);

            if (SPLIT > 1)
            {
                this.AMOUNT_TO_BUY = this.AMOUNT_TO_BUY / this.SPLIT;
                this.AMOUNT_TO_SELL = this.AMOUNT_TO_SELL / this.SPLIT;
                this.AMOUNT_TO_RECEIVE = this.AMOUNT_TO_RECEIVE / this.SPLIT;
            }
        }

        #endregion

        public async void main()
        {
            return;
            BotState = BotStateEnum.Running;
            Task.Run(InvokeChangeEvent);

            try
            {
                this.loopRunning = true;

                // Contract pairContractToken = await FindPairContract(factory_contract, token_wbnb, token_swap, web3_http, webSocketClient);
                this.tokenTrackerSwap.TrackPrice(dexContract, Web3.Convert.ToWei(0.0001), this.configData.TRACK_TOKEN_PRICE_MS);
                this.tokenTrackerUSD.TrackPrice(dexContract, Web3.Convert.ToWei(0.0001), this.configData.TRACK_BNB_PRICE_MS);

                if (this.SELL)
                {
                    //CHECK ALLOWANCE
                    if (await WebUtils.CheckAllowance(this.web3_http, this.tokenContract, this.ROUTER, this.account.Address) == false)
                    {
                        //APPROVE TOKEN SELL
                        Log($"Approving token...");

                        var approve_hash = await WebUtils.Approve(this.tokenContract, this.ROUTER, this.account.Address, this.gasPrice);
                        Log($"Approve Txn : {approve_hash}");
                        ComunicationManager.SendMessage($"BOT ({ID}) TOKEN APPROVED - https://bscscan.com/tx/{approve_hash}", UserID);

                        //Wait transaction complete
                    }
                }

                double tokenUsdPrice = 0;
                while (this.loopRunning)
                {
                    tokenUsdPrice = this.tokenTrackerUSD.CurrentPrice * this.tokenTrackerSwap.CurrentPrice;

                    if (this.BUY)
                    {
                        if (tokenUsdPrice <= this.buyPrice && tokenUsdPrice != 0)
                        {
                            await BuyMethod();
                        }
                    }

                    else if (this.SELL)
                    {
                        if (tokenUsdPrice >= this.sellPrice && tokenUsdPrice != 0)
                        {
                            await SellMethod();
                        }
                    }
                    else if (BotState == BotStateEnum.Completed)
                    {
                        this.ForceStop();
                    }

                    if (this.forceStop)
                    {
                        this.ForceStop();
                        break;
                    }

                    await Task.Delay(this.configData.MAIN_LOOP_MS);
                }
            }
            catch (Exception e)
            {
                Log("Error main loop");
                Log(e.Message);
                Log(e.StackTrace);
            }

            InvokeChangeEvent();

            Log("Main loop finalizado");
            ForceStop();
        }

        private async Task SellMethod()
        {
            try
            {

                if (this.SELL == false) return;

                this.SELL = false;
                if (this.botData.WAIT_SECONDS > 0f)
                {
                    Log($"Waiting to sell...");
                    await Task.Delay((int)(this.botData.WAIT_SECONDS * 1000));
                }


                for (int i = 0; i < this.SPLIT; i++)
                {
                    if (this.forceStop)
                    {
                        break;
                    }

                    string hash = await dex.swapExactTokensForTokensSupportingFeeOnTransferTokens(this.AMOUNT_TO_SELL, this.AMOUNT_TO_RECEIVE, this.pathToSell, this.account.Address, this.botData.TXN_MAX_DURATION_MINUTES, this.gasLimit, this.gasPrice);
                    Log($"Hash >: {hash}");
                    ComunicationManager.SendMessage($"BOT ({ID}) SELLED - https://bscscan.com/tx/{hash}", UserID);

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

                // Log($"Price: {tokenUsdPrice.ToString("0.##################")} - {this.tokenTrackerSwap.CurrentPrice} - {this.tokenTrackerUSD.CurrentPrice}");
            }
            catch (System.Exception e)
            {
                Log(e.ToString());
                ForceStop();
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

        public override async void ForceStop(bool fromTelegram=false)
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

                if(fromTelegram == false){
                    Task.Run(() => OrdersManager.Instance.RemoveBot(UserID, (int)ID, true));
                }

            }
            catch (System.Exception e)
            {
                Log($"Error - Force Stop - {e}");
            }
        }

        public async Task<Contract> FindPairContract(Contract factoryContract, string token0, string token1, Web3 web3, StreamingWebSocketClient webSocketClient)
        {
            var func_getPair = factoryContract.GetFunction("getPair");
            object[] paramters = new object[2] { token0, token1 };
            string pairAddress = await func_getPair.CallAsync<string>(paramters);

            if (pairAddress == EMPTY_ADDRESS)
            {
                var eventSubscription = new EthLogsObservableSubscription(webSocketClient);
                var filterAuction = Event<PairCreatedEventDTO>.GetEventABI().CreateFilterInput(factoryContract.Address);

                EventLog<PairCreatedEventDTO> eventLog;
                bool founded = false;
                eventSubscription.GetSubscriptionDataResponsesAsObservable().Subscribe(log =>
                {
                    if (founded == false)
                    {
                        eventLog = log.DecodeEvent<PairCreatedEventDTO>();
                        if ((eventLog.Event.Token0 == token0 && eventLog.Event.Token1 == token1) || (eventLog.Event.Token0 == token1 && eventLog.Event.Token1 == token0))
                        {
                            founded = true;
                            pairAddress = eventLog.Event.Pair;
                            Log($"Founded - {pairAddress}");
                        }
                    }
                });

                eventSubscription.GetSubscribeResponseAsObservable().Subscribe(id => Log($"Subscribed with id: {id}"));

                eventSubscription.GetUnsubscribeResponseAsObservable().Subscribe(response =>
                {
                    Log("Pending transactions unsubscribe result: " + response);
                });

                await webSocketClient.StartAsync();

                await eventSubscription.SubscribeAsync(filterAuction);

                var delay = TimeSpan.FromMilliseconds(2);
                while (pairAddress == EMPTY_ADDRESS)
                {
                    await Task.Delay(delay);
                }

                await eventSubscription.UnsubscribeAsync();
                Log("Done");
            }

            return web3.Eth.GetContract(ABI.PAIR_ABI, pairAddress);
        }

        public async Task ChangeState(BotStateEnum newState, float delaySec = 0f)
        {
            if (newState != BotState)
            {
                if (delaySec > 0f)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySec));
                }
                BotState = newState;
                InvokeChangeEvent();
            }
        }

        protected override void InvokeChangeEvent()
        {
            changedUpdateEvent?.Invoke(this);
            Log($"Invoke {BotState}");
        }

        public override string BotDetail()
        {
            string detail = "" +
            $"State: {BotState}\n" +
            $"Token: $ {tokenTrackerSwap.Symbol} ( {TOKEN_SWAP} )\n" +
            $"Current Price: {currentPrice} | [Chart](https://poocoin.app/tokens/{TOKEN_SWAP})\n" +
            $"Liquidity Token: $ {Tokens.TryGetSymbol(TOKEN_PAIR).ToUpper()}\n" +
            "";

            if (BUY)
            {
                detail += $"Buying at: {buyPrice}\n" +
                $"Buying with: $ {Tokens.TryGetSymbol(BUY_USING_TOKEN).ToUpper()}\n" +
                $"Paying: {Web3.Convert.FromWei(AMOUNT_TO_BUY)}\n";
            }
            else
            {
                detail += $"Selling at: {sellPrice}\n" +
                $"Selling: {AMOUNT_TO_SELL}\n" +
                $"Receiving in: $ {Tokens.TryGetSymbol(RECEIVE_IN_TOKEN).ToUpper()}\n";
            }

            detail += $"Expecting receive: {Web3.Convert.FromWei(AMOUNT_TO_RECEIVE)}";

            return detail;
        }

        public override string ExtractBotData()
        {
            return JsonConvert.SerializeObject(this.botData);
        }

    }
}
