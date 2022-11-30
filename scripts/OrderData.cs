using System;
using MongoDB.Bson.Serialization.Attributes;

public class OrderData : Identification
{
    [BsonId]
    public long BotId { get; set; }
    public long UserID { get; set; }
    public BotTypeEnum BotType { get; set; }
    public BotStateEnum CurrentState { get; set; }
    public string BotDataJson { get; set; }
    public string LogName { get; set; }
    public DateTime AddedTime { get; set; }
    public DateTime LastChangeTime { get; set; }

    [BsonIgnore]
    public long Id => BotId;

    public OrderData() { }

    public void Dispose()
    {
        return;
    }

    public void ChangeBotData(string newData)
    {
        this.BotDataJson = newData;
    }

    public void ChangeCurrentState(BotStateEnum newState)
    {
        this.CurrentState = newState;
    }
}