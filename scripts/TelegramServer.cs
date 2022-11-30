using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Bot;
using System.Threading;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TradingBot.Data;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net;
using Telegram.Bot.Types.ReplyMarkups;
using System.IO;
using Telegram.Bot.Types.InputFiles;

namespace TradingBot.Telegram
{
    public class TelegramBotServer
    {
        private string telegramKey;
        public TelegramBotClient botClient { get; private set; }
        public CancellationTokenSource ctSource { get; private set; }

        private const string SUPPORT_TELEGRAM = "@joaodosbots";

        public static TelegramBotServer Instance { get; private set; }

        private UsersManager usersManager;
        private OrdersManager ordersManager;

        private bool askMessages = false;
        private string stopMessage = "Em manutenção";

        private string logName;
        private Dictionary<UserPlanEnum, Dictionary<string, Action<string, UserData>>> dictAuthCommandsUsers;
        private Dictionary<string, Action<long, string>> nonAuthCommandsFunctions;

        private JsonConverter[] converters = new JsonConverter[] { new BooleanJsonConverter(), new TokenBSCJsonConverter(), new DexBSCJsonConverter(), new PredictionBSCJsonConverter() };

        private int ignoreMessages = 5;
        private DateTimeOffset botStartTime;
        private bool Running = false;
        private bool messageLoop = true;


        private List<MessageStruct> messages = new List<MessageStruct>();

        public TelegramBotServer()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Log("TelegramBot já criado");
                return;
            }

            usersManager = UsersManager.Instance;
            ordersManager = OrdersManager.Instance;
            logName = "Telegram-" + new Random().Next(0, 9999).ToString() + HelperFunctions.GetRandomCharacterFromString("abcdefghijkl");

            dictAuthCommandsUsers = new Dictionary<UserPlanEnum, Dictionary<string, Action<string, UserData>>>();

            dictAuthCommandsUsers.Add(UserPlanEnum.Expirado, new Dictionary<string, Action<string, UserData>>(){
                {"/update_key", Command_UpdateLicenseKey},
                {"/upgrade_key", Command_UpdateLicenseKey},
                {"/id", Command_ID},
                {"/remover", Command_RemoveOrder},
                {"/todas_ordens", Command_AllOrdersList},
                {"/mudar_gas", Command_PegaxyEditGasOrder},
                {"/contas", Command_VerContas},
            });

            dictAuthCommandsUsers.Add(UserPlanEnum.Basic, new Dictionary<string, Action<string, UserData>>(){
                {"/start", Command_Start},
                {"/privatekey", Command_SetPrivateKey},
                {"/allorders", Command_AllOrdersList},
                {"/orders", Command_AllOrdersList},
                {"/ordens", Command_AllOrdersList},
                {"/todas_ordens", Command_AllOrdersList},
                {"/data", Command_DataPlaceHolder},
                {"/order", Command_OrderDetails},
                {"/ordem", Command_OrderDetails},
                {"/remove", Command_RemoveOrder},
                {"/remover", Command_RemoveOrder},
                {"/remover_ordem", Command_RemoveOrder},
                {"/key", Command_LicenseDetails},
                {"/chave", Command_LicenseDetails},
                {"/id", Command_ID},
                {"/update_key", Command_UpdateLicenseKey},
                {"/upgrade_key", Command_UpdateLicenseKey},
            });

            dictAuthCommandsUsers.Add(UserPlanEnum.Pegaxy, new Dictionary<string, Action<string, UserData>>(){
                {"/pegar_cavalo", Command_StartPegaxySharedProfitBot},
                {"/alugar_cavalo", Command_StartPegaxySharedProfitBot},
                {"/pegar_cavalo_fee", Command_StartPegaxyRentPayBot},
                {"/alugar_cavalo_fee", Command_StartPegaxyRentPayBot},
                {"/adicionar_conta", Command_AdicionarConta},
                {"/add_conta", Command_AdicionarConta},
                {"/contas", Command_VerContas},
                {"/conta_ativa", Command_SetarContaAtiva},
                {"/mudar_gas", Command_PegaxyEditGasOrder},
                {"/fila", Command_QueuePegaxy},
                {"/status", Command_StatusPegaxy},
                // {"/snipe", Command_StartSniperBot},
            });

            HelperFunctions.AddRange(dictAuthCommandsUsers[UserPlanEnum.Pegaxy], dictAuthCommandsUsers[UserPlanEnum.Basic]);

            dictAuthCommandsUsers.Add(UserPlanEnum.PegaxyVip, new Dictionary<string, Action<string, UserData>>());

            HelperFunctions.AddRange(dictAuthCommandsUsers[UserPlanEnum.PegaxyVip], dictAuthCommandsUsers[UserPlanEnum.Pegaxy]);

            dictAuthCommandsUsers.Add(UserPlanEnum.Bronze, new Dictionary<string, Action<string, UserData>>(){
                {"/trade", Command_StartTradeBot},
                {"/add_token", Command_AddToken},
                {"/remove_token", Command_RemoveToken},
                {"/tokens", Command_GetAllTokens},
            });

            HelperFunctions.AddRange(dictAuthCommandsUsers[UserPlanEnum.Bronze], dictAuthCommandsUsers[UserPlanEnum.Basic]);

            dictAuthCommandsUsers.Add(UserPlanEnum.MOD, new Dictionary<string, Action<string, UserData>>(){
                {"/remover_conta", Command_RemoverConta},
                {"/launchpad", Command_StartLaunchPadBot},
                {"/safelaunch", Command_SafeLaunchDeals},
                {"/log", Command_Log},
                {"/generate_key", Command_GenerateKey},
                {"/pegaxy_queue", Command_QueueWithDetailsPegaxy},
                {"/pegaxy_gas", Command_ChangeGasPegaxy},
                {"/pegaxy_gas_min", Command_ChangeGasMin},
                {"/pegaxy_gas_base", Command_ChangeGasBasePegaxy},
                {"/pegaxy_gas_high", Command_ChangeGasMinWithHighestPorcePegaxy},
                {"/conta_pegaxy", Command_CreateAccountPegaxy},
                {"/contas_user", Command_VerContasUser},
                {"/remover_ordem_user", Command_RemoveUserOrder},
                {"/ordens_user", Commmand_VerOrdensUser},
                {"/key_user", Command_LicenseDetailsUser},
                // MUDAR LICENÇA
                {"/pegaxy_gaslimit", Command_ChangeGasLimitPegaxy},
                {"/pegaxy_gaslimit_auto", Command_ChangeGasLimitAuto},
                {"/pegaxy_gas_auto", Command_ChangeGasAutoTracker},
                {"/pegaxy_gas_auto_multiplier", Command_ChangeGasAutoMultiplier},
                {"/pegaxy_gas_multiplier", Command_PegaxyGasMultiplier},
                {"/pegaxy_gashigh_multiplier", Command_PegaxyGasHighMultiplier},
                {"/pegaxy_lowgas_multiplier", Command_PegaxyLowGasMultiplier},
                {"/pegaxy_maxcaptchas", Command_ChangeMaxCaptchaToken},
                {"/pegaxy_delay", Command_ChangeDelayPegaxy},
                {"/pegaxy_ignore_porce", Command_PegaxyIgnorePorce},
                {"/pegaxy_max_porce", Command_PegaxyMaxPorceOrder},
                {"/pegaxy_order_expiration", Command_PegaxyOrderExpiration},
                {"/pegaxy_captcha_expiration", Command_PegaxyCaptchaExpiration},
                {"/pegaxy_commands", Command_PegaxyCommands},
                {"/pegaxy_data", Command_PegaxyManagerData},
                {"/pegaxy_openorder", Command_ChangeOpenOrderPegaxy},
                {"/pegaxy_canorder", Command_ChangeOpenOrderPegaxy},
                {"/telegram_messageloop", Command_SetStateMessageLoop},
                {"/pegaxy_checktime", Command_PegaxyCheckTime},
                {"/pegaxy_maxorder", Command_ChangeMaxOrderPerAddress},
                {"/pegaxy_maxtries", Command_ChangeMaxTriesOrder},
            });

