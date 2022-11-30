using System;
using System.Threading.Tasks;

public abstract class Bot
{
    public long ID { get; protected set; }
    public long UserID { get; protected set; }
    public BotStateEnum BotState { get; protected set; }
    public BotTypeEnum BotType { get; protected set; }

    public Action<Bot> changedUpdateEvent;

    protected abstract void InvokeChangeEvent();

    public abstract string BotDetail();
    public abstract string ExtractBotData();

    public abstract void ForceStop(bool fromTelegram=false);

    public Bot() { }
}