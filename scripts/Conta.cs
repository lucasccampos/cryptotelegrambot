using System;

public class Conta
{

    public Conta() { }

    public Conta(string nome, string privateKey)
    {
        this.Name = nome;
        this.PrivateKey = privateKey;
    }

    public Conta(string nome, string address, string privateKey)
    {
        this.Name = nome;
        this.Address = address;
        this.PrivateKey = privateKey;
    }

    public Conta(string nome, string address, string privateKey, DateTimeOffset createDate)
    {
        this.Name = nome;
        this.Address = address;
        this.PrivateKey = privateKey;
        this.CreateDate = createDate;
    }

    public string Name;
    public string Address;
    public string PrivateKey;
    public DateTimeOffset CreateDate;
}