            if (Program.PredictionProgram == true)
            {
                dictAuthCommandsUsers[UserPlanEnum.Bronze].Remove("/trade");
                dictAuthCommandsUsers[UserPlanEnum.Bronze].Add("/prediction", Command_StartPredictionBot);
            }
            HelperFunctions.AddRange(dictAuthCommandsUsers[UserPlanEnum.MOD], dictAuthCommandsUsers[UserPlanEnum.Bronze]);
            HelperFunctions.AddRange(dictAuthCommandsUsers[UserPlanEnum.MOD], dictAuthCommandsUsers[UserPlanEnum.Pegaxy]);

            dictAuthCommandsUsers.Add(UserPlanEnum.ADMIN, new Dictionary<string, Action<string, UserData>>(){
                {"/remake_http", Command_RemakeHttp},
                {"/remover_user", Command_RemoverContaUser},
                {"/block", Command_BlockUser},
                {"/unblock", Command_UnblockUser},
                {"/pegaxy_debug", Command_ChangeDebugMode},
                {"/pegaxy_pause", Command_ChangePause},
                {"/sudo", Command_Sudo},
                {"/telegram", Command_TelegramState},
                {"/adicionar_limite", Command_AdicionarContasLimite},
                {"/add_limite", Command_AdicionarContasLimite},
                {"/log_server", Command_LogServer},
                {"/stop_server", Command_Test}, // TEST
                {"/_pause_orders", Command_NotImplemented},
                {"/_stop_server", Command_NotImplemented},
                {"/_restart_server", Command_NotImplemented},
            });
            HelperFunctions.AddRange(dictAuthCommandsUsers[UserPlanEnum.ADMIN], dictAuthCommandsUsers[UserPlanEnum.MOD]);

            nonAuthCommandsFunctions = new Dictionary<string, Action<long, string>>(){
                {"/key", Command_SetLicenseKey},
                {"/start", Command_Start},
                {"/id", Command_ID},
            };

