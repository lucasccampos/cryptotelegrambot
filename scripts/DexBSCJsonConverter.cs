using System;
using System.Linq;
using Newtonsoft.Json;

public class DexBSCJsonConverter : JsonConverter
{
    public static DexData PANCAKESWAP = new DexData(router: "0x10ed43c718714eb63d5aa57b78b54704e256024e", factory: "0xca143ce32fe78f1f7019d7d551a6402fc5350c73");
    public static DexData APESWAP = new DexData(router: "0xcf0febd3f17cef5b47b0cd257acf6025c5bff3b7", factory: "0x0841bd0b734e4f5853f0dd8d7ea041c241fb0da6");
    public static DexData BISWAP = new DexData(router: "0x3a6d8ca21d1cf76f653a67577fa0d27453350dd8", factory: "0x858e3312ed3a876947ea49d572a7c42de08af7ee");

    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(string);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        string value = reader.Value.ToString().ToLower().Trim();
        switch (value)
        {
            case "pancakeswap":
                return PANCAKESWAP;
            case "apeswap":
                return APESWAP;
            case "biswap":
                return BISWAP;
            case "babyswap":
                return new DexData(router: "0x325e343f1de602396e256b67efd1f61c3a6b38bd", factory: "0x86407bea2078ea5f5eb5a52b2caa963bc1f889da");
            case "waultswap":
                return new DexData(router: "0xd48745e39bbed146eec15b79cbf964884f9877c2", factory: "0xb42e3fe71b7e0673335b3331b3e1053bd9822570");
            case "mdex":
                return new DexData(router: "0x7dae51bd3e3376b8c7c4900e9107f12be3af1ba8", factory: "0x3cd1c46068daea5ebb0d3f55f6915b10648062b8");
            case "jetswap":
                return new DexData(router: "0xbe65b8f75b9f20f4c522e0067a3887fada714800", factory: "0x0eb58e5c8aa63314ff5547289185cc4583dfcbd5");
            case "cafeswap":
                return new DexData(router: "0x933daea3a5995fb94b14a7696a5f3ffd7b1e385a", factory: "0x3e708fdbe3ada63fc94f8f61811196f1302137ad");
            case "caramelswap":
                return new DexData(router: "0x1c0a81cc2383e2f28df42d7e4b47a09dd9526157", factory: "0x4ded9d6013a708d1eb743086e7d8cad436ff560d");
            case "sushiswap":
                return new DexData(router: "0x1b02da8cb0d097eb8d57a175b88c7d8b47997506", factory: "0xc35dadb65012ec5796536bd9864ed8773abc74c4");
            case "autoshark":
                return new DexData(router: "0xb0eeb0632bab15f120735e5838908378936bd484", factory: "0xe759dd4b9f99392be64f1050a6a8018f73b53a13");
        }

        try
        {
            var array = value.Trim('[', ']')
             .Split(",")
             .Select(x => x.Trim('"').Trim())
             .ToArray();

            foreach (var item in array)
            {
                if (item.StartsWith("0x") == false) throw new Exception("Dex Json Converter - Invalid Array!");
            }

            return new DexData(router: array[0], factory: array[1]);

        }
        catch (System.Exception)
        {
            throw;
        }

        // If we reach here, we're pretty much going to throw an error so let's let Json.NET throw it's pretty-fied error message.
        return new JsonSerializer().Deserialize(reader, objectType);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        writer.WriteValue(((DexData)value).ToJson());
    }

}