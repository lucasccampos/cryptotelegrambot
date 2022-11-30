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
using System.Threading;
using System.Net.Http;
using TradingBot.Utils;
using Nethereum.RPC.Fee1559Suggestions;

namespace TradingBot
{

    public class PegaxyBot : Bot
    {

        public const string pegaxyContractAddress = "0xfb7316fb56fd2a8fdb925321b8f616eef106db33";
        public const string pgxContractAddress = "0xc1c93d475dc82fe72dbc7074d55f5a734f8ceeae";

        private Contract pegaxyContract;

        // private HttpClient httpClient;

        public Account Account { get; private set; }
        public Web3 Web3_http { get; private set; }

        private Function func_RentWithPassword;
        private Function func_RentSimple;
        public Fee1559 Fee;
        public float MaxGas;
        public HexBigInteger GasLimit;

        public float Porcentagem = 100f;
        public int Duration = 0;
        public int Energy = 0;
        public int PGX = 0;
        public int Count = 0;
        public int Tentativas = 0;
        public int TentativasReajuste = 0;

        private string logName;

        public bool Paused => Pause || ForcePause;
        private bool Pause;
        public bool ForcePause{get; private set;}

        private PegaxyData data;
        private CancellationTokenSource token_source_loop;
        private CancellationToken ct_loop;
        private CancellationTokenSource token_source_wait;
        private CancellationToken ct_wait;
        public DateTimeOffset StartDate { get; set; }

        private string ContaName;

        private bool debug;
        private bool forceStop = false;

        public bool Running => !forceStop;

        private float minGasCost => GetMinGasCost(Fee.MaxFeePerGas.Value, GasLimit);

        public PegaxyBot(ConfigData configData, Conta privateData, PegaxyData botData, long userID, long botID, string logName)
        {
            this.data = botData;
            this.Count = data.COUNT;
            this.Porcentagem = data.PORCENTAGEM;
            this.Duration = data.DURATION;
            this.Energy = data.ENERGY;
            this.PGX = data.PGX;
            BotType = BotTypeEnum.Pegaxy;



            this.ID = botID;
            this.UserID = userID;
            this.logName = logName;
            BotState = BotStateEnum.Running;
            debug = botData.DEBUG;
            this.ContaName = privateData.Name;


            Account = new Account(privateData.PrivateKey, 137);
            Web3_http = new Web3(Account, configData.POLYGON_RPC_HTTP);
            Web3_http.TransactionManager.UseLegacyAsDefault = true;

            this.Fee = data.FEE;
            this.GasLimit = new HexBigInteger(data.GAS_LIMIT);
            this.MaxGas = (int)Math.Ceiling((float)Web3.Convert.FromWei(this.Fee.MaxFeePerGas.Value, UnitConversion.EthUnit.Gwei));

            CheckBalance().Wait();

            pegaxyContract = Web3_http.Eth.GetContract(ABI.PEGAXY_ABI, pegaxyContractAddress);

            func_RentWithPassword = pegaxyContract.GetFunction("rentWithPassword");

            if (Duration > 0)
            {
                func_RentSimple = pegaxyContract.GetFunction("rent");
                var pgxContract = Web3_http.Eth.GetContract(ABI.ERC20_ABI, pgxContractAddress);
                if (WebUtils.CheckAllowance(this.Web3_http, pgxContract, pegaxyContractAddress, this.Account.Address, value: WebUtils.MaxApproveAmmount - 1).Result == false) // .Result
                {
                    Log($"Approving token...");

                    var approve_hash = WebUtils.Approve(pgxContract, pegaxyContractAddress, this.Account.Address, new HexBigInteger(Fee.MaxFeePerGas.Value)).Result; // .Result
                    Log($"Approve Txn : {approve_hash}");
                    ComunicationManager.SendMessage($"BOT ({ID}) TOKEN APPROVED - \nhttps://polygonscan.com/tx/{approve_hash}", UserID);
                }
            }

            StartDate = DateTimeOffset.UtcNow;

            Log(ExtractBotData());
        }

        private void NewWeb3Connection()
        {
            Log("Creating new Web3");

            Web3_http = new Web3(Account, OrdersManager.configData.POLYGON_RPC_HTTP);
            Web3_http.TransactionManager.UseLegacyAsDefault = true;

            pegaxyContract = Web3_http.Eth.GetContract(ABI.PEGAXY_ABI, pegaxyContractAddress);
            func_RentSimple = pegaxyContract.GetFunction("rent");
            func_RentWithPassword = pegaxyContract.GetFunction("rentWithPassword");
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
                message = $"Conta {ContaName}\nBOT({ID})\n{message}";
                ComunicationManager.SendMessage(message, UserID);
            }
        }

