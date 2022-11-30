using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using Nethereum.Web3.Accounts;

public class UserData : Identification
{
    [BsonId]
    public long ChatId { get; set; } //Telegram Chat ID
    public UserPlanEnum UserPlan { get; set; }
    public string LicenseKey { get; set; }
    public DateTime LicenseKeyExpireDate { get; set; }

    public string Username { get; set; }
    public bool Block { get; set; }
    public string MyReferralCode { get; set; }
    public string ReferralCodeUsed { get; set; }
    public string PrivateKey { get; set; }
    public int AccountLimit { get; set; }
    public int ActiveAccount { get; set; }
    public List<Conta> Accounts { get; set; }
    public List<long> BotList { get; set; }

    public string AdicionalInfoJson { get; set; }

    public event Action<UserData> changedUpdate;
    public DateTime lastChange { get; set; }

    [BsonIgnore]
    public long Id => ChatId;

    public UserData() { }

    public UserData(long chatID)
    {
        this.ChatId = chatID;
        lastChange = DateTime.UtcNow;
    }

    public UserData(long chatID, string username)
    {
        this.ChatId = chatID;
        this.Username = username;
        lastChange = DateTime.UtcNow;
        this.Accounts = new List<Conta>();
    }

    private void InvokeChangeEvent()
    {
        changedUpdate?.Invoke(this);
        lastChange = DateTime.UtcNow;
    }

    public void Dispose()
    {
        changedUpdate = null;
    }

    public void SetPrivateKey(string privateKey)
    {
        this.PrivateKey = privateKey;
        InvokeChangeEvent();
    }

    public void SetLicenseKey(LicenseKeyData license)
    {
        //TODO : passar junto o tempo de expiração da key
        this.LicenseKey = license.Value;
        this.LicenseKeyExpireDate = license.DateExpire;
        if (license.UserPlanEnum >= license.UserPlanEnum)
        {
            this.UserPlan = license.UserPlanEnum;
        }
        InvokeChangeEvent();
    }

    public void AddBot(long botID)
    {
        if (BotList == null)
            BotList = new List<long>();
        BotList.Add(botID);
        InvokeChangeEvent();
    }

    public void AddAccount(Conta conta)
    {
        if (this.Accounts == null)
        {
            this.Accounts = new List<Conta>();
        }

        if (this.Accounts.Count + 1 > this.AccountLimit)
        {
            throw new Exception($"Você só pode ter {this.AccountLimit}\nFale com o mod para aumentar esse limite!");
        }
        else
        {
            if (String.IsNullOrEmpty(conta.PrivateKey))
            {
                throw new Exception("Você precisa colocar uma privatekey valida!");
            }

            try
            {
                var account = new Account(conta.PrivateKey);
                if (String.IsNullOrEmpty(conta.Address))
                {
                    conta.Address = account.Address;
                }
            }
            catch (System.Exception)
            {
                throw new Exception("Você precisa colocar uma privatekey valida!");
            }
            conta.CreateDate = DateTimeOffset.UtcNow;
            this.Accounts.Add(conta);
            InvokeChangeEvent();
        }

    }

    public void AddLimitAccount(int plusLimit)
    {
        this.AccountLimit += plusLimit;
        InvokeChangeEvent();
    }

    public void RemoveAccount(int accountNumber)
    {
        if (this.Accounts == null)
        {
            this.Accounts = new List<Conta>();
        }

        if (accountNumber > this.Accounts.Count - 1)
        {
            throw new Exception("Essa conta não existe");
        }

        if (this.ActiveAccount == accountNumber)
        {
            this.ActiveAccount = 0;
        }
        else if (this.ActiveAccount > accountNumber)
        {
            this.ActiveAccount -= 1;
        }


        this.Accounts.RemoveAt(accountNumber);


        InvokeChangeEvent();
    }

    public void SetActiveAccount(int accountNumber)
    {
        this.ActiveAccount = accountNumber;
        InvokeChangeEvent();
    }

    public string GetActivePrivateKey()
    {
        if (this.Accounts == null)
        {
            this.Accounts = new List<Conta>();
        }

        if (this.Accounts.Count == 0)
        {
            if (string.IsNullOrEmpty(PrivateKey))
            {
                return null;
                // throw new Exception("Private key null");
            }
            return PrivateKey;
        }
        else
        {
            if (this.ActiveAccount > this.Accounts.Count - 1)
            {
                this.ActiveAccount = this.Accounts.Count - 1;
            }
        }
        InvokeChangeEvent();
        return this.Accounts[this.ActiveAccount].PrivateKey;
    }

    public Conta GetActiveAccount()
    {
        if (this.Accounts == null)
        {
            this.Accounts = new List<Conta>();
            InvokeChangeEvent();
        }

        if (this.Accounts.Count > 0)
        {
            if (this.ActiveAccount > this.Accounts.Count - 1)
            {
                this.ActiveAccount = this.Accounts.Count - 1;
                InvokeChangeEvent();
            }
            return this.Accounts[this.ActiveAccount];
        }
        return null;
    }

    public string ListAccounts()
    {
        if (this.Accounts == null)
        {
            this.Accounts = new List<Conta>();
            InvokeChangeEvent();
        }

        string message = "";
        if (this.Accounts.Count > 0)
        {
            for (int i = 0; i < this.Accounts.Count; i++)
            {
                message += $"\nNumero da Conta: {i} - {this.Accounts[i].Name}\n" +
                $"Endereço: {this.Accounts[i].Address}\n";
            }
        }
        else
        {
            message = "Você não tem contas";
        }

        return message;
    }

    //TODO: Criar uma propriedade de comparação aqui ==
}