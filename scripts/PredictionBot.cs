using Nethereum.Web3;
using System;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Nethereum.Hex.HexTypes;
using System.Numerics;
using TradingBot.Data;

using Newtonsoft.Json;
using TradingBot.Functions;
using System.Threading;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;

public enum PredictionSide
{
    BULL,
    BEAR,
    NONE
}

namespace TradingBot
{
    public class PredictionBot : Bot
    {
        private const string pancakePredictionAddress = "0x18B2A687610328590Bc8F2e5fEdDe3b582A49cdA";
        private const string pancakeOracleAddress = "0xD276fCF34D54A926773c399eBAa772C12ec394aC";

        private Contract predictContract;

        private HttpClient httpClient;
        private Contract oracleContract;
        private string priceSource;

        private Account account;
        public Web3 web3_http;

        private Function func_GetCurrentEpoch;
        private Function func_GetRound;
        private Function func_BetBear;
        private Function func_BetBull;
        private Function func_paused;
        private Function func_oracleGetPrice;
        private HexBigInteger gasPrice;
        private HexBigInteger gasLimit;

        private DateTimeOffset lastWinDate;
        private DateTimeOffset currentDate;
        private float baseBet;
        private float currentBet;
        private int desiredSequencial;

        private IRound currentRound;

        private int currentSequencial = 0;

        private float waitSecondsToClose;
        private float secondsBeforeAttack;

        private bool Running = false;
        private string logName;

        private int currentWins;
        private int totalWins;
        private int totalLoss;
        private float totalProfit;

        private PredictionData data;
        private CancellationTokenSource token_source_loop;
        private CancellationToken ct_loop;
        private CancellationTokenSource token_source_wait;
        private CancellationToken ct_wait;
        private bool isPancake;

        private bool debug;
        private bool forceStop=false;

        public PredictionBot(ConfigData configData, PrivateData privateData, PredictionData botData, long userID, long botID, string logName)
        {
            BotType = BotTypeEnum.PancakePrediction;

            this.ID = botID;
            this.UserID = userID;
            this.logName = logName;
            this.data = botData;
            isPancake = botData.PREDICTION_ADDRESS == pancakePredictionAddress;
            BotState = BotStateEnum.Not_Started;
            debug = botData.DEBUG;

            baseBet = data.INICIAL_BET;
            currentBet = data.INICIAL_BET;

            desiredSequencial = data.MAX_SEQUENCIAL;
            waitSecondsToClose = data.SECONDS_AFTER_ROUND_CLOSE;
            secondsBeforeAttack = data.SECONDS_BEFORE_BET;

            account = new Account(privateData.PRIVATE_KEY);
            web3_http = new Web3(account, configData.RPC_HTTP);
            web3_http.TransactionManager.UseLegacyAsDefault = true;

            if (isPancake)
            {
                predictContract = web3_http.Eth.GetContract(ABI.PANCAKE_PREDICTION_ABI, pancakePredictionAddress);
                oracleContract = web3_http.Eth.GetContract(ABI.ORACLE_ABI, pancakeOracleAddress);
                func_oracleGetPrice = oracleContract.GetFunction("latestAnswer");
            }
            else
            {
                predictContract = web3_http.Eth.GetContract(ABI.CANDLEGENIE_PREDICTION_ABI, botData.PREDICTION_ADDRESS);
                httpClient = new HttpClient();
                priceSource = predictContract.GetFunction("priceSource").CallAsync<string>().Result;
            }

            func_GetCurrentEpoch = predictContract.GetFunction("currentEpoch");
            func_GetRound = predictContract.GetFunction(isPancake ? "rounds" : "Rounds");
            func_BetBear = predictContract.GetFunction(isPancake ? "betBear" : "user_BetBear");
            func_BetBull = predictContract.GetFunction(isPancake ? "betBull" : "user_BetBull");
            func_paused = predictContract.GetFunction("paused");


            this.gasPrice = new HexBigInteger(Web3.Convert.ToWei(data.GAS_PRICE, UnitConversion.EthUnit.Gwei));
            this.gasLimit = new HexBigInteger(data.GAS_LIMIT);

            Log(ExtractBotData());
        }