        public bool LegitGas()
        {
            if (this.forceStop)
            {
                return false;
            }

            if ((float)PegaxyBotManager.ActualFeeData.MaxFeePerGas * 0.9f > this.MaxGas)
            {
                PauseBot();
                Task.Run(() =>
                {
                    PegaxyBotManager.DesactiveBot(this);
                    Log("Sua ordem está com gás baixo\nSua ordem ficará pausada até você mudar o gás ou retornará automaticamente quando o gás da rede abaixar para o valor do gás da sua ordem", true);
                });
                return false;
            }

            // Fee1559 newFee = PegaxyBotManager.GasPriceNormalizer((int)this.MaxGas, (int)this.Porcentagem);
            // if (newFee.MaxFeePerGas > Fee.MaxFeePerGas)
            // {
            //     return false;
            // }
            // Fee = newFee;
            return true;
        }

        public void PauseBot(bool force = false)
        {
            if (this.Pause == false)
            {
                Log("Pause");
            }

            this.Pause = true;
            if (force)
            {
                ForcePause = true;
                Log("ForcePaused");
            }
        }

        public void UnpauseBot()
        {
            if (this.Pause)
            {
                Log("Unpause");
            }
            this.Pause = false;
        }

        public async Task EditGas(int newGas)
        {
            if (this.forceStop)
            {
                throw new MessageException($"Ordem já está parada!");
            }

            if (newGas < PegaxyBotManager.ActualFeeData.MaxFeePerGas)
            {
                throw new MessageException($"Gas abaixo do ideal!\nMinimo Necessario: {(int)PegaxyBotManager.ActualFeeData.MaxFeePerGas}");
            }

            Fee1559 newFee = PegaxyBotManager.GasPriceNormalizer(newGas, (int)Porcentagem);

            float balance = (float)Web3.Convert.FromWei(await WebUtils.GetAccountBalance(Web3_http, Account.Address));
            float minGasCost = PegaxyBot.GetMinGasCost(newFee.MaxFeePerGas.Value, GasLimit);

            if (balance < minGasCost)
            {
                throw new MessageException($"Voce não tem saldo suficiente na carteira pra fazer essa mudança!\nMinimo Necessario: {minGasCost}");
            }

            Fee = newFee;
            MaxGas = (int)Web3.Convert.FromWei(Fee.MaxFeePerGas.Value, UnitConversion.EthUnit.Gwei);
            if (ForcePause)
            {
                TentativasReajuste = Tentativas;
            }
            ForcePause = false;
            PegaxyBotManager.ActiveBot(this);
            Log($"Gas editado para {MaxGas}");
            InvokeChangeEvent();
        }

        public static float GetMinGasCost(BigInteger price, BigInteger limit)
        {
            return (float)Web3.Convert.FromWei(price * limit);
        }