            botStartTime = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5);

            StartMessageLoop();
        }


        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (ignoreMessages > 0)
            {
                ignoreMessages--;
                return;
            }

            if (update.Type != UpdateType.Message)
                return;
            if (update.Message.Type != MessageType.Text)
                return;
            long chatId = update.Message.Chat.Id;

            string[] splits = update.Message.Text.Split(" ", 2);
            string msgCommand = splits[0].ToLower();
            string msgData = splits.Length > 1 ? splits[1].Trim() : "";
            Console.WriteLine($"msg-'{msgCommand}' - (({chatId}))");
            Log($"msg-'{msgCommand}' - (({chatId}))");

            await HandleCommand(chatId, msgData, msgCommand);
        }

        private async Task HandleCommand(long chatId, string msgData, string msgCommand)
        {
            try
            {
                UserData user = usersManager.Get(chatId);

                if (user == default(UserData) || user == null) //New User
                {
                    if (askMessages == false)
                    {
                        SendMessage(chatId, stopMessage);
                        return;
                    }

                    if (nonAuthCommandsFunctions.TryGetValue(msgCommand, out var commandFunction))
                    {
                        commandFunction.Invoke(chatId, msgData);
                    }
                    else
                    {
                        Command_NotFound(chatId);
                    }
                }
                else
                {
                    if (user.LicenseKeyExpireDate < DateTime.UtcNow)
                    {
                        if (dictAuthCommandsUsers[UserPlanEnum.Expirado].TryGetValue(msgCommand, out var command))
                        {
                            command.Invoke(msgData, user);
                        }
                        else
                        {
                            SendMessage(chatId, "Sua licença do BOT está expirada, por favor renove ela com seu revendedor");
                        }

                        return;
                    }

                    if (user.Block == true)
                    {
                        SendMessage(chatId, "Seu acesso ao BOT está bloqueado, verifique sua renovação com o revendedor");
                        return;
                    }

                    if (user.UserPlan != UserPlanEnum.ADMIN)
                    {
                        if (askMessages == false)
                        {
                            SendMessage(chatId, stopMessage);
                            return;
                        }
                    }

                    if (dictAuthCommandsUsers[user.UserPlan].TryGetValue(msgCommand, out var commandFunction))
                    {
                        commandFunction.Invoke(msgData, user);
                    }
                    else
                    {
                        Command_NotFound(chatId);
                    }
                }
            }
            catch (System.Exception e)
            {
                Log(e.ToString());
                Console.WriteLine(e);
            }
        }

        public void SendMessage(long chatId, string message, bool normal = false)
        {
            if (Running == false) return;
            // var keyboard = new InlineKeyboardMarkup(new []
            // {
            //     new InlineKeyboardButton[]
            //     {
            //         "A",
            //         "B"
            //     },
            //     new InlineKeyboardButton[]
            //     {
            //         new InlineKeyboardButton("cu")
            //         {
            //             Text = "C",
            //             Url = "https://www.nuget.org/packages/Telegram.Bot/"
            //         }, 
            //         "D"
            //     } 
            // }
            // );

            IEnumerable<string> msgChunks = new string[] { message };
            try
            {

                if (message.Length > 4095)
                {
                    msgChunks = HelperFunctions.ChunksUpto(message, 4095);
                }
            }
            catch (System.Exception ex)
            {
                Log("Error creating msg chunk");
                Log($"message: {message}");
                Log(ex);
            }


            foreach (string msg in msgChunks)
            {
                messages.Add(new MessageStruct(chatId, msg, normal));
            }
        }

        public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            try
            {
                Log("Erro telegram server");
                Console.WriteLine("Erro telegram server");
                if (exception is ApiRequestException apiRequestException)
                {
                    Log(apiRequestException.ToString());
                    Console.WriteLine(apiRequestException.ToString());
                }
                Log(exception);
                Console.WriteLine(exception);

                await StartBot(this.telegramKey);
            }
            catch (System.Exception e)
            {
                Log("Error no Handle Error");
                Log(e);
            }
        }


        private async Task StartMessageLoop()
        {
            messageLoop = true;
            Task.Run(MessageLoop);
            Log("Message Loop iniciado");
        }

        private async Task StopMessageLoop()
        {
            messageLoop = false;
            Log("Message Loop desativado");
        }

        public async Task MessageLoop()
        {
            while (messageLoop)
            {
                await Task.Delay(100);

                foreach (var item in messages.ToArray())
                {
                    if (item != null)
                    {

                        try
                        {
                            await botClient.SendTextMessageAsync(
                                chatId: item.chatId,
                                text: item.message,
                                parseMode: item.normal ? ParseMode.Html : ParseMode.Markdown,
                                replyMarkup: null
                            );
                        }
                        catch (System.Exception ex)
                        {
                            if (item.normal)
                            {
                                Log("Error sending message telegram");
                                Log($"message: {item.message}");
                                Log(ex);
                            }
                            else
                            {
                                item.normal = true;
                                messages.Add(item);
                            }
                        }
                    }
                    messages.Remove(item);
                }
            }
        }

        public void CreateBotTelegram(string telegramKey)
        {
            this.telegramKey = telegramKey;
            this.botClient = new TelegramBotClient(telegramKey);
            this.ctSource = new CancellationTokenSource();
        }

        public async Task StartBot(string telegramKey)
        {
            if ((DateTimeOffset.UtcNow - botStartTime) < TimeSpan.FromMinutes(5))
            {
                this.ignoreMessages = 3;
                return;
            }

            botStartTime = DateTimeOffset.UtcNow;
            Running = false;

            if (botClient == null)
            {
                CreateBotTelegram(telegramKey);
            }
            else
            {
                try
                {
                    ctSource?.Cancel();
                    Log("Closing Async telegram");
                    await Task.Delay(1000);
                    ctSource?.Dispose();
                    this.ctSource = new CancellationTokenSource();
                    // return;
                    // Log("Deleting Webhook");
                    // await botClient.DeleteWebhookAsync(true);
                    // await botClient.CloseAsync();
                    // Log("Closed1");
                }
                catch (System.Exception ex)
                {
                    Log("Error");
                    Log(ex);
                    throw ex;
                }
                Log("Closed");
            }

            this.ignoreMessages = 5;
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                try
                {
                    botClient.StartReceiving(new DefaultUpdateHandler(updateHandler: HandleUpdateAsync, errorHandler: HandleErrorAsync), cancellationToken: ctSource.Token);
                    Running = true;
                }
                catch (Exception e)
                {
                    Log("Error start receiving");
                    Log(e);
                }
                Log("Started2");
            });
            Log("Started");
        }

        private async Task IgnoreOldMessages(string telegramKey)
        {
            return;

            try
            {
                if (botClient == null)
                {
                    CreateBotTelegram(telegramKey);
                }

                var usuarios = new List<long>();
                var ct = new CancellationTokenSource();
                botClient.StartReceiving(new DefaultUpdateHandler(updateHandler: HandleUpdateTemp, errorHandler: HandleErrorTemp), cancellationToken: ct.Token);

                await Task.Delay(TimeSpan.FromSeconds(5));

                foreach (var user_id in usuarios)
                {
                    SendMessage(user_id, "Try send your message now!");
                }

                ct.Cancel();
                ct = null;

                await botClient.DeleteWebhookAsync(true);
                await botClient.CloseAsync();

                botClient = null;

                Task HandleErrorTemp(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
                {
                    Console.WriteLine("HandleErrorTemp");
                    return null;
                }

                Task HandleUpdateTemp(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
                {
                    long id = update.Message.Chat.Id;
                    if (usuarios.Contains(id) == false)
                    {
                        usuarios.Add(id);
                    }
                    Console.WriteLine("HandleUpdateTemp");

                    return null;
                }
            }
            catch (System.Exception ex)
            {
                Log($"Error ignoring old messages: {ex}");
            }
        }

        private void Command_SetLicenseKey(long userID, string msgData)
        {
            try
            {

                UserData user = usersManager.Get(userID);
                if (!(user == default(UserData) || user == null)) return;

                LicenseKeyData licenseData = LicenseManager.ManagerInstance.UseLicenseKey(userID, msgData);

                if (licenseData == null)
                {
                    SendMessage(userID, $"Chave de licença invalida, tente novamente com uma chave valida!\nVocê pode comprar uma com {SUPPORT_TELEGRAM}");
                    return;
                }

                //TODO: fix @Username
                usersManager.NewUser(userID, "@placeholder", licenseData);
                SendMessage(userID, "Chave de licença aprovada, obrigado por comprar nosso serviço.");
            }
            catch (Exception e)
            {
                Log(e.ToString());
                SendMessage(userID, $"Falhou ao tentar usar a chave de licença!\nSe o problema persistir fale com {SUPPORT_TELEGRAM}");
            }
        }

        private void Command_UpdateLicenseKey(string msgData, UserData user)
        {
            try
            {
                LicenseKeyData licenseData = LicenseManager.ManagerInstance.UseLicenseKey(user.ChatId, msgData);

                if (licenseData == null)
                {
                    SendMessage(user.ChatId, $"Chave de licença invalida, tente novamente com uma chave valida!\nVocê pode comprar uma com {SUPPORT_TELEGRAM}");
                    return;
                }

                user.SetLicenseKey(licenseData);

                SendMessage(user.ChatId, "Chave de licença atualizada, obrigado por continuar utilizando nosso serviço.");
            }
            catch (Exception e)
            {
                Log(e.ToString());
                SendMessage(user.ChatId, $"Falhou ao tentar usar a chave de licença!\nSe o problema persistir fale com {SUPPORT_TELEGRAM}");
            }
        }

        private void Command_SetPrivateKey(string msgData, UserData user)
        {
            try
            {
                //TODO : verificar se essa privateKey é valida antes de aprovar
                if (String.IsNullOrEmpty(msgData))
                {
                    SendMessage(user.ChatId, $"Please enter a wallet private key while passing the command.\nex: '/privatekey your_private_key_here'");
                    return;
                }
                user.SetPrivateKey(msgData);
                SendMessage(user.ChatId, $"Private key saves successfully");
            }
            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                Console.WriteLine(e);
                SendMessage(user.ChatId, $"Error trying to setup private key ! \nCheck your private key and try again\nIf the problem persists contact our support {SUPPORT_TELEGRAM}");
            }
        }

        private async void Command_Test(string msgData, UserData user)
        {
            await StartBot(this.telegramKey);
        }

        #region Bots

        private async void Command_StartTradeBot(string msgData, UserData user)
        {
            if (String.IsNullOrEmpty(user.PrivateKey))
            {
                SendMessage(user.ChatId, "You need setup your wallet private key first. use /privatekey\nex: '/privatekey your_private_key_here'");
                return;
            }
            try
            {
                TradeData data = JsonConvert.DeserializeObject<TradeData>(msgData, converters);

                long botID = ordersManager.CreateTradeBot(data, user);

                SendMessage(user.ChatId, $"Trading Bot started successfully\nID: {botID} -> /order {botID}");

                await Task.Delay(500);
                Command_OrderDetails($"{botID}", user);
            }
            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                Console.WriteLine(e);
                SendMessage(user.ChatId, $"Error: Trading bot not started! \nCheck your data and try again\nIf the problem persists contact our support {SUPPORT_TELEGRAM}");
            }
        }

        private async void Command_StartSniperBot(string msgData, UserData user)
        {
            if (String.IsNullOrEmpty(user.PrivateKey))
            {
                SendMessage(user.ChatId, "You need setup your wallet private key first. use /privatekey\nex: '/privatekey your_private_key_here'");
                return;
            }
            try
            {
                SniperData data = JsonConvert.DeserializeObject<SniperData>(msgData, converters);

                long botID = ordersManager.CreateSniperBot(data, user);

                SendMessage(user.ChatId, $"Sniper Bot started successfully\nID: {botID} -> /order {botID}");

                await Task.Delay(500);
                Command_OrderDetails($"{botID}", user);
            }
            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                Console.WriteLine(e);
                SendMessage(user.ChatId, $"Error: Sniper bot not started! \nCheck your data and try again\nIf the problem persists contact our support {SUPPORT_TELEGRAM}");
            }
        }

        private async void Command_StartPredictionBot(string msgData, UserData user)
        {
            if (String.IsNullOrEmpty(user.PrivateKey))
            {
                SendMessage(user.ChatId, "You need setup your wallet private key first. use /privatekey\nex: '/privatekey your_private_key_here'");
                return;
            }
            try
            {
                PredictionData data = JsonConvert.DeserializeObject<PredictionData>(msgData, converters);

                long botID = ordersManager.CreatePredictionBot(data, user).Result;

                SendMessage(user.ChatId, $"Prediction Bot started successfully\nID: {botID} -> /order {botID}");

                await Task.Delay(300);
                Command_OrderDetails($"{botID}", user);
            }
            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                Console.WriteLine(e);
                SendMessage(user.ChatId, $"Error: Prediction bot not started! \nCheck your data and try again\nIf the problem persists contact our support {SUPPORT_TELEGRAM}");
            }
        }

        private async void Command_StartLaunchPadBot(string msgData, UserData user)
        {
            if (String.IsNullOrEmpty(user.PrivateKey))
            {
                SendMessage(user.ChatId, "You need setup your wallet private key first. use /privatekey\nex: '/privatekey your_private_key_here'");
                return;
            }
            try
            {
                SafeLaunchData data = JsonConvert.DeserializeObject<SafeLaunchData>(msgData, converters);

                long botID = ordersManager.CreateSafeLaunchBot(data, user).Result;

                SendMessage(user.ChatId, $"SafeLaunch Bot started successfully\nID: {botID} -> /order {botID}");

                await Task.Delay(100);
                Command_OrderDetails($"{botID}", user);
            }
            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                Console.WriteLine(e);
                SendMessage(user.ChatId, $"Error: SafeLaunch bot not started! \nCheck your data and try again\nIf the problem persists contact our support {SUPPORT_TELEGRAM}");
            }
        }

        private async void Command_StartPegaxySharedProfitBot(string msgData, UserData user)
        {
            if (String.IsNullOrEmpty(user.GetActivePrivateKey()))
            {
                SendMessage(user.ChatId, $"Você precisa registrar uma conta no bot\n fale com o adminstrador {SUPPORT_TELEGRAM} pra ele te ajudar!");
                return;
            }
            try
            {
                string[] splits = msgData.Split(" ", 2); // porcentage count
                float porcentagem = float.Parse(splits[0].Replace("%", ""));
                int gas = 0;

                if (user.UserPlan < UserPlanEnum.PegaxyVip)
                {
                    if (PegaxyBotManager.canOpenOrder == false)
                    {
                        SendMessage(user.ChatId, $"Este comando está desabilitado no momento\n{PegaxyBotManager.stopMessage}");
                        return;
                    }

                }

                if (splits.Length > 1)
                {
                    gas = (int)float.Parse(splits[1]);
                }

                if (gas == 0)
                {
                    gas = (int)PegaxyBotManager.ActualFeeData.MaxFeePerGas;
                }


                if (porcentagem > (PegaxyBotManager.MaxPercentage))
                {
                    porcentagem = PegaxyBotManager.MaxPercentage;
                }
                else if (porcentagem < 1f)
                {
                    porcentagem = 1f;
                }

                PegaxyData data = new PegaxyData()
                {
                    PORCENTAGEM = porcentagem,
                    COUNT = 1,
                    FEE = PegaxyBotManager.GasPriceNormalizer(gas, (int)porcentagem),
                    GAS_LIMIT = PegaxyBotManager.GasLimitPegaxy,
                    DEBUG = false
                };

                long botID = ordersManager.CreatePegaxyBot(data, user);

                SendMessage(user.ChatId, $"Ordem iniciada com sucesso\nOrdem ID: {botID} -> /order {botID}");

                await Task.Delay(100);
                Command_OrderDetails($"{botID}", user);
            }
            catch (MessageException e)
            {
                Log(e.Message);
                SendMessage(user.ChatId, $"Error: Pegaxy bot\nSua ordem não foi iniciada!\n{e.Message} \n Se o problema persistir fale com {SUPPORT_TELEGRAM}");
            }
            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                Console.WriteLine(e);
                SendMessage(user.ChatId, $"Error: Pegaxy bot\nSua ordem não foi iniciada!\nSe o problema persistir fale com {SUPPORT_TELEGRAM}");
            }
        }

        private async void Command_StartPegaxyRentPayBot(string msgData, UserData user)
        {
            if (String.IsNullOrEmpty(user.GetActivePrivateKey()))
            {
                SendMessage(user.ChatId, $"Você precisa registrar uma conta no bot\n fale com o adminstrador {SUPPORT_TELEGRAM} pra ele te ajudar!");
                return;
            }
            try
            {
                string[] splits = msgData.Split(" ", 3); // pgx duration(days) gas
                int pgx = (int)float.Parse(splits[0]);
                int duration = (int)float.Parse(splits[1]);
                int gas = 0;

                if (user.UserPlan < UserPlanEnum.PegaxyVip)
                {
                    if (PegaxyBotManager.canOpenOrder == false)
                    {
                        SendMessage(user.ChatId, $"Este comando está desabilitado no momento\n{PegaxyBotManager.stopMessage}");
                        return;
                    }

                }

                if (splits.Length > 2)
                {
                    gas = (int)float.Parse(splits[2]);
                }

                if (gas == 0)
                {
                    gas = (int)PegaxyBotManager.ActualFeeData.MaxFeePerGas;
                }


                if (pgx < 5)
                {
                    pgx = 5;
                }

                if (duration < 1)
                {
                    duration = 1;
                }

                PegaxyData data = new PegaxyData()
                {
                    PGX = pgx,
                    DURATION = duration * 24 * 60 * 60,
                    COUNT = 1,
                    FEE = PegaxyBotManager.GasPriceNormalizer(gas, 15),
                    GAS_LIMIT = PegaxyBotManager.GasLimitPegaxy,
                    DEBUG = false
                };

                long botID = ordersManager.CreatePegaxyBot(data, user);

                SendMessage(user.ChatId, $"Ordem iniciada com sucesso\nOrdem ID: {botID} -> /order {botID}");

                await Task.Delay(100);
                Command_OrderDetails($"{botID}", user);
            }
            catch (MessageException e)
            {
                Log(e.Message);
                SendMessage(user.ChatId, $"Error: Pegaxy bot\nSua ordem não foi iniciada!\n{e.Message} \n Se o problema persistir fale com {SUPPORT_TELEGRAM}");
            }
            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                Console.WriteLine(e);
                SendMessage(user.ChatId, $"Error: Pegaxy bot\nSua ordem não foi iniciada!\nSe o problema persistir fale com {SUPPORT_TELEGRAM}");
            }
        }

        #endregion

        #region Pegaxy

        private async void Command_CreateAccountPegaxy(string msgData, UserData user)
        {
            try
            {
                string[] splits = msgData.Split(" ", 2); // nome endereço privatekey
                long userID = long.Parse(splits[0].Trim());
                string privateKey = splits[1].Trim();

                UserData newUser = usersManager.Get(userID);
                if (newUser == default(UserData) || newUser == null)
                {
                    string key = LicenseManager.ManagerInstance.CreateNewKey(user, UserPlanEnum.Pegaxy, TimeSpan.FromDays(31));
                    Command_SetLicenseKey(userID, key);
                    await Task.Delay(500);
                    newUser = usersManager.Get(userID, true);

                    SendMessage(user.ChatId, $"Novo cliente registrado com sucesso!");
                }

                if (newUser.Accounts.Count == newUser.AccountLimit)
                {
                    newUser.AddLimitAccount(1);
                    await Task.Delay(500);
                }

                Command_AdicionarConta($"conta_{newUser.Accounts.Count} {privateKey}", newUser);

                SendMessage(newUser.ChatId, $"Conta configurada com sucesso!");
                SendMessage(user.ChatId, $"Conta Adicionada !");
            }
            catch (MessageException e)
            {
                Log(e.Message);
                SendMessage(user.ChatId, e.Message);
            }
            catch (System.Exception e)
            {
                Log(e);
                SendMessage(user.ChatId, "Falhou ao tentar criar a conta, veja se digitou corretamente o comando!");
            }
        }

        private void Command_QueuePegaxy(string msgData, UserData user)
        {
            try
            {
                string queue = PegaxyBotManager.GetAllQueue();
                SendMessage(user.ChatId, $"{queue}");
            }
            catch (System.Exception e)
            {
                Log(e);
                SendMessage(user.ChatId, $"Falhou ao dar esse comando\nFale com o suporte {SUPPORT_TELEGRAM}");
            }
        }

        private void Command_StatusPegaxy(string msgData, UserData user)
        {
            try
            {
                string msg = PegaxyBotManager.GetStatus();
                SendMessage(user.ChatId, $"{msg}");
            }
            catch (System.Exception e)
            {
                Log(e);
                SendMessage(user.ChatId, $"Falhou ao dar esse comando\nFale com o suporte {SUPPORT_TELEGRAM}");
            }
        }

        private void Command_QueueWithDetailsPegaxy(string msgData, UserData user)
        {
            try
            {
                string queue = PegaxyBotManager.GetAllQueueWithDetails();
                SendMessage(user.ChatId, $"{queue}", normal: true);
            }
            catch (System.Exception e)
            {
                Log(e);
                SendMessage(user.ChatId, $"Falhou ao dar esse comando\nFale com o suporte {SUPPORT_TELEGRAM}");
            }
        }

        private async void Command_ChangeDelayPegaxy(string msgData, UserData user)
        {
            try
            {
                PegaxyBotManager.loopDelay_ms = (int)float.Parse(msgData);
                SendMessage(user.ChatId, $"Pegaxy Delay mudado para {PegaxyBotManager.loopDelay_ms} ms");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_ChangeGasMin(string msgData, UserData user)
        {
            try
            {
                // PegaxyBotManager.MinGasPegaxy = (int)float.Parse(msgData);
                // SendMessage(user.ChatId, $"Pegaxy MinGasPegaxy mudado para {PegaxyBotManager.MinGasPegaxy}");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_ChangeGasMinWithHighestPorcePegaxy(string msgData, UserData user)
        {
            try
            {
                // PegaxyBotManager.MinGasPegaxyHighPorce = (int)float.Parse(msgData);
                // SendMessage(user.ChatId, $"Pegaxy MinGasPegaxyHighPorce mudado para {PegaxyBotManager.MinGasPegaxyHighPorce}");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_ChangeGasBasePegaxy(string msgData, UserData user)
        {
            try
            {
                // PegaxyBotManager.BaseGasPegaxy = (int)float.Parse(msgData);
                // SendMessage(user.ChatId, $"Pegaxy BaseGasPegaxy mudado para {PegaxyBotManager.BaseGasPegaxy}");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_ChangeGasPegaxy(string msgData, UserData user)
        {
            try
            {
                // PegaxyBotManager.GasPegaxy = float.Parse(msgData);
                // SendMessage(user.ChatId, $"Pegaxy Default Gas mudado para {PegaxyBotManager.GasPegaxy}");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_ChangeGasLimitPegaxy(string msgData, UserData user)
        {
            try
            {
                PegaxyBotManager.GasLimitPegaxy = (int)float.Parse(msgData);
                SendMessage(user.ChatId, $"Pegaxy Gas Limit mudado para {PegaxyBotManager.GasLimitPegaxy}");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_PegaxyMaxPorceOrder(string msgData, UserData user)
        {
            try
            {
                PegaxyBotManager.MaxPercentage = (int)float.Parse(msgData);
                SendMessage(user.ChatId, $"Pegaxy Max Porcentagem mudado para {PegaxyBotManager.MaxPercentage} %");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_PegaxyIgnorePorce(string msgData, UserData user)
        {
            try
            {
                PegaxyBotManager.IgnorePercentage = (int)float.Parse(msgData);
                SendMessage(user.ChatId, $"Pegaxy Ignore Porcentagem mudado para {PegaxyBotManager.IgnorePercentage} %");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_PegaxyGasMultiplier(string msgData, UserData user)
        {
            try
            {
                PegaxyBotManager.GasMultipler = float.Parse(msgData);
                SendMessage(user.ChatId, $"Pegaxy Gas Multiplier mudado para {PegaxyBotManager.GasMultipler}");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_PegaxyGasHighMultiplier(string msgData, UserData user)
        {
            try
            {
                PegaxyBotManager.GasMultipler = float.Parse(msgData);
                SendMessage(user.ChatId, $"Pegaxy Gas High Multiplier mudado para {PegaxyBotManager.GasHighPorceMultipler}");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_PegaxyLowGasMultiplier(string msgData, UserData user)
        {
            try
            {
                PegaxyBotManager.GasLowerMultipler = float.Parse(msgData);
                SendMessage(user.ChatId, $"Pegaxy Low Gas Multiplier mudado para {PegaxyBotManager.GasLowerMultipler}");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_PegaxyCaptchaExpiration(string msgData, UserData user)
        {
            try
            {
                PegaxyBotManager.CaptchaTokenExpirationTime = TimeSpan.FromSeconds((int)float.Parse(msgData));
                SendMessage(user.ChatId, $"Pegaxy Captcha Expiration mudado para {PegaxyBotManager.CaptchaTokenExpirationTime.TotalSeconds} segundos");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_PegaxyOrderExpiration(string msgData, UserData user)
        {
            try
            {
                PegaxyBotManager.BotExpirationTime = TimeSpan.FromMinutes((int)float.Parse(msgData));
                SendMessage(user.ChatId, $"Pegaxy Order Expiration mudado para {PegaxyBotManager.BotExpirationTime.TotalMinutes} minutos");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_PegaxyCheckTime(string msgData, UserData user)
        {
            try
            {
                PegaxyBotManager.TimeToTryCheckTransactionState = (int)float.Parse(msgData);
                SendMessage(user.ChatId, $"Pegaxy Check Time mudado para {PegaxyBotManager.TimeToTryCheckTransactionState} minutos");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_ChangeMaxOrderPerAddress(string msgData, UserData user)
        {
            try
            {
                PegaxyBotManager.MaxOrdersPerAddress = (int)float.Parse(msgData);
                SendMessage(user.ChatId, $"Pegaxy Max Order per Address mudado para {PegaxyBotManager.MaxOrdersPerAddress}");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_ChangeMaxTriesOrder(string msgData, UserData user)
        {
            try
            {
                PegaxyBotManager.MaxTriesOrder = (int)float.Parse(msgData);
                SendMessage(user.ChatId, $"Pegaxy Max Order per Address mudado para {PegaxyBotManager.MaxTriesOrder}");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_ChangeMaxCaptchaToken(string msgData, UserData user)
        {
            try
            {
                PegaxyBotManager.MaxCaptchaTokens = (int)float.Parse(msgData);
                SendMessage(user.ChatId, $"Pegaxy MaxCaptchaTokens mudado para {PegaxyBotManager.MaxCaptchaTokens}");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_PegaxyManagerData(string msgData, UserData user)
        {
            try
            {
                string message = "" +
                $"Telegram State: {askMessages}\n" +
                $"Horse Bought >: {PegaxyBotManager.TotalHorsesBought}\n" +
                $"Pegaxy Debug >: {PegaxyBotManager.DebugMode}\n" +
                $"Pegaxy Paused >: {PegaxyBotManager.Paused}\n" +
                $"Can Order: {PegaxyBotManager.canOpenOrder}\n" +
                $"Order Checker Time: {PegaxyBotManager.TimeToTryCheckTransactionState}\n" +
                $"Gas Auto: {PegaxyBotManager.AutoGas}\n" +
                $"Gas Auto Multiplier: {PegaxyBotManager.AutoMultiplier}\n" +
                $"Gas Multiplier: {PegaxyBotManager.GasMultipler}\n" +
                $"Gas Low Multiplier: {PegaxyBotManager.GasLowerMultipler}\n" +
                $"Gas High Porce Multiplier: {PegaxyBotManager.GasHighPorceMultipler}\n" +
                $"Max Order per Address: {PegaxyBotManager.MaxOrdersPerAddress}\n" +
                $"Max Order tries: {PegaxyBotManager.MaxTriesOrder}\n" +
                // $"Gas Default: {PegaxyBotManager.GasPegaxy}\n" +
                $"Gas Base: {PegaxyBotManager.ActualFeeData?.BaseFee}\n" +
                $"Gas MaxFeePerGas: {PegaxyBotManager.ActualFeeData?.MaxFeePerGas}\n" +
                $"Gas MaxPriorityFeePerGas: {PegaxyBotManager.ActualFeeData?.MaxPriorityFeePerGas}\n" +
                $"Gas Limit Auto : {PegaxyBotManager.AutoGasLimit}\n" +
                $"Gas Limit: {PegaxyBotManager.GasLimitPegaxy}\n" +
                $"Delay: {PegaxyBotManager.loopDelay_ms} ms\n" +
                $"Captchas Solved: {PegaxyBotManager.TotalCaptchaSolved}\n" +
                $"MaxClaim Captcha: {PegaxyBotManager.MaxCaptchaTokens}\n" +
                $"Processing Captcha: {PegaxyBotManager.ProcessingCaptcha}\n" +
                $"Captchas Completed: {PegaxyBotManager.CaptchaTokenList?.ToArray().Length}\n" +
                $"Captcha Expiration: {PegaxyBotManager.CaptchaTokenExpirationTime.TotalSeconds} segundos\n" +
                $"Order Expiration: {PegaxyBotManager.BotExpirationTime.TotalMinutes} minutos\n" +
                $"Max Porce: {PegaxyBotManager.MaxPercentage} %\n" +
                $"Ignore Porce: {PegaxyBotManager.IgnorePercentage} %\n" +
                "";

                SendMessage(user.ChatId, message);
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_PegaxyCommands(string msgData, UserData user)
        {
            try
            {
                string message = "";

                foreach (var item in dictAuthCommandsUsers[user.UserPlan].Keys)
                {
                    message += item + "\n";
                }

                SendMessage(user.ChatId, message, normal: true);
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_ChangeGasLimitAuto(string msgData, UserData user)
        {
            try
            {
                PegaxyBotManager.AutoGasLimit = bool.Parse(msgData);

                SendMessage(user.ChatId, $"Pegaxy Gas Limit Auto >: {PegaxyBotManager.AutoGasLimit}");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_ChangeGasAutoTracker(string msgData, UserData user)
        {
            try
            {
                PegaxyBotManager.AutoGas = bool.Parse(msgData);

                SendMessage(user.ChatId, $"Pegaxy Gas Auto Tracker >: {PegaxyBotManager.AutoGas}");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_ChangeDebugMode(string msgData, UserData user)
        {
            try
            {
                PegaxyBotManager.DebugMode = bool.Parse(msgData);

                SendMessage(user.ChatId, $"Pegaxy DebugMode >: {PegaxyBotManager.DebugMode}");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_ChangePause(string msgData, UserData user)
        {
            try
            {
                PegaxyBotManager.SetPauseState(bool.Parse(msgData));

                SendMessage(user.ChatId, $"Pegaxy Paused >: {PegaxyBotManager.Paused}");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_ChangeGasAutoMultiplier(string msgData, UserData user)
        {
            try
            {
                PegaxyBotManager.AutoMultiplier = bool.Parse(msgData);

                SendMessage(user.ChatId, $"Pegaxy Gas Auto Multiplier >: {PegaxyBotManager.AutoMultiplier}");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_ChangeOpenOrderPegaxy(string msgData, UserData user)
        {
            try
            {
                string[] splits = msgData.Split(" ", 2);
                PegaxyBotManager.canOpenOrder = bool.Parse(splits[0]);
                if (splits.Length > 1)
                {
                    PegaxyBotManager.stopMessage = splits[1];
                }

                SendMessage(user.ChatId, $"Pegaxy Can Open Order State >: {PegaxyBotManager.canOpenOrder}\n{PegaxyBotManager.stopMessage}");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_SetStateMessageLoop(string msgData, UserData user)
        {
            try
            {
                bool state = bool.Parse(msgData);

                if (state)
                {
                    StartMessageLoop();
                }
                else
                {
                    StopMessageLoop();
                }

                SendMessage(user.ChatId, $"Telegram Message Loop >: {this.messageLoop}");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private async void Command_PegaxyEditGasOrder(string msgData, UserData user)
        {
            try
            {
                string[] splits = msgData.Split(" ", 2);
                long orderID = long.Parse(splits[0]);
                int newGas = Int32.Parse(splits[1]);

                PegaxyBot bot = (PegaxyBot)ordersManager.GetBot(orderID);

                if (bot == null)
                {
                    throw new MessageException("Esta ordem não existe");
                }

                if (bot.UserID != user.ChatId)
                {
                    throw new MessageException("Você não é o dono desta ordem!");
                }

                await bot.EditGas(newGas);


                SendMessage(user.ChatId, $"Bot ({orderID}) - Gás alterado com sucesso");
                await Task.Delay(100);
                Command_OrderDetails($"{bot.ID}", user);
            }
            catch (MessageException e)
            {
                Log(e.Message);
                SendMessage(user.ChatId, e.Message);
            }
            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error: Não foi possivel editar sua ordem");
            }
        }

        #endregion

        private async void Command_TelegramState(string msgData, UserData user)
        {
            try
            {
                string[] splits = msgData.Split(" ", 2);
                askMessages = bool.Parse(splits[0]);
                if (splits.Length > 1)
                {
                    stopMessage = splits[1];
                }

                SendMessage(user.ChatId, $"Telegram Stage >: {askMessages}\n{stopMessage}");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error");
            }
        }

        private void Command_DataPlaceHolder(string msgData, UserData user)
        {
            if (String.IsNullOrEmpty(user.PrivateKey))
            {
                SendMessage(user.ChatId, "You need setup your wallet private key first. use /privatekey\nex: '/privatekey your_private_key_here'");
                return;
            }
            try
            {
                msgData = msgData.ToLower();
                object botData = null;
                switch (msgData)
                {
                    case "prediction":
                        botData = new PredictionData();
                        break;
                    case "sniper":
                        botData = new SniperData();
                        break;
                    case "trade":
                        botData = new TradeData();
                        break;
                    case "launchpad":
                        botData = new SafeLaunchData();
                        break;
                }

                if (botData == null)
                {
                    SendMessage(user.ChatId, "Type with the bot type (prediction, sniper, trade)");
                    return;
                }

                string jsonPlaceHolder = JsonConvert.SerializeObject(botData, Formatting.Indented);

                SendMessage(user.ChatId, jsonPlaceHolder, true);
            }
            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                Console.WriteLine(e);
                SendMessage(user.ChatId, $"Error: Prediction bot not started! \nCheck your data and try again\nIf the problem persists contact our support {SUPPORT_TELEGRAM}");
            }
        }

        private void Command_OrderDetails(string msgData, UserData user)
        {
            try
            {
                if (String.IsNullOrEmpty(msgData) == false)
                {
                    if (Int32.TryParse(msgData, out int result))
                    {
                        string resultMessage = ordersManager.BotDetail(user.ChatId, result);
                        SendMessage(user.ChatId, resultMessage);
                        return;
                    }
                }

                SendMessage(user.ChatId, "Falhou, Porfavor coloque um numero de ordem valido!");
            }
            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                Console.WriteLine(e);
                SendMessage(user.ChatId, $"Error trying to get order details ! \nCheck order ID and try again\nIf the problem persists contact our support {SUPPORT_TELEGRAM}");
            }
        }

        private async void Command_Log(string msgData, UserData user)
        {
            {
                try
                {
                    if (String.IsNullOrEmpty(msgData) == false)
                    {
                        if (Int32.TryParse(msgData, out int result))
                        {
                            string botLogName = ordersManager.OrderLogName(user.ChatId, result);

                            if (String.IsNullOrEmpty(botLogName))
                            {
                                SendMessage(user.ChatId, "This log does not exist!");
                                return;
                            }

                            string logPath = Logger.LogPath(botLogName);

                            using (FileStream stream = System.IO.File.Open(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                using (StreamReader reader = new StreamReader(stream))
                                {
                                    // while (!reader.EndOfStream)
                                    // {
                                    InputOnlineFile inputOnlineFile = new InputOnlineFile(stream, logPath);
                                    await botClient.SendDocumentAsync(user.ChatId, inputOnlineFile);
                                    // }
                                }
                            }
                            return;
                        }
                    }

                    SendMessage(user.ChatId, "Failed, Please place a valid order id !");
                }
                catch (Exception e)
                {
                    Log("ERROR - " + e.ToString());
                    Console.WriteLine(e);
                    SendMessage(user.ChatId, $"Error trying to get log!");
                }
            }
        }

        private void Command_AllOrdersList(string msgData, UserData user)
        {
            try
            {
                string resposta = "Suas ordens:";
                var lista = ordersManager.GetBotList(user.ChatId);
                if (lista.Count > 0)
                {

                    foreach (var num in lista)
                    {
                        resposta += $"\n- {num}";
                    }
                }
                else
                {
                    resposta = "Você não tem nenhuma ordem ativa.";
                }

                SendMessage(user.ChatId, resposta);

            }
            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                Console.WriteLine(e);
                SendMessage(user.ChatId, $"Error trying to get all orders! \nTry again\nIf the problem persists contact our support {SUPPORT_TELEGRAM}");
            }
        }

        private void Command_RemoveOrder(string msgData, UserData user)
        {
            try
            {
                string resposta;
                bool hasParsed = Int32.TryParse(msgData, out int orderID);
                if (hasParsed && ordersManager.RemoveBot(user.ChatId, orderID))
                    resposta = $"Ordem({orderID}) parada e removida com sucesso!";
                else
                    resposta = $"Falhou ao tentar remover a ordem({orderID})!\nCheque o numero da ordem e tente novamente\nSe o problema persisti fale com nosso suporte {SUPPORT_TELEGRAM}";

                SendMessage(user.ChatId, resposta);

            }
            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                Console.WriteLine(e);
                SendMessage(user.ChatId, $"Falhou ao tentar remover a ordem!\nCheque o numero da ordem e tente novamente\nSe o problema persisti fale com nosso suporte {SUPPORT_TELEGRAM}");
            }
        }

        private void Command_RemoveUserOrder(string msgData, UserData user)
        {
            try
            {
                string resposta;
                bool hasParsed = Int32.TryParse(msgData, out int orderID);
                if (hasParsed && ordersManager.RemoveBot(user.ChatId, orderID, force: true))
                    resposta = $"Ordem({orderID}) parada e removida com sucesso!";
                else
                    resposta = $"Falhou ao tentar remover a ordem({orderID})!\nCheque o numero da ordem e tente novamente\nSe o problema persisti fale com nosso suporte {SUPPORT_TELEGRAM}";

                SendMessage(user.ChatId, resposta);

            }
            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                Console.WriteLine(e);
                SendMessage(user.ChatId, $"Falhou ao tentar remover a ordem!\nCheque o numero da ordem e tente novamente\nSe o problema persisti fale com nosso suporte {SUPPORT_TELEGRAM}");
            }
        }

        private async void Command_RemakeHttp(string msgData, UserData user)
        {
            SendMessage(user.ChatId, $"Bot pausado");
            SendMessage(user.ChatId, $"Refazendo HttpConections");
            SendMessage(user.ChatId, $"Espere 5 minutos");
            await PegaxyBotManager.RemakeAllHttpClientConnections();
            SendMessage(user.ChatId, $"Bot despausado");
        }

        private void Command_ID(string msgData, UserData user)
        {
            SendMessage(user.ChatId, $"Telegram ID: {user.ChatId}");
        }

        private void Command_ID(long userID, string msgData)
        {
            SendMessage(userID, $"Telegram ID: {userID}");
        }

        private void Command_LicenseDetails(string msgData, UserData user)
        {
            SendMessage(user.ChatId, $"License\nPlan: {user.UserPlan.ToString()}\nExpire in {user.LicenseKeyExpireDate}");
        }

        private void Command_LicenseDetailsUser(string msgData, UserData user)
        {
            try
            {
                long userID = long.Parse(msgData.Trim());

                UserData userCliente = usersManager.Get(userID);
                if ((userCliente == default(UserData) || userCliente == null)) return;

                SendMessage(user.ChatId, $"License ({userID})\nPlan: {userCliente.UserPlan.ToString()}\nExpire in {userCliente.LicenseKeyExpireDate}");
            }
            catch (System.Exception)
            {
                SendMessage(user.ChatId, $"Falhou ao dar esse comando\nFale com o suporte {SUPPORT_TELEGRAM}");
            }
        }

        private void Commmand_VerOrdensUser(string msgData, UserData user)
        {
            try
            {
                long userID = long.Parse(msgData.Trim());

                string resposta = $"({userID}) ordens:";
                var lista = ordersManager.GetBotList(userID);
                if (lista.Count > 0)
                {

                    foreach (var num in lista)
                    {
                        resposta += $"\n- {num}";
                    }
                }
                else
                {
                    resposta = "Você não tem nenhuma ordem ativa.";
                }

                SendMessage(user.ChatId, resposta);

            }
            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                Console.WriteLine(e);
                SendMessage(user.ChatId, $"Error trying to get all orders! \nTry again\nIf the problem persists contact our support {SUPPORT_TELEGRAM}");
            }
        }
        private void Command_VerContasUser(string msgData, UserData user)
        {
            try
            {
                long userID = long.Parse(msgData.Trim());

                UserData userCliente = usersManager.Get(userID);
                if ((userCliente == default(UserData) || userCliente == null)) return;


                SendMessage(user.ChatId, $"Contas ({userID}) \nConta Ativa >: {userCliente.ActiveAccount}\nContas >:\n{userCliente.ListAccounts()}", normal: true);
            }
            catch (System.Exception)
            {
                SendMessage(user.ChatId, $"Falhou ao dar esse comando\nFale com o suporte {SUPPORT_TELEGRAM}");
            }
        }

        private void Command_VerContas(string msgData, UserData user)
        {
            try
            {
                SendMessage(user.ChatId, $"Conta Ativa >: {user.ActiveAccount}\nContas >:\n{user.ListAccounts()}", normal: true);
            }
            catch (System.Exception)
            {
                SendMessage(user.ChatId, $"Falhou ao dar esse comando\nFale com o suporte {SUPPORT_TELEGRAM}");
            }
        }

        private void Command_SetarContaAtiva(string msgData, UserData user)
        {
            try
            {
                user.SetActiveAccount(Int32.Parse(msgData.Trim()));

                SendMessage(user.ChatId, $"{user.GetActiveAccount()?.Name}\nConta Ativa >: {user.ActiveAccount}");
            }
            catch (System.Exception)
            {
                SendMessage(user.ChatId, $"Falhou ao dar esse comando\nFale com o suporte {SUPPORT_TELEGRAM}");
            }
        }

        private void Command_GenerateKey(string msgData, UserData user)
        {
            try
            {
                var splits = msgData.Split(" ");
                UserPlanEnum planEnum;

                switch (splits[0].ToLower())
                {
                    case "bronze":
                        {
                            planEnum = UserPlanEnum.Bronze;
                            break;
                        }
                    case "silver":
                        {
                            planEnum = UserPlanEnum.Silver;
                            break;
                        }
                    case "gold":
                        {
                            planEnum = UserPlanEnum.Gold;
                            break;
                        }
                    case "diamond":
                        {
                            planEnum = UserPlanEnum.Diamond;
                            break;
                        }
                    case "mod":
                        {
                            planEnum = UserPlanEnum.MOD;
                            break;
                        }
                    case "pegaxy":
                        {
                            planEnum = UserPlanEnum.Pegaxy;
                            break;
                        }
                    case "pegaxyvip":
                        {
                            planEnum = UserPlanEnum.PegaxyVip;
                            break;
                        }
                    case "admin":
                        {
                            planEnum = UserPlanEnum.ADMIN;
                            break;
                        }

                    default:
                        {
                            throw new Exception("Plan do not exist");
                        }
                }

                TimeSpan duration = TimeSpan.FromDays(Int32.Parse(splits[1]));

                string key = LicenseManager.ManagerInstance.CreateNewKey(user, planEnum, duration);
                SendMessage(user.ChatId, $"License: {key}\nPlan: {planEnum.ToString()}\nExpire in {duration.TotalDays} Days");
            }
            catch (System.Exception e)
            {
                Log(e.ToString());
                SendMessage(user.ChatId, $"Fail to generate new key!\nIf the problem persists contact our support {SUPPORT_TELEGRAM}");
            }

        }

        private void Command_AddToken(string msgData, UserData user)
        {
            try
            {
                var splits = (msgData.Trim()).Split(" ");

                string symbol = splits[0];
                string token_address = splits[1];

                if (Tokens.AddToken(symbol, token_address))
                {
                    SendMessage(user.ChatId, $"Token : ({symbol}) Adicionado com sucesso\n{token_address}");
                }
                else
                {
                    throw new Exception("Falhou na criação do token, provavelmente já existe");
                }
            }
            catch (Exception e)
            {
                Log(e.ToString());
                SendMessage(user.ChatId, $"Fail to add new token!\n{msgData}\nIf the problem persists contact our support {SUPPORT_TELEGRAM}");
            }
        }

        private void Command_RemoveToken(string msgData, UserData user)
        {
            try
            {
                string symbol = msgData.Trim();

                if (Tokens.RemoveToken(symbol))
                {
                    SendMessage(user.ChatId, $"Token : $ {symbol.ToUpper()} Removido com sucesso");
                }
                else
                {
                    throw new Exception("Falhou na remoção do token, provavelmente não já existe");
                }
            }
            catch (Exception e)
            {
                Log(e.ToString());
                SendMessage(user.ChatId, $"Fail to remove token!\n{msgData}\nIf the problem persists contact our support {SUPPORT_TELEGRAM}");
            }
        }

        private void Command_GetAllTokens(string msgData, UserData user)
        {
            try
            {
                string resposta = "Tokens :\n\n";

                foreach (var keyValuePair in Tokens.TokensDict.ToArray())
                {
                    resposta += $"- ${keyValuePair.Key.ToUpper()} : {keyValuePair.Value}\n";
                }

                SendMessage(user.ChatId, resposta);
            }
            catch (System.Exception e)
            {
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error getting all tokens!\ntrying again /tokens");
            }
        }

        private async void Command_AdicionarConta(string msgData, UserData user)
        {
            try
            {
                string[] splits = msgData.Split(" ", 2); // nome endereço privatekey
                string nome = "CarteiraPadrao";

                if (splits.Length > 1)
                {
                    nome = splits[0].Trim();
                }
                string privateKey = splits.Length > 1 ? splits[1].Trim() : splits[0].Trim();

                user.AddAccount(new Conta(nome, privateKey));

                SendMessage(user.ChatId, $"Conta criada");
            }
            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                Console.WriteLine(e);
                SendMessage(user.ChatId, $"Error: Não foi possivel adicionar a conta");
            }
        }

        private async void Command_AdicionarContasLimite(string msgData, UserData user)
        {
            try
            {
                string[] splits = msgData.Split(" ", 2); // id limit
                long chatId = long.Parse(splits[0]);
                int limit_add = Int32.Parse(splits[1]);

                usersManager.Get(chatId)?.AddLimitAccount(limit_add);

                SendMessage(user.ChatId, $"Usuario({chatId}) - Limite de contas aumentado em {limit_add}");
            }
            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                Console.WriteLine(e);
                SendMessage(user.ChatId, $"Error: Não foi possivel adicioanr limite a conta\n{e}");
            }
        }

        private async void Command_UnblockUser(string msgData, UserData user)
        {
            try
            {
                long userID = long.Parse(msgData);

                var getUser = usersManager.Get(userID, true);
                if (getUser != null)
                {
                    getUser.Block = false;
                    SendMessage(user.ChatId, $"User({userID}) desbloqueado");
                }
                else
                {
                    throw new MessageException("Usuario não existe");
                }
            }
            catch (MessageException e)
            {
                Log(e.Message);
                SendMessage(user.ChatId, e.Message);
            }

            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error: Não foi possivel remover a conta\n{e}");
            }
        }
        private async void Command_BlockUser(string msgData, UserData user)
        {
            try
            {
                long userID = long.Parse(msgData);

                var getUser = usersManager.Get(userID, true);
                if (getUser != null)
                {
                    if (getUser.UserPlan >= UserPlanEnum.ADMIN)
                    {
                        throw new MessageException("Não permitido");
                    }

                    getUser.Block = true;
                    SendMessage(user.ChatId, $"User({userID}) bloqueado");
                }
                else
                {
                    throw new MessageException("Usuario não existe");
                }
            }
            catch (MessageException e)
            {
                Log(e.Message);
                SendMessage(user.ChatId, e.Message);
            }

            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error: Não foi possivel remover a conta\n{e}");
            }
        }
        private async void Command_RemoverContaUser(string msgData, UserData user)
        {
            try
            {

                string[] splits = msgData.Split(" ", 2);
                long userID = long.Parse(splits[0]);
                int accountNumber = Int32.Parse(splits[1]);

                var getUser = usersManager.Get(userID, true);
                if (getUser != null)
                {
                    getUser.RemoveAccount(accountNumber);
                }
                else
                {
                    throw new MessageException("Usuario não existe");
                }


                SendMessage(user.ChatId, $"Conta removida");
            }
            catch (MessageException e)
            {
                Log(e.Message);
                SendMessage(user.ChatId, e.Message);
            }

            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error: Não foi possivel remover a conta\n{e}");
            }
        }

        private async void Command_RemoverConta(string msgData, UserData user)
        {
            try
            {
                bool hasParsed = Int32.TryParse(msgData, out int accountNumber);
                if (hasParsed)
                {
                    user.RemoveAccount(accountNumber);
                }
                else
                {
                    throw new Exception($"Numero de conta errado\nFale com {SUPPORT_TELEGRAM}");
                }

                SendMessage(user.ChatId, $"Conta removida");
            }
            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                SendMessage(user.ChatId, $"Error: Não foi possivel remover a conta\n{e}");
            }
        }

        private async void Command_Sudo(string msgData, UserData user)
        {
            try
            {
                string[] splits = msgData.Split(" ", 3); // 20000 /command msg_data
                long chatId = long.Parse(splits[0]);
                string msgCommand = splits[1].ToLower();
                string msgData_2 = splits.Length > 2 ? splits[2].Trim() : "";

                SendMessage(chatId, $"Sudo command ({msgCommand})");
                await HandleCommand(chatId, msgData_2, msgCommand);

                SendMessage(user.ChatId, $"Sudo sended!");
            }
            catch (Exception e)
            {
                Log("ERROR - " + e.ToString());
                Console.WriteLine(e);
                SendMessage(user.ChatId, $"Error trying to sudo command!");
            }
        }

        private async void Command_LogServer(string msgData, UserData user)
        {
            try
            {
                string logPath = Logger.LogPath(this.logName);

                using (FileStream stream = System.IO.File.Open(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        InputOnlineFile inputOnlineFile = new InputOnlineFile(stream, logPath);
                        await botClient.SendDocumentAsync(user.ChatId, inputOnlineFile);
                    }
                }
            }
            catch (System.Exception e)
            {
                Log("ERROR - " + e.ToString());
                Console.WriteLine(e);
                SendMessage(user.ChatId, $"Error trying to get server log!");
            }
        }

        private async void Command_SafeLaunchDeals(string msgData, UserData user)
        {
            try
            {
                string deals = await SafeLauncher.AllUpcomingProjects();

                SendMessage(user.ChatId, deals);
            }
            catch (System.Exception e)
            {
                Log("ERROR - " + e.ToString());
                Console.WriteLine(e);
                SendMessage(user.ChatId, $"Error trying to get Safe Launch Deals!");
            }
        }

        private void Command_NotFound(long userID)
        {
            SendMessage(userID, $"Comando não encontrado!\nSe o problema persisti fale com o suporte {SUPPORT_TELEGRAM}");
        }

        private void Command_Start(long userId, string msgData)
        {
            SendMessage(userId, "Bem vindo!");
        }

        private void Command_Start(string msgData, UserData user)
        {
            SendMessage(user.ChatId, "Bem vindo!");
        }

        private void Command_NotImplemented(string msgData, UserData user)
        {
            SendMessage(user.ChatId, "Command not yet implemented");
        }

        private void Command_NotImplemented(long userId, string msgData)
        {
            SendMessage(userId, "Command not yet implemented");
        }

        public void Log(string message)
        {
            try
            {
                Logger.WriteLog(message, logName);

            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public void Log(object e)
        {
            try
            {
                Logger.WriteLog(e?.ToString(), logName);
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }

    internal class MessageStruct
    {
        public long chatId;
        public string message;
        public bool normal = false;

        public MessageStruct(long chatId, string message, bool normal)
        {
            this.chatId = chatId;
            this.message = message;
            this.normal = normal;
        }
    }
}