using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TradingBot;
using TradingBot.Data;

public class OrdersManager : ObjectManager<long, OrderData>
{
    public static OrdersManager Instance { get; private set; }

    public static ConfigData configData;

    private Dictionary<long, Bot> updateOrdersDict;

    public int BotCount { get; private set; }
    public Dictionary<long, Bot> DictBots { get; private set; }

    protected override string managerLogName { get => "ordersManager"; }

    public OrdersManager(string tableName, MongoCRUD database) : base(tableName, database)
    {
        if (Instance != null)
        {
            Log("OrdersManager já existe");
            throw new Exception("OrdersManager já existe");
        }

        CacheTime = TimeSpan.FromSeconds(600);
        expireCheckerInterval = TimeSpan.FromSeconds(660);

        Instance = this;
        DictBots = new Dictionary<long, Bot>();
        updateOrdersDict = new Dictionary<long, Bot>();

        var allOrders = database.LoadRecords<OrderData>(tableName);
        if (allOrders != null && allOrders.Count > 0)
        {
            BotCount = (int)allOrders.Last().Id + 1; //Load bot Count
        }

        string configString = File.ReadAllText("data/config.json");
        configData = JsonConvert.DeserializeObject<ConfigData>(configString, new BooleanJsonConverter());

        Console.WriteLine($"Config >:\n-HTTP : {configData.RPC_HTTP}\n-WS : {configData.RPC_WS}\n-POLYGON-HTTP : {configData.POLYGON_RPC_HTTP}");
        Log($"Config >:\n-HTTP : {configData.RPC_HTTP}\n-WS : {configData.RPC_WS}\n-POLYGON-HTTP : {configData.POLYGON_RPC_HTTP}");
    }

    private void AddBotToUpdateList(Bot bot)
    {
        if (updateOrdersDict.ContainsKey(bot.ID)) return;

        updateOrdersDict.Add(bot.ID, bot);
    }

    protected override void UpdateAllQueued()
    {
        try
        {

            foreach (var keyPair in updateOrdersDict.ToArray())
            {
                UpdateObject(keyPair.Value);
                updateOrdersDict.Remove(keyPair.Key);
            }
        }
        catch (System.Exception e)
        {
            Console.WriteLine(e);
            Log(e);
            throw e;
        }
    }

    protected override void UpdateObject(OrderData key) { }

    protected void UpdateObject(Bot bot)
    {
        try
        {
            if (bot == default(Bot) || bot == null)
            {
                Console.WriteLine("bot null");
                return;
            }

            OrderData order = Get(bot.ID);

            if (order != null)
            {
                order.ChangeBotData(bot.ExtractBotData());
                order.ChangeCurrentState(bot.BotState);
                order.LastChangeTime = System.DateTime.UtcNow;

                database.UpsertRecord(tableName, order.Id, order);
            }
            else
            {
                Console.WriteLine("Ordem = null");
            }
        }
        catch (System.Exception e)
        {
            Console.WriteLine(e);
            Log(e);
            throw e;
        }
    }

    public Bot GetBot(long id)
    {
        try
        {
            if (DictBots.TryGetValue(id, out var objCached))
            {
                return objCached;
            }
            return null;

        }
        catch (System.Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }

    public long CreateTradeBot(TradeData data, UserData user)
    {
        try
        {
            if (data == null || data == default(TradeData) || data.TOKEN == null || (data.BUY && data.SELL))
            {
                throw new Exception("Trade data invalid");
            }

            long bot_id = ++BotCount;
            string logNameBot = $"Trader_{bot_id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            TraderBot bot = new TraderBot(configData: configData,
                                            privateData: new PrivateData { PRIVATE_KEY = user.GetActivePrivateKey() },
                                            botData: data,
                                            userID: user.ChatId,
                                            botID: bot_id,
                                            logName: logNameBot);
            TraderBotManager.Add(bot);
            AddBotToCache(bot);

            bot.changedUpdateEvent += AddBotToUpdateList;

            OrderData orderData = new OrderData()
            {
                BotId = bot.ID,
                UserID = user.ChatId,
                BotType = BotTypeEnum.Trade,
                AddedTime = System.DateTime.UtcNow,
                CurrentState = bot.BotState,
                LogName = logNameBot,
                LastChangeTime = System.DateTime.UtcNow,
                BotDataJson = JsonConvert.SerializeObject(data),
            };

            AddToDatabase(orderData, true);
            user.AddBot(bot.ID);

            AddBotToUpdateList(bot);

            return orderData.BotId;
        }
        catch (Exception e)
        {
            Log(e);
            throw e;
        }
    }