        private async Task CheckBalance()
        {
            float balance = (float)Web3.Convert.FromWei(await WebUtils.GetAccountBalance(Web3_http, Account.Address));
            Log($"Balance: {balance}");

            if (balance < this.minGasCost)
            {
                Log($"Você está sem GAS !\nVocê precisa ter no minimo >: {this.minGasCost.ToString("0.#####")} MATIC\nEssa ordem e as suas outras ordens nessa carteira vão ser canceladas!", true);
                PegaxyBotManager.RemoveAllUserOrdersByAddress(Account.Address, ID);
                throw new MessageException("Sem gas !");
            }

        }

        public override string BotDetail()
        {
            return $"Conta {ContaName} \nBOT({ID})\n" +
            $"Ativada: {(Paused ? "Nao" : "Sim")}\n" +
            $"State: {BotState}\n" +
            $"Porcentagem: {this.data.PORCENTAGEM} %\n" +
            $"Total Cavalos: {this.data.COUNT}\n" +
            $"Cavalos comprados: {this.data.COUNT - this.Count}\n" +
            $"Posição fila: {1 + PegaxyBotManager.PositionQueue(this)}/{PegaxyBotManager.len_botList}\n" +
            $"Tentativas: {this.Tentativas}\n" +
            $"Gas: {Web3.Convert.FromWei(this.Fee.MaxFeePerGas.Value, UnitConversion.EthUnit.Gwei)}\n" +
            $"Gas Limit: {this.GasLimit.Value}\n" +
            "";
        }

        public override string ExtractBotData()
        {
            return JsonConvert.SerializeObject(data);
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


                if (BotState != BotStateEnum.Completed)
                {
                    Log($"Parou de funcionar", true);
                    BotState = BotStateEnum.Stopped;
                }
                else
                {
                    Log($"Completada com sucesso!", true);
                }

                InvokeChangeEvent();
                changedUpdateEvent = null;
                Log("Forcing Stop");

                if (fromTelegram == false)
                {
                    Task.Run(() => OrdersManager.Instance.RemoveBot(UserID, (int)ID, true));
                }
                else
                {
                    Log("Stop from telegram");
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

        public void SubtractCount()
        {
            Count -= 1;
            Log($"Subtraido, Atual Contagemm >: {Count}");
        }


        private void CheckOrderTooMuchTries()
        {
            if (Tentativas - TentativasReajuste >= PegaxyBotManager.MaxTriesOrder)
            {
                Log("Sua ordem está falhando muito e foi pausada\nPorfavor verifique se você já tem 3 cavalos nessa conta\n\n A ordem será ativada novamente quando você mudar o gás dela", true);
                PauseBot(true);
                PegaxyBotManager.DesactiveBot(this);
            }
        }

        public async Task RentSharedProfit(BigInteger horseID, float horsePorcentagem, string claimHash)
        {
            if (forceStop)
            {
                ForceStop();
                return;
            }
            try
            {
                if (Count < 0)
                {
                    Log("Count < 0");
                    Log("Nao deveria ter chegado aqui");
                    this.Count += 1;
                    return;
                }

                if (horsePorcentagem >= this.Porcentagem)
                {
                    var txn_hash = await RentWithPasswordTransaction(horseID, claimHash);
                    Log(txn_hash);

                    if (txn_hash.ToLower() == "falhou")
                    {
                        Log($"Falhou na Compra do Cavalo ({horseID}) - {horsePorcentagem}%\n*Vamos tentar novamente, nao se preocupe!*", true);
                        this.Count += 1;
                        this.Tentativas += 1;
                        CheckOrderTooMuchTries();
                        return;
                    }

                    Log($"Tentando comprar ({horseID}) - {horsePorcentagem}%\nEsperando transação ser confirmada - [Txn](https://polygonscan.com/tx/{txn_hash})", true);

                    await Task.Delay(TimeSpan.FromSeconds(60));

                    if (forceStop)
                    {
                        return;
                    }

                    Nethereum.RPC.Eth.DTOs.TransactionReceipt receipt = null;

                    for (int i = 0; i < PegaxyBotManager.TimeToTryCheckTransactionState; i++)
                    {
                        try
                        {
                            receipt = await Web3_http.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txn_hash);
                            if (receipt != null)
                            {
                                Log("receipt is not null");
                                break;
                            }
                            else
                            {
                                Log("receipt null");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Log("Erro ao tentar pegar txn receipt");
                            Log(ex);
                        }
                        await Task.Delay(TimeSpan.FromSeconds(60));
                    }

                    if (receipt == null)
                    {
                        Log("O Bot não conseguiu confirmar o resultado da transação, porfavor check manualmente\n" +
                        "Sua ordem deve estar com um gás mais baixo do que o aceitavel pela rede no momento\n" +
                        "Sua ordem ficará pausada até você mudar o gás ou retornará automaticamente quando o gás da rede abaixar para o valor do gás da sua ordem", true);

                        PegaxyBotManager.DesactiveBot(this);
                        this.Count += 1;
                        this.Tentativas += 1;
                        CheckOrderTooMuchTries();
                        return;
                    }

                    if (receipt.Status.Value == 1)
                    {
                        BotState = BotStateEnum.Completed;
                        Log($"Cavalo Comprado ({horseID}) - {horsePorcentagem}%", true);
                        PegaxyBotManager.TotalHorsesBought += 1;

                        if (this.Count <= 0)
                        {
                            // Log("Todos cavalos comprados\n Finalizando Ordem\n Boas corridas !", true);
                            ForceStop();
                        }
                        return;
                    }
                    else
                    {
                        string message = null;

                        if ((Tentativas - TentativasReajuste) > (int)(PegaxyBotManager.MaxTriesOrder / 2))
                        {
                            await Task.Delay(TimeSpan.FromMinutes(1));
                        }

                        try
                        {
                            message = await Web3_http.Eth.GetContractTransactionErrorReason.SendRequestAsync(receipt.TransactionHash);
                        }
                        catch (System.Exception ex)
                        {
                            message = ex.Message;
                        }

                        if ((String.IsNullOrEmpty(message) == false) && message.Contains("RentService: Cannot rent more"))
                        {
                            Log($"*Você já tem 3 cavalos em sua conta!*\nEssa ordem e as suas outras ordens nessa carteira vão ser canceladas!", true);
                            ForceStop();
                            PegaxyBotManager.RemoveAllUserOrdersByAddress(Account.Address, ID);
                            return;
                        }


                        await CheckBalance();

                        Log($"Falhou na Compra do Cavalo ({horseID}) - {horsePorcentagem}%\n*Vamos tentar novamente, nao se preocupe!*", true);
                        this.Count += 1;
                        this.Tentativas += 1;
                        CheckOrderTooMuchTries();
                    }
                }
            }
            catch (MessageException e)
            {
                Log("ERROR Rent Message Exception");
                Log(e.Message);
                ForceStop();
            }
            catch (System.Exception e)
            {
                Log("ERROR Rent Exception");
                Log(e);
                ForceStop();
            }

        }

        public async Task RentPay(BigInteger horseID)
        {
            if (forceStop)
            {
                ForceStop();
                return;
            }
            try
            {
                if (Count < 0)
                {
                    Log("Count < 0");
                    Log("Nao deveria ter chegado aqui");
                    this.Count += 1;
                    return;
                }

                // if (horsePorcentagem >= this.Porcentagem)
                // {
                var txn_hash = await RentSimpleTransaction(horseID);
                Log(txn_hash);

                if (txn_hash.ToLower() == "falhou")
                {
                    Log($"Falhou na Compra do Cavalo ({horseID})\n*Vamos tentar novamente, nao se preocupe!*", true);
                    this.Count += 1;
                    this.Tentativas += 1;
                    CheckOrderTooMuchTries();
                    return;
                }

                Log($"Tentando comprar ({horseID})\nEsperando transação ser confirmada - [Txn](https://polygonscan.com/tx/{txn_hash})", true);

                await Task.Delay(TimeSpan.FromSeconds(60));

                if (forceStop)
                {
                    return;
                }

                Nethereum.RPC.Eth.DTOs.TransactionReceipt receipt = null;

                for (int i = 0; i < PegaxyBotManager.TimeToTryCheckTransactionState; i++)
                {
                    try
                    {
                        receipt = await Web3_http.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txn_hash);
                        if (receipt != null)
                        {
                            Log("receipt is not null");
                            break;
                        }
                        else
                        {
                            Log("receipt null");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log("Erro ao tentar pegar txn receipt");
                        Log(ex);
                    }
                    await Task.Delay(TimeSpan.FromSeconds(60));
                }

                if (receipt == null)
                {
                    Log("O Bot não conseguiu confirmar o resultado da transação, porfavor check manualmente\n" +
                    "Sua ordem deve estar com um gás mais baixo do que o aceitavel pela rede no momento\n" +
                    "Sua ordem ficará pausada até você mudar o gás ou retornará automaticamente quando o gás da rede abaixar para o valor do gás da sua ordem", true);

                    PegaxyBotManager.DesactiveBot(this);
                    this.Count += 1;
                    this.Tentativas += 1;
                    CheckOrderTooMuchTries();
                    return;
                }

                if (receipt.Status.Value == 1)
                {
                    BotState = BotStateEnum.Completed;
                    Log($"Cavalo Comprado ({horseID})", true);
                    PegaxyBotManager.TotalHorsesBought += 1;

                    if (this.Count <= 0)
                    {
                        // Log("Todos cavalos comprados\n Finalizando Ordem\n Boas corridas !", true);
                        ForceStop();
                    }
                    return;
                }
                else
                {
                    string message = null;

                    if ((Tentativas - TentativasReajuste) > (int)(PegaxyBotManager.MaxTriesOrder / 2))
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1));
                    }

                    try
                    {
                        message = await Web3_http.Eth.GetContractTransactionErrorReason.SendRequestAsync(receipt.TransactionHash);
                    }
                    catch (System.Exception ex)
                    {
                        message = ex.Message;
                    }

                    if ((String.IsNullOrEmpty(message) == false) && message.Contains("RentService: Cannot rent more"))
                    {
                        Log($"*Você já tem 3 cavalos em sua conta!*\nEssa ordem e as suas outras ordens nessa carteira vão ser canceladas!", true);
                        ForceStop();
                        PegaxyBotManager.RemoveAllUserOrdersByAddress(Account.Address, ID);
                        return;
                    }


                    await CheckBalance();

                    Log($"Falhou na Compra do Cavalo ({horseID})\n*Vamos tentar novamente, nao se preocupe!*", true);
                    this.Count += 1;
                    this.Tentativas += 1;
                    CheckOrderTooMuchTries();
                }
                // }
            }
            catch (MessageException e)
            {
                Log("ERROR Rent Message Exception");
                Log(e.Message);
                ForceStop();
            }
            catch (System.Exception e)
            {
                Log("ERROR Rent Exception");
                Log(e);
                ForceStop();
            }

        }

