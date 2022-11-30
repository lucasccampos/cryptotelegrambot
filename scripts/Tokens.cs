using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TradingBot;

public static class Tokens
{
    public const string WBNB = "0xbb4cdb9cbd36b01bd1cbaebf2de08d9173bc095c";
    public const string BUSD = "0xe9e7cea3dedca5984780bafc599bd69add087d56";
    public const string USDT = "0x55d398326f99059ff775485246999027b3197955";
    public const string USDC = "0x8ac76a51cc950d9822d68b83fe1ad97b32cd580d";

    public const string fileName = "tokens.json";
    public const string folderName = "data";
    private static string currentFolder = Program.CurrentFolder;

    static Tokens()
    {
        string jsonString = File.ReadAllText($"{folderName}/{fileName}");
        TokensDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);
    }

    public static Dictionary<string, string> TokensDict = new Dictionary<string, string>();

    public static bool AddToken(string symbol, string address)
    {
        if (TokensDict.ContainsKey(symbol)) return false;
        if (address.StartsWith("0x") == false) return false;

        TokensDict.Add(symbol.ToLower().Trim(), address.Trim().ToLower());
        SaveJson();

        return true;
    }

    public static bool RemoveToken(string symbol)
    {
        if (TokensDict.ContainsKey(symbol) == false) return false;

        TokensDict.Remove(symbol.ToLower().Trim());
        SaveJson();

        return true;
    }

    public static string TryGetSymbol(string address)
    {
        if (TokensDict.ContainsValue(address.ToLower().Trim()))
        {
            return TokensDict.FirstOrDefault(x => x.Value == address).Key;
        }

        return address;
    }

    private static void SaveJson()
    {
        string json = JsonConvert.SerializeObject(TokensDict, Formatting.Indented);
        using (StreamWriter writer = new StreamWriter($"{folderName}/{fileName}", false))
        {
            writer.Write(json);
        }
    }


}