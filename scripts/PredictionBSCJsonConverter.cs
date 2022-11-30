using System;
using Newtonsoft.Json;

public class PredictionBSCJsonConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(string);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        string value = reader.Value.ToString().ToLower().Trim();
        switch (value)
        {
            case "pancake":
                return "0x18B2A687610328590Bc8F2e5fEdDe3b582A49cdA";
            case "candlegenie_bnb":
                return "0x4d85b145344f15B4419B8afa1CbB2A9d00B17935";
            case "candlegenie_btc":
                return "0x995294CdBfBf7784060BD3Bec05CE38a5F94A0C5";
            case "candlegenie_eth":
                return "0x65669Dcd4813341ACACF51b261F560c92d40A632";
        }

        return value;


        // If we reach here, we're pretty much going to throw an error so let's let Json.NET throw it's pretty-fied error message.
        return new JsonSerializer().Deserialize(reader, objectType);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        writer.WriteValue(value);
    }
}