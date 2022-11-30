using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

public abstract class ObjectManager<TKey, TValue> where TValue : Identification
{
    protected Dictionary<TKey, CacheItem<TValue>> cacheDict;
    protected Dictionary<long, TValue> updateObjsDict;

    public static ObjectManager<TKey, TValue> ManagerInstance { get; protected set; }

    protected readonly string tableName;
    protected MongoCRUD database;

    protected System.Timers.Timer ExpireTimer;
    protected System.Timers.Timer UpdateQueueTimer;

    public TimeSpan CacheTime = TimeSpan.FromHours(16);
    public TimeSpan expireCheckerInterval = TimeSpan.FromSeconds(15);
    public TimeSpan updateCheckerInterval = TimeSpan.FromSeconds(0.2);

    protected abstract string managerLogName {get;}

    protected ObjectManager(string tableName, MongoCRUD database)
    {
        ManagerInstance = this;

        this.tableName = tableName;
        this.cacheDict = new Dictionary<TKey, CacheItem<TValue>>();
        this.updateObjsDict = new Dictionary<long, TValue>();
        this.database = database;

        CreateExpireTimer();
        ExpireTimer?.Start();

        CreateUpdateTimer();
        UpdateQueueTimer?.Start();
    }

    protected virtual void CreateExpireTimer()
    {
        ExpireTimer = new System.Timers.Timer(expireCheckerInterval.TotalMilliseconds); // Set the time (5 mins in this case)
        ExpireTimer.AutoReset = true;
        ExpireTimer.Elapsed += new System.Timers.ElapsedEventHandler((object sender, ElapsedEventArgs e) => RemoveAllExpired());
    }

    protected virtual void CreateUpdateTimer()
    {
        UpdateQueueTimer = new System.Timers.Timer(updateCheckerInterval.TotalMilliseconds); // Set the time (5 mins in this case)
        UpdateQueueTimer.AutoReset = true;
        UpdateQueueTimer.Elapsed += new System.Timers.ElapsedEventHandler((object sender, ElapsedEventArgs e) => UpdateAllQueued());
    }

    public virtual TValue Get(TKey id, bool addToCache = true)
    {
        TValue obj;
        if (cacheDict.TryGetValue(id, out var objCached))
        {
            obj = objCached.Value;
        }
        else
        {
            obj = database.LoadRecordById<TValue, TKey>(tableName, id);
            if (obj == null)
            {
                return default(TValue);
            }

            if (addToCache)
            {
                AddToCache(id, obj);
            }
        }

        return obj;
    }

    public abstract void AddToDatabase(TValue obj, bool addToCache = true);

    public virtual void AddToCache(TKey id, TValue obj)
    {
        cacheDict.TryAdd(id, new CacheItem<TValue>(obj, CacheTime));
    }

    protected virtual void RemoveAllExpired()
    {
        try
        {

            var currentDateTime = DateTimeOffset.UtcNow;

            // ToArray prevents modifying an iterated collection.
            foreach (var keyValuePair in cacheDict.ToArray())
            {
                if(keyValuePair.Value == null) continue;
                if ((currentDateTime - keyValuePair.Value?.CreatedDate) > keyValuePair.Value?.ExpiresAfter)
                {
                    RemoveFromCache(keyValuePair.Key, keyValuePair.Value.Value);
                }
            }
        }
        catch (System.Exception e)
        {
            Console.WriteLine(e);
            Log(e);
            // throw e;
        }
    }

    protected void Log(string message)
    {
        Logger.WriteLog(message, this.managerLogName);
    }

    protected void Log(object e)
    {
        Logger.WriteLog(e?.ToString(), this.managerLogName);
    }

    public virtual void RemoveFromDb(TKey id)
    {
        database.DeleteRecord<TValue, TKey>(tableName, id);
    }

    public virtual void RemoveFromCache(TKey id, TValue obj)
    {
        obj.Dispose();
        cacheDict.Remove(id);
    }

    protected virtual void UpdateAllQueued()
    {
        try
        {

            foreach (var keyValuePair in updateObjsDict.ToArray())
            {
                UpdateObject(keyValuePair.Value);
            }
        }
        catch (System.Exception e)
        {
            Console.WriteLine(e);
            Log(e);
            // throw e;
        }
    }

    protected void AddToUpdateList(TValue obj)
    {
        if (updateObjsDict.ContainsKey(obj.Id))
        {
            return;
        }
        updateObjsDict.Add(obj.Id, obj);
    }

    protected virtual void UpdateObject(TValue obj)
    {
        try
        {

            database.UpsertRecord(tableName, obj.Id, obj);

            if (updateObjsDict.ContainsKey(obj.Id))
            {
                updateObjsDict.Remove(obj.Id);
            }
        }
        catch (System.Exception e)
        {
            Console.WriteLine(e);
            Log(e);
            throw e;
        }
    }

    public void StopAllTimers()
    {
        UpdateQueueTimer.Stop();
        UpdateQueueTimer.Dispose();
        UpdateQueueTimer = null;

        ExpireTimer.Stop();
        ExpireTimer.Dispose();
        ExpireTimer = null;
    }
}