using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using MongoDB.Bson.Serialization.Attributes;

public class LicenseManager
{
    public static LicenseManager ManagerInstance { get; private set; }

    private readonly string tableName;
    private MongoCRUD database;

    public LicenseManager(string tableName, MongoCRUD database)
    {
        ManagerInstance = this;

        this.tableName = tableName;
        this.database = database;
    }

    public LicenseKeyData Get(string key)
    {
        LicenseKeyData obj = database.LoadRecordById<LicenseKeyData, string>(tableName, key);
        if (obj == null)
        {
            return default(LicenseKeyData);
        }

        return obj;
    }

    private void AddToDatabase(LicenseKeyData obj)
    {
        database.InsertRecord(tableName, obj);
    }

    private string CreateRandomKey()
    {
        string key = Guid.NewGuid().ToString("n").Substring(0, 8);
        var keyData = Get(key);
        if (keyData != null && keyData != default(LicenseKeyData))
        {
            return CreateRandomKey();
        }
        return key;
    }

    public string CreateNewKey(UserData user, UserPlanEnum planEnum, TimeSpan licenseDuration)
    {
        if (!(user.UserPlan > planEnum)) throw new MessageException("User is not allowed to create a license superior to his own plan.");

        var license = new LicenseKeyData()
        {
            Value = CreateRandomKey(),
            UserPlanEnum = planEnum,
            LicenseDuration = licenseDuration,
            CreatorID = user.Id
        };
        AddToDatabase(license);

        return license.Value;
    }

    public LicenseKeyData UseLicenseKey(long userID, string licenseKey)
    {
        var licenseData = Get(licenseKey);
        if (licenseData == null || licenseData == default(LicenseKeyData)) return null;

        if (licenseData.OwnerID != default(long)) return null;

        licenseData.OwnerID = userID;
        licenseData.DateExpire = DateTime.UtcNow + licenseData.LicenseDuration;

        database.UpsertRecord(tableName, licenseData.Value, licenseData);

        return licenseData;
    }

    //TODO: Search for all expired users

}

public class LicenseKeyData
{
    [BsonId]
    public string Value { get; set; }
    public long CreatorID { get; set; }
    public long OwnerID { get; set; }
    public UserPlanEnum UserPlanEnum { get; set; }
    public TimeSpan LicenseDuration { get; set; }
    public DateTime DateExpire { get; set; }
}