using System;
using System.Threading.Tasks;

public class UsersManager : ObjectManager<long, UserData>
{
    public static UsersManager Instance;

    protected override string managerLogName { get => "usersManager";}

    public UsersManager(string tableName, MongoCRUD database) : base(tableName, database)
    {
        if (Instance != null)
        {
            Logger.WriteLog("UsersManager já existe", "problema");
            return;
        }
        Instance = this;

        CacheTime = TimeSpan.FromHours(3);
        expireCheckerInterval = TimeSpan.FromHours(3.1);
    }

    public void NewUser(long chatID, string username, LicenseKeyData keyData)
    {
        UserData newUser = new UserData(chatID, username){
            AccountLimit=1
        };
        newUser.SetLicenseKey(keyData);

        AddToDatabase(newUser, true);
    }

    public override void AddToDatabase(UserData obj, bool addToCache)
    {
        database.UpsertRecord(tableName, obj.ChatId, obj);
        if (addToCache)
            AddToCache(obj.ChatId, obj);
    }

    public override void AddToCache(long id, UserData obj)
    {
        base.AddToCache(id, obj);
        obj.changedUpdate += AddToUpdateList;
    }
}