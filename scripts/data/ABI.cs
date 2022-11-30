using System.IO;
using Newtonsoft.Json;

public static class ABI
{
    private static AbiData abiData;
    public const string fileName = "abi.json";

    public static string DEX_ABI => abiData.DEX_ABI;
    public static string FACTORY_ABI => abiData.FACTORY_ABI;
    public static string ERC20_ABI => abiData.ERC20_ABI;
    public static string PAIR_ABI => abiData.PAIR_ABI;
    public static string MULTICALL_ABI => abiData.MULTICALL_ABI;
    public static string PANCAKE_PREDICTION_ABI => abiData.PANCAKE_PREDICTION_ABI;
    public static string CANDLEGENIE_PREDICTION_ABI => abiData.CANDLEGENIE_PREDICTION_ABI;
    public static string ORACLE_ABI => abiData.ORACLE_ABI;
    public static string SUPERLAUNCH_ABI => abiData.SUPERLAUNCH_ABI;
    public static string PEGAXY_ABI => abiData.PEGAXY_ABI;

    static ABI()
    {
        string jsonString = File.ReadAllText("data/" + ABI.fileName);
        abiData = JsonConvert.DeserializeObject<AbiData>(jsonString);
    }
}

class AbiData
{
    public string DEX_ABI { get; set; }
    public string FACTORY_ABI { get; set; }
    public string ERC20_ABI { get; set; }
    public string PAIR_ABI { get; set; }
    public string MULTICALL_ABI { get; set; }
    public string PANCAKE_PREDICTION_ABI { get; set; }
    public string ORACLE_ABI { get; set; }
    public string CANDLEGENIE_PREDICTION_ABI { get; set; }
    public string SUPERLAUNCH_ABI { get; set; }
    public string PEGAXY_ABI { get; set; }
}