        private void Log(object obj)
        {
            Log(obj?.ToString());
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

        public async Task<bool> StartBot()
        {
            currentRound = await GetRoundData(await GetCurrentEpoch() - 1);
            if (debug)
                Log(currentRound.ToString());
            currentSequencial = 0;
            currentWins = 0;
            currentBet = baseBet;

            lastWinDate = DateTimeOffset.UtcNow - TimeSpan.FromDays(5);
            currentDate = DateTimeOffset.UtcNow;

            if (Running == false)
            {
                if (await CheckPredictionState() == false)
                {
                    Running = true;
                    token_source_loop = new CancellationTokenSource();
                    token_source_wait = new CancellationTokenSource();
                    ct_wait = token_source_wait.Token;
                    ct_loop = token_source_loop.Token;
                    Task.Run(MainLoop, ct_loop);

                    return true;
                }
                else
                {
                    Log("Falha ao iniciar PredictionBot -> Prediction está pausado!", true);
                    return false;
                }
            }
            else
            {
                Log("Bot já foi iniciado");
                return false;
            }

        }

        private async Task<bool> CheckPredictionState()
        {
            return await func_paused.CallAsync<bool>();
        }

        private async Task<Nethereum.RPC.Eth.DTOs.TransactionReceipt> Bet(PredictionSide side, BigInteger epoch, float betValue)
        {
            if (debug)
            {
                var receipt_debug = new Nethereum.RPC.Eth.DTOs.TransactionReceipt();
                receipt_debug.Status = new HexBigInteger(1);
                receipt_debug.TransactionHash = "DEBUG";
                return receipt_debug;
            }

            object[] paramters = new object[] { epoch };
            if (side == PredictionSide.BULL)
            {
                return await func_BetBull.SendTransactionAndWaitForReceiptAsync(from: account.Address, gas: gasLimit, gasPrice: gasPrice, value: new HexBigInteger(Web3.Convert.ToWei(betValue)), functionInput: paramters);
            }
            else
            {
                return await func_BetBear.SendTransactionAndWaitForReceiptAsync(from: account.Address, gas: gasLimit, gasPrice: gasPrice, value: new HexBigInteger(Web3.Convert.ToWei(betValue)), functionInput: paramters);
            }
        }

        private async Task MainLoop()
        {
            try
            {
                if (data.START_AT_END_DAY)
                {
                    var waitTimeToStart = TimeRemaningToNextDay() - TimeSpan.FromMinutes(1);
                    if (waitTimeToStart < TimeSpan.Zero) waitTimeToStart = TimeSpan.FromSeconds(1);
                    Log($"Waiting day({DateTime.UtcNow.Day}) end to start running -> {waitTimeToStart} h remaning", true);
                    await Task.Delay(waitTimeToStart, ct_wait);
                    StartBot().Wait();
                }

                BotState = BotStateEnum.Running;
                InvokeChangeEvent();
                Log("Bot running\n");

                while (Running)
                {
                    if (Running == false || ct_loop.IsCancellationRequested)
                    {
                        ForceStop();
                        ct_loop.ThrowIfCancellationRequested();
                        return;
                    }

                    if (await CheckPredictionState())
                    {
                        Log("Bot stopped -> Prediction is paused", true);
                        ForceStop();
                        return;
                    }

                    if (currentSequencial + 1 >= desiredSequencial)
                    {
                        Log($"Waiting to bet in round({currentRound.epoch + 1})");

                        // CALCULAR MEDIAS
                        List<IRound> medias = new List<IRound>();
                        for (int i = 0; i < 20; i++)
                        {
                            BigInteger epoch = currentRound.epoch - i;
                            medias.Add(await GetRoundData(epoch));
                        }

                        var waitTimeBeforeAttack = (currentRound.CloseTimestamp - DateTimeOffset.UtcNow) - TimeSpan.FromSeconds(secondsBeforeAttack);
                        if (waitTimeBeforeAttack < TimeSpan.Zero) waitTimeBeforeAttack = TimeSpan.FromSeconds(0);
                        Log($"Waiting to attack - {waitTimeBeforeAttack}");
                        await Task.Delay(waitTimeBeforeAttack, ct_wait);

                        if (Running == false || ct_loop.IsCancellationRequested)
                        {
                            ForceStop();
                            ct_loop.ThrowIfCancellationRequested();
                            return;
                        }

                        currentRound = await GetRoundData(currentRound.epoch);
                        var bnbPrice = await GetOraclePrice();

                        PredictionSide currentWinnerSide = currentRound.GetWinner(bnbPrice);

                        Log($"Bnb Price {bnbPrice}");
                        Log($"currentWinnerSide {currentWinnerSide}");

                        if (currentRound.GetMostProfitbleSide() != currentWinnerSide)
                        {
                            IRound nextRound = await GetRoundData(currentRound.epoch + 1);
                            PredictionSide betSide = nextRound.GetMostProfitbleSide();

                            if (betSide == PredictionSide.NONE)
                            {
                                Log("Currently price in draw, is not going to BET this round!", true);
                                return;
                            }

                            Nethereum.RPC.Eth.DTOs.TransactionReceipt txn_receipt = await Bet(betSide, currentRound.epoch + 1, currentBet);
                            Log(currentRound.ToString());

                            if (txn_receipt.Status.Value == 1)
                            {
                                if (debug)
                                {
                                    float media2_totalAmount = 0f;
                                    float media3_totalAmount = 0f;
                                    float media7_totalAmount = 0f;
                                    float media8_totalAmount = 0f;
                                    float media10_totalAmount = 0f;
                                    float media15_totalAmount = 0f;
                                    float media20_totalAmount = 0f;
                                    BigInteger media2_closePrice = 0;
                                    BigInteger media3_closePrice = 0;
                                    BigInteger media7_closePrice = 0;
                                    BigInteger media8_closePrice = 0;
                                    BigInteger media10_closePrice = 0;
                                    BigInteger media15_closePrice = 0;
                                    BigInteger media20_closePrice = 0;

                                    float media_totalAmount = 0f;
                                    BigInteger media_closePrice = 0;
                                    for (int i = 0; i < medias.Count; i++)
                                    {
                                        media_totalAmount += (float)medias[i].TotalAmount;
                                        if (i == 0)
                                        {
                                            media_closePrice += await GetOraclePrice();
                                        }
                                        else
                                        {
                                            media_closePrice += medias[i].closePrice;
                                        }
                                        switch (i + 1)
                                        {
                                            case 2:
                                                media2_totalAmount = media_totalAmount / (i + 1);
                                                media2_closePrice = media_closePrice / (i + 1);
                                                break;
                                            case 3:
                                                media3_totalAmount = media_totalAmount / (i + 1);
                                                media3_closePrice = media_closePrice / (i + 1);
                                                break;
                                            case 7:
                                                media7_totalAmount = media_totalAmount / (i + 1);
                                                media7_closePrice = media_closePrice / (i + 1);
                                                break;
                                            case 8:
                                                media8_totalAmount = media_totalAmount / (i + 1);
                                                media8_closePrice = media_closePrice / (i + 1);
                                                break;
                                            case 10:
                                                media10_totalAmount = media_totalAmount / (i + 1);
                                                media10_closePrice = media_closePrice / (i + 1);
                                                break;
                                            case 15:
                                                media15_totalAmount = media_totalAmount / (i + 1);
                                                media15_closePrice = media_closePrice / (i + 1);
                                                break;
                                            case 20:
                                                media20_totalAmount = media_totalAmount / (i + 1);
                                                media20_closePrice = media_closePrice / (i + 1);
                                                break;
                                        }
                                    }

                                    float round_TotalAmount = (float)nextRound.TotalAmount;
                                    Log($"Bet({betSide} Side) in Round ({currentRound.epoch + 1}) - Amount: {currentBet.ToString("0.#####")} BNB\n" +
                                    $"BullAmount: {nextRound.BullAmount.ToString("0.##")}\n" +
                                    $"BearAmount: {nextRound.BearAmount.ToString("0.##")}\n" +
                                    $"TotalAmount: {round_TotalAmount.ToString("0.##")}\n" +
                                    $"Current Price: {bnbPrice.ToString("")}\n" +
                                    $"Media2_ClosePrice: {media2_closePrice.ToString()}({(bnbPrice > media2_closePrice ? "+" + (((float)bnbPrice / (float)media2_closePrice) - 1f).ToString("0.##" + "%") : "-" + (1f - ((float)bnbPrice / (float)media2_closePrice)).ToString("0.##" + "%"))})\n" +
                                    $"Media3_ClosePrice: {media3_closePrice.ToString()}({(bnbPrice > media3_closePrice ? "+" + (((float)bnbPrice / (float)media3_closePrice) - 1f).ToString("0.##" + "%") : "-" + (1f - ((float)bnbPrice / (float)media3_closePrice)).ToString("0.##" + "%"))})\n" +
                                    // $"Media7_ClosePrice: {media7_closePrice.ToString()}({(bnbPrice > media7_closePrice ? "+" + (((float)bnbPrice / (float)media7_closePrice) - 1f).ToString("0.##" + "%") : "-" + (1f - ((float)bnbPrice / (float)media7_closePrice)).ToString("0.##" + "%"))})\n" +
                                    $"Media8_ClosePrice: {media8_closePrice.ToString()}({(bnbPrice > media8_closePrice ? "+" + (((float)bnbPrice / (float)media8_closePrice) - 1f).ToString("0.##" + "%") : "-" + (1f - ((float)bnbPrice / (float)media8_closePrice)).ToString("0.##" + "%"))})\n" +
                                    $"Media10_ClosePrice: {media10_closePrice.ToString()}({(bnbPrice > media10_closePrice ? "+" + (((float)bnbPrice / (float)media10_closePrice) - 1f).ToString("0.##" + "%") : "-" + (1f - ((float)bnbPrice / (float)media10_closePrice)).ToString("0.##" + "%"))})\n" +
                                    $"Media15_ClosePrice: {media15_closePrice.ToString()}({(bnbPrice > media15_closePrice ? "+" + (((float)bnbPrice / (float)media15_closePrice) - 1f).ToString("0.##" + "%") : "-" + (1f - ((float)bnbPrice / (float)media15_closePrice)).ToString("0.##" + "%"))})\n" +
                                    $"Media20_ClosePrice: {media20_closePrice.ToString()}({(bnbPrice > media20_closePrice ? "+" + (((float)bnbPrice / (float)media20_closePrice) - 1f).ToString("0.##" + "%") : "-" + (1f - ((float)bnbPrice / (float)media20_closePrice)).ToString("0.##" + "%"))})\n" +
                                    $"Media2_TotalAmount: {media2_totalAmount.ToString("0.##")}({(round_TotalAmount > media2_totalAmount ? "+" + ((round_TotalAmount / media2_totalAmount) - 1f).ToString("0.##" + "%") : "-" + (1f - (round_TotalAmount / media2_totalAmount)).ToString("0.##" + "%"))})\n" +
                                    $"Media3_TotalAmount: {media3_totalAmount.ToString("0.##")}({(round_TotalAmount > media3_totalAmount ? "+" + ((round_TotalAmount / media3_totalAmount) - 1f).ToString("0.##" + "%") : "-" + (1f - (round_TotalAmount / media3_totalAmount)).ToString("0.##" + "%"))})\n" +
                                    // $"Media7_TotalAmount: {media7_totalAmount.ToString("0.##")}({(round_TotalAmount > media7_totalAmount ? "+" + ((round_TotalAmount / media7_totalAmount) - 1f).ToString("0.##" + "%") : "-" + (1f - (round_TotalAmount / media7_totalAmount)).ToString("0.##" + "%"))})\n" +
                                    $"Media8_TotalAmount: {media8_totalAmount.ToString("0.##")}({(round_TotalAmount > media8_totalAmount ? "+" + ((round_TotalAmount / media8_totalAmount) - 1f).ToString("0.##" + "%") : "-" + (1f - (round_TotalAmount / media8_totalAmount)).ToString("0.##" + "%"))})\n" +
                                    $"Media10_TotalAmount: {media10_totalAmount.ToString("0.##")}({(round_TotalAmount > media10_totalAmount ? "+" + ((round_TotalAmount / media10_totalAmount) - 1f).ToString("0.##" + "%") : "-" + (1f - (round_TotalAmount / media10_totalAmount)).ToString("0.##" + "%"))})\n" +
                                    $"Media15_TotalAmount: {media15_totalAmount.ToString("0.##")}({(round_TotalAmount > media15_totalAmount ? "+" + ((round_TotalAmount / media15_totalAmount) - 1f).ToString("0.##" + "%") : "-" + (1f - (round_TotalAmount / media15_totalAmount)).ToString("0.##" + "%"))})\n" +
                                    $"Media20_TotalAmount: {media20_totalAmount.ToString("0.##")}({(round_TotalAmount > media20_totalAmount ? "+" + ((round_TotalAmount / media20_totalAmount) - 1f).ToString("0.##" + "%") : "-" + (1f - (round_TotalAmount / media20_totalAmount)).ToString("0.##" + "%"))})"
                                    , true);
                                }
                                else
                                {
                                    Log($"Bet({betSide} Side) in Round ({currentRound.epoch + 1}) - Amount: {currentBet.ToString("0.#####")} BNB\nTxn -> https://bscscan.com/tx/{txn_receipt.TransactionHash}\nWaiting results...", true);
                                }

                                var waitTimeToCurrentRoundEnd = (currentRound.CloseTimestamp - DateTimeOffset.UtcNow) + TimeSpan.FromSeconds(waitSecondsToClose);
                                if (waitTimeToCurrentRoundEnd < TimeSpan.Zero) waitTimeToCurrentRoundEnd = TimeSpan.FromSeconds(5);
                                Log($"Waiting current round end - {waitTimeToCurrentRoundEnd}");
                                await Task.Delay(waitTimeToCurrentRoundEnd, ct_wait);

                                if (Running == false || ct_loop.IsCancellationRequested)
                                {
                                    ForceStop();
                                    ct_loop.ThrowIfCancellationRequested();
                                    return;
                                }

                                currentRound = await GetRoundData(currentRound.epoch);

                                if (currentRound.GetMostProfitbleSide() != currentRound.GetWinnerSide())
                                {
                                    Log($"Last round closed like predicted({currentRound.epoch})");
                                }
                                else
                                {
                                    Log($"Last round fail to predict bnbPrice/betSide in round ({currentRound.epoch})", true);
                                }

                                currentRound = nextRound;

                                if (betSide != currentRound.GetMostProfitbleSide())
                                {
                                    Log($"Bet in wrong side profitable ({currentRound.epoch})", true);
                                }

                                var waitTimeToBetRoundEnd = (currentRound.CloseTimestamp - DateTimeOffset.UtcNow) + TimeSpan.FromSeconds(waitSecondsToClose);
                                if (waitTimeToBetRoundEnd < TimeSpan.Zero) waitTimeToBetRoundEnd = TimeSpan.FromSeconds(5);
                                Log(waitTimeToBetRoundEnd);
                                await Task.Delay(waitTimeToBetRoundEnd, ct_wait);

                                currentRound = await GetRoundData(currentRound.epoch);

                                while (currentRound.isClosed == false)
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(waitSecondsToClose), ct_wait);
                                    currentRound = await GetRoundData(currentRound.epoch);
                                }

                                string message;
                                if (currentRound.GetWinnerSide() == betSide)
                                {
                                    float totalGain = currentBet * (float)currentRound.RewardMultiplier;
                                    float profit = totalGain - currentBet;
                                    totalProfit += profit;
                                    message = $"Won Round ({currentRound.epoch}) - Bet ({betSide}) - {((float)currentRound.RewardMultiplier).ToString("0.##")}x\nTotal Gain: {(totalGain.ToString("0.#####"))} BNB\nProfit: {(profit).ToString("0.#####")} BNB";
                                    if (debug)
                                    {
                                        message += "\n" +
                                        $"ClosePrice: {currentRound.closePrice.ToString()}\n" +
                                        $"LockPrice: {currentRound.lockPrice.ToString()}\n" +
                                        $"BearAmount: {currentRound.BearAmount.ToString("0.##")}\n" +
                                        $"BullAmount: {currentRound.BullAmount.ToString("0.##")}\n" +
                                        $"TotalAmount: {currentRound.TotalAmount.ToString("0.##")}\n";
                                    }
                                    Log(message, true);
                                    currentWins += 1;
                                    totalWins += 1;
                                    currentBet = baseBet;
                                    lastWinDate = currentRound.CloseTimestamp;

                                }
                                else
                                {
                                    float oldBet = currentBet;
                                    currentBet *= data.MULTIPLIER_LOSS;
                                    message = $"Lost Round ({currentRound.epoch}) - Lose {oldBet.ToString("0.#####")} BNB\n Bet Increase to {currentBet.ToString("0.#####")} BNB";
                                    totalLoss += 1;
                                    if (debug)
                                    {
                                        message += "\n" +
                                        $"ClosePrice: {currentRound.closePrice.ToString()}\n" +
                                        $"LockPrice: {currentRound.lockPrice.ToString()}\n" +
                                        $"BearAmount: {currentRound.BearAmount.ToString("0.##")}\n" +
                                        $"BullAmount: {currentRound.BullAmount.ToString("0.##")}\n" +
                                        $"TotalAmount: {currentRound.TotalAmount.ToString("0.##")}\n";
                                    }
                                    Log(message, true);
                                }
                            }
                            else
                            {
                                Log($"Transaction failed\nTxn -> https://bscscan.com/tx/{txn_receipt.TransactionHash}", true);
                            }
                        }
                        else
                        {
                            Log(currentRound.ToString());
                            Log($"SkipBet ProfitableSide -> ({currentRound.GetMostProfitbleSide()}) / CurrentWinner -> {currentWinnerSide}");
                        }

                        currentSequencial = 0;
                    }
                    else
                    {
                        Log($"Esperando round({currentRound.epoch}) atual acabar");

                        var waitTime = (currentRound.CloseTimestamp - DateTimeOffset.UtcNow) + TimeSpan.FromSeconds(waitSecondsToClose);
                        if (waitTime < TimeSpan.Zero) waitTime = TimeSpan.FromSeconds(5);

                        if (Running == false || ct_loop.IsCancellationRequested)
                        {
                            ForceStop();
                            ct_loop.ThrowIfCancellationRequested();
                            return;
                        }

                        Log(waitTime);


                        await Task.Delay(waitTime, ct_wait);

                        if (Running == false || ct_loop.IsCancellationRequested)
                        {
                            ForceStop();
                            ct_loop.ThrowIfCancellationRequested();
                            return;
                        }

                        currentRound = await GetRoundData(currentRound.epoch);

                        while (currentRound.isClosed == false)
                        {
                            Log($"Round{currentRound.epoch} not closed... waiting more {waitSecondsToClose} seconds");
                            await Task.Delay(TimeSpan.FromSeconds(waitSecondsToClose), ct_wait);
                            currentRound = await GetRoundData(currentRound.epoch);

                            if (await CheckPredictionState())
                            {
                                Log("Prediction is paused");
                                ForceStop();
                                return;
                            }
                        }

                        Log($"Winner Side {currentRound.GetWinnerSide()} Closed: ({currentRound.closePrice}) Locked: ({currentRound.lockPrice})\nProfitable Side {currentRound.GetMostProfitbleSide()}");
                        if (currentRound.GetWinnerSide() != currentRound.GetMostProfitbleSide())
                        {
                            currentSequencial += 1;
                            Log($"+1 Sequencial({currentSequencial}) ({currentRound.epoch})");
                        }
                        else
                        {
                            currentSequencial = 0;
                            Log("Sequencial Zerado");
                        }
                    }

                    if (Running == false || ct_loop.IsCancellationRequested)
                    {
                        ForceStop();
                        ct_loop.ThrowIfCancellationRequested();
                        return;
                    }

                    currentRound = await GetRoundData(currentRound.epoch + 1);
                    if (currentRound.CloseTimestamp.Day != currentDate.Day)
                    {
                        if (data.RESET_BET_END_DAY)
                        {
                            currentBet = baseBet;
                        }
                        currentSequencial = 0;
                        currentWins = 0;
                        currentDate = currentRound.CloseTimestamp.Date;
                        Log($"The day ended\nThe current bet is ({currentBet}) BNB", true);
                    }

                    Log("");
                    Log($"Current Round -> {currentRound.epoch}");

                    if (currentRound.CloseTimestamp.Day == lastWinDate.Day && currentWins == data.WINS_PER_DAY)
                    {
                        if (Running == false || ct_loop.IsCancellationRequested)
                        {
                            ForceStop();
                            ct_loop.ThrowIfCancellationRequested();
                            return;
                        }

                        var waitTimeToNextDay = TimeRemaningToNextDay() - TimeSpan.FromMinutes(1);
                        if (waitTimeToNextDay < TimeSpan.Zero) waitTimeToNextDay = TimeSpan.FromSeconds(1);
                        Log($"Waiting until end of the day({currentRound.CloseTimestamp.Day}) {waitTimeToNextDay} hours remaning", true);


                        await Task.Delay(waitTimeToNextDay, ct_wait);

                        if (Running == false || ct_loop.IsCancellationRequested)
                        {
                            ForceStop();
                            ct_loop.ThrowIfCancellationRequested();
                            return;
                        }

                        currentRound = await GetRoundData(await GetCurrentEpoch() - 1);
                        currentSequencial = 0;
                        currentWins = 0;
                        currentBet = baseBet;
                        Log("New Day Started", true);
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
                Log("Error Prediction loop");
                Log(e.Message);
                Log(e.StackTrace);
            }
            finally
            {
                ForceStop();
            }
        }

        private TimeSpan TimeRemaningToNextDay()
        {
            return (DateTime.UtcNow.Date.AddDays(1) - DateTime.UtcNow);
        }

        private async Task<BigInteger> GetCurrentEpoch()
        {
            BigInteger currentEpoch = await func_GetCurrentEpoch.CallAsync<BigInteger>();
            return currentEpoch;
        }

        private async Task<IRound> GetRoundData(BigInteger roundEpoch)
        {
            try
            {
                object[] paramters = new object[1] { roundEpoch };
                IRound roundData;
                if (isPancake)
                {
                    roundData = await func_GetRound.CallDeserializingToObjectAsync<RoundDataPancake>(paramters);
                }
                else
                {
                    roundData = await func_GetRound.CallDeserializingToObjectAsync<RoundDataCandleGenie>(paramters);
                }

                return roundData;
            }
            catch (System.Exception ex)
            {

                Log("Error getting the round");
                Log(ex);
                await Task.Delay(TimeSpan.FromSeconds(1.5));
                return await GetRoundData(roundEpoch);
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
            $"Betting after: {desiredSequencial}\n" +
            $"BaseBet: {baseBet}\n" +
            $"CurrentBet: {currentBet}\n" +
            $"Profit: {totalProfit}\n" +
            $"Current Wins: {currentWins}\n" +
            $"Total Wins: {totalWins}\n" +
            $"Total Loss: {totalLoss}\n" +
            $"PredictionAddress: {data.PREDICTION_ADDRESS}" +
            "";
        }

        public override void ForceStop(bool fromTelegram=false)
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

        private async Task<BigInteger> GetOraclePrice()
        {
            try
            {
                if (isPancake)
                {
                    return await func_oracleGetPrice.CallAsync<BigInteger>();
                }
                else
                {
                    var response = await httpClient.GetStringAsync(priceSource);
                    return BigInteger.Parse(JsonConvert.DeserializeObject<PriceSourceResponse>(response).price.Replace(".", ""));
                }
            }
            catch (System.Exception e)
            {
                Log("Deu erro aqui no OraclePrice");
                Log(e);

                await Task.Delay(10);

                if (isPancake)
                {
                    return await func_oracleGetPrice.CallAsync<BigInteger>();
                }
                else
                {
                    var response = await httpClient.GetStringAsync(priceSource);
                    return BigInteger.Parse(JsonConvert.DeserializeObject<PriceSourceResponse>(response).price.Replace(".", ""));
                }
            }
        }

        public async Task<bool> ClaimAll()
        {
            try
            {
                var func_getUserRoundLength = predictContract.GetFunction("getUserRoundsLength");
                var func_claimable = predictContract.GetFunction("claimable");
                var func_getUserRounds = predictContract.GetFunction("getUserRounds");
                var func_claim = predictContract.GetFunction(isPancake ? "claim" : "user_Claim");

                int userRoundLength = await func_getUserRoundLength.CallAsync<int>(new object[] { account.Address });
                int cursor = userRoundLength >= 20 ? userRoundLength - 10 : 0;

                var roundsInfo = await func_getUserRounds.CallDeserializingToObjectAsync<UserRounds>(new object[] { account.Address, cursor, userRoundLength });

                var roundsToClaim = new List<BigInteger>();

                for (int i = 0; i < roundsInfo.quantity; i++)
                {
                    BigInteger epoch = roundsInfo.rounds[i];
                    if (roundsInfo.roundsInfo[i].claimed == false)
                    {
                        bool canClaim = await func_claimable.CallAsync<bool>(new object[] { epoch, account.Address });

                        if (canClaim == true)
                        {
                            roundsToClaim.Add(epoch);
                        }
                    }
                }

                if (roundsToClaim.Count > 0)
                {
                    var gasLimitEstimate = await func_claim.EstimateGasAsync(roundsToClaim);
                    gasLimitEstimate.Value += gasLimitEstimate.Value / new BigInteger(8);
                    var txn_recp = await func_claim.SendTransactionAndWaitForReceiptAsync(from: account.Address, gas: gasLimitEstimate, gasPrice: null, value: new HexBigInteger(0), functionInput: roundsToClaim);
                    if (txn_recp.Status.Value == 1)
                    {
                        Log($"Claimed [{(string.Join(",", roundsToClaim.ToArray()))}]\nTxn -> https://bscscan.com/tx/{txn_recp.TransactionHash}", true);
                    }
                    else
                    {
                        Log($"Failed to Claim [{(string.Join(",", roundsToClaim.ToArray()))}]\nTxn -> https://bscscan.com/tx/{txn_recp.TransactionHash}", true);
                    }
                }

                return true;
            }
            catch (System.Exception ex)
            {
                Log("Error claiming");
                Log(ex);
                return false;
            }
        }
    }

    public class PriceSourceResponse
    {
        public string symbol { get; set; }
        public string price { get; set; }
    }
}