    public long CreateSniperBot(SniperData data, UserData user)
    {
        long bot_id = ++BotCount + 200000;
        string logNameBot = $"Sniper_{bot_id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        SniperBot bot = new SniperBot(configData: configData,
                                        privateData: new PrivateData { PRIVATE_KEY = user.GetActivePrivateKey() },
                                        botData: data,
                                        userID: user.ChatId,
                                        botID: bot_id,
                                        logName: logNameBot);
        try
        {

            Task.Run(bot.main);
            AddBotToCache(bot);

            bot.changedUpdateEvent += AddBotToUpdateList;

            OrderData orderData = new OrderData()
            {
                BotId = bot.ID,
                UserID = user.ChatId,
                BotType = BotTypeEnum.Trade,
                AddedTime = System.DateTime.UtcNow,
                CurrentState = bot.BotState,
                LogName = logNameBot,
                LastChangeTime = System.DateTime.UtcNow,
                BotDataJson = JsonConvert.SerializeObject(data),
            };

            AddToDatabase(orderData, true);
            user.AddBot(bot.ID);

            AddBotToUpdateList(bot);

            return orderData.BotId;
        }
        catch (System.Exception e)
        {
            bot?.ForceStop();
            Log(e);
            throw;
        }
    }

    public void AddBotToCache(Bot bot)
    {
        DictBots.Add(bot.ID, bot);
    }

    public override void AddToDatabase(OrderData obj, bool addToCache = true)
    {
        database.UpsertRecord(tableName, obj.BotId, obj);
        if (addToCache)
            AddToCache(obj.BotId, obj);
    }

    private List<OrderData> GetAllActiveOrdersFromUserId(long userID)
    {
        var lista = new List<OrderData>();
        foreach (var order in GetAllOrdersFromUserId(userID))
        {
            if (DictBots.ContainsKey(order.BotId))
            {
                lista.Add(order);
            }
        }

        return lista;
    }

    private List<OrderData> GetAllOrdersFromUserId(long userID)
    {
        return database.LoadRecordsWithKey<OrderData, long>(tableName, "UserID", userID);
    }

    public List<long> GetBotList(long userID)
    {
        return GetAllActiveOrdersFromUserId(userID).Select(i => i.BotId).ToList();
    }

    public string OrderLogName(long userID, int botID)
    {
        var order = Get(botID);
        if (order == null || order == default(OrderData))
            throw new Exception($"Bot({botID}) não encontrado");

        if (order.UserID == userID)
        {
            return order.LogName;
        }
        else
            throw new Exception($"Bot({order.Id}) -> You are not the owner of this bot!");
    }

    public string BotDetail(long userID, int botID)
    {
        var order = Get(botID);
        if (order == null || order == default(OrderData))
            return $"Bot({botID}) não encontrado";

        if (order.UserID == userID)
        {
            Bot bot = GetBot(botID);
            return $"Bot({order.Id})\n{(bot != null ? bot?.BotDetail() : $"Current State: {order.CurrentState}")}";
        }
        else
            return $"Bot({order.Id}) -> You are not the owner of this bot!";
    }

    public bool RemoveBot(long userID, int botID, bool fromBot = false, bool force=false)
    {
        try
        {

            if (DictBots.TryGetValue(botID, out var bot))
            {
                if (bot.UserID == userID || force)
                {
                    if (fromBot == false)
                    {
                        bot?.ForceStop(true);
                    }

                    if (bot.BotType == BotTypeEnum.Trade)
                    {
                        TraderBotManager.Remove((TraderBot)bot);
                    }
                    else if (bot.BotType == BotTypeEnum.Pegaxy)
                    {
                        PegaxyBotManager.Remove((PegaxyBot)bot);
                    }

                    DictBots.Remove(botID);
                    if (bot?.changedUpdateEvent != null)
                        bot.changedUpdateEvent -= AddBotToUpdateList;

                    bot = null;
                    return true;
                }
            }
            return false;
        }
        catch (System.Exception e)
        {
            Log($"Error\n{e.ToString()}");
            throw e;
        }
    }

    public async Task<long> CreatePredictionBot(PredictionData data, UserData user)
    {
        long bot_id = ++BotCount + 400000;
        string logNameBot = $"Prediction_{bot_id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        PredictionBot bot = new PredictionBot(configData: configData,
                                                privateData: new PrivateData { PRIVATE_KEY = user.GetActivePrivateKey() },
                                                botData: data,
                                                userID: user.ChatId,
                                                botID: bot_id,
                                                logName: logNameBot);
        try
        {
            bool created = await bot.StartBot();
            if (created == false)
            {
                throw new Exception("Erro ao criar prediction bot");
            }
            AddBotToCache(bot);

            bot.changedUpdateEvent += AddBotToUpdateList;

            OrderData orderData = new OrderData()
            {
                BotId = bot.ID,
                UserID = user.ChatId,
                BotType = BotTypeEnum.Trade,
                LogName = logNameBot,
                AddedTime = System.DateTime.UtcNow,
                CurrentState = bot.BotState,
                LastChangeTime = System.DateTime.UtcNow,
                BotDataJson = JsonConvert.SerializeObject(data),
            };

            AddToDatabase(orderData, true);
            user.AddBot(bot.ID);

            AddBotToUpdateList(bot);

            return orderData.BotId;
        }
        catch (System.Exception e)
        {
            bot?.ForceStop();
            Log(e);
            throw;
        }
    }

    public async Task<long> CreateSafeLaunchBot(SafeLaunchData data, UserData user)
    {
        long bot_id = ++BotCount + 500000;
        string logNameBot = $"SafeLaunch_{bot_id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        SafeLauncherBot bot = new SafeLauncherBot(configData: configData,
                                                privateData: new PrivateData { PRIVATE_KEY = user.GetActivePrivateKey() },
                                                botData: data,
                                                userID: user.ChatId,
                                                botID: bot_id,
                                                logName: logNameBot);
        try
        {
            bool created = await bot.StartBot();
            if (created == false)
            {
                throw new Exception("Erro ao criar prediction bot");
            }
            AddBotToCache(bot);

            bot.changedUpdateEvent += AddBotToUpdateList;

            OrderData orderData = new OrderData()
            {
                BotId = bot.ID,
                UserID = user.ChatId,
                BotType = BotTypeEnum.Trade,
                AddedTime = System.DateTime.UtcNow,
                LogName = logNameBot,
                CurrentState = bot.BotState,
                LastChangeTime = System.DateTime.UtcNow,
                BotDataJson = JsonConvert.SerializeObject(data),
            };

            AddToDatabase(orderData, true);
            user.AddBot(bot.ID);

            AddBotToUpdateList(bot);

            return orderData.BotId;
        }
        catch (System.Exception e)
        {
            bot?.ForceStop();
            Log(e);
            throw;
        }
    }

    public long CreatePegaxyBot(PegaxyData data, UserData user)
    {
        try
        {
            if (data == null || data == default(PegaxyData))
            {
                throw new Exception("Trade data invalid");
            }

            long bot_id = ++BotCount;
            string logNameBot = $"Pegaxy_{bot_id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            var conta = user.GetActiveAccount();

            if(conta == null){
                throw new MessageException("Você não tem nenhuma conta adicionada");
            }

            if(PegaxyBotManager.DictOrderPerAddress.TryGetValue(conta.Address, out int len_ordens)){
                if(len_ordens >= PegaxyBotManager.MaxOrdersPerAddress){
                    throw new MessageException($"Você não pode adicionar mais ordens, você já tem {len_ordens} ordens ativas nessa conta!");
                }
            }

            data.ContaData = new ContaData(conta.Name, conta.Address);

            PegaxyBot bot = new PegaxyBot(configData: configData,
                                            privateData: conta,
                                            botData: data,
                                            userID: user.ChatId,
                                            botID: bot_id,
                                            logName: logNameBot);
            PegaxyBotManager.Add(bot);
            AddBotToCache(bot);

            bot.changedUpdateEvent += AddBotToUpdateList;

            OrderData orderData = new OrderData()
            {
                BotId = bot.ID,
                UserID = user.ChatId,
                BotType = BotTypeEnum.Trade,
                AddedTime = System.DateTime.UtcNow,
                CurrentState = bot.BotState,
                LogName = logNameBot,
                LastChangeTime = System.DateTime.UtcNow,
                BotDataJson = JsonConvert.SerializeObject(data),
            };

            AddToDatabase(orderData, true);
            user.AddBot(bot.ID);

            AddBotToUpdateList(bot);

            return orderData.BotId;
        }
        catch (MessageException e)
        {
            Log(e.Message);
            throw e;
        }
        catch (Exception e)
        {
            Log(e);
            throw e;
        }
    }
}