        private async Task<String> RentSimpleTransaction(BigInteger horseID)
        {
            if (debug)
            {
                return "DEBUG";
            }

            object[] paramters = new object[] { horseID };
            try
            {
                Log("Enviando Transaction");
                return await func_RentSimple.SendTransactionAsync(from: Account.Address, gas: GasLimit, maxFeePerGas: new HexBigInteger(Fee.MaxFeePerGas.Value), maxPriorityFeePerGas: new HexBigInteger(Fee.MaxPriorityFeePerGas.Value), value: new HexBigInteger(BigInteger.Zero), functionInput: paramters);
                // return await func_RentWithPassword.SendTransactionAsync(from: Account.Address, gas: GasLimit, gasPrice: GasPrice, value: new HexBigInteger(BigInteger.Zero), functionInput: paramters);
            }
            catch (Nethereum.JsonRpc.Client.RpcResponseException e)
            {
                if (e.Message.Contains("insufficient funds for gas"))
                {
                    PegaxyBotManager.RemoveAllUserOrdersByAddress(Account.Address, ID);
                    Log($"Você está sem GAS !\nVocê precisa ter no minimo >: {this.minGasCost.ToString("0.#####")} MATIC ", true);
                    throw e;
                }

                Log("Rpc Response error");
                Log(e);
            }
            catch (Nethereum.JsonRpc.Client.RpcClientTimeoutException e)
            {
                Log("Rpc Timeout");
                Log(e);

                await Task.Delay(TimeSpan.FromSeconds(26));

                NewWeb3Connection();
            }
            catch (Nethereum.JsonRpc.Client.RpcClientUnknownException e)
            {
                Log("Rpc Unknown");
                Log(e);
                await Task.Delay(TimeSpan.FromSeconds(26));

                NewWeb3Connection();
            }
            catch (Exception e)
            {
                Log("Error mandando rent transacao");
                Log(e);
                throw e;
            }

            return "falhou";
        }

        private async Task<String> RentWithPasswordTransaction(BigInteger horseID, String claimHash)
        {
            if (debug)
            {
                return "DEBUG";
            }

            object[] paramters = new object[] { horseID, claimHash };
            try
            {
                Log("Enviando Transaction");
                return await func_RentWithPassword.SendTransactionAsync(from: Account.Address, gas: GasLimit, maxFeePerGas: new HexBigInteger(Fee.MaxFeePerGas.Value), maxPriorityFeePerGas: new HexBigInteger(Fee.MaxPriorityFeePerGas.Value), value: new HexBigInteger(BigInteger.Zero), functionInput: paramters);
                // return await func_RentWithPassword.SendTransactionAsync(from: Account.Address, gas: GasLimit, gasPrice: GasPrice, value: new HexBigInteger(BigInteger.Zero), functionInput: paramters);
            }
            catch (Nethereum.JsonRpc.Client.RpcResponseException e)
            {
                if (e.Message.Contains("insufficient funds for gas"))
                {
                    PegaxyBotManager.RemoveAllUserOrdersByAddress(Account.Address, ID);
                    Log($"Você está sem GAS !\nVocê precisa ter no minimo >: {this.minGasCost.ToString("0.#####")} MATIC ", true);
                    throw e;
                }

                Log("Rpc Response error");
                Log(e);
            }
            catch (Nethereum.JsonRpc.Client.RpcClientTimeoutException e)
            {
                Log("Rpc Timeout");
                Log(e);

                await Task.Delay(TimeSpan.FromSeconds(26));

                NewWeb3Connection();
            }
            catch (Nethereum.JsonRpc.Client.RpcClientUnknownException e)
            {
                Log("Rpc Unknown");
                Log(e);
                await Task.Delay(TimeSpan.FromSeconds(26));

                NewWeb3Connection();
            }
            catch (Exception e)
            {
                Log("Error mandando rent transacao");
                Log(e);
                throw e;
            }

            return "falhou";
        }
    }
}