using System;
using Newtonsoft.Json;

public class TokenBSCJsonConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(string);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        string value = reader.Value.ToString().ToLower().Trim();
        if (Tokens.TokensDict.TryGetValue(value, out string address))
        {
            return address;
        }

        if (value.StartsWith("0x"))
        {
            return value;
        }
        else
        {
            throw new Exception("Token not valid");
        }

        // If we reach here, we're pretty much going to throw an error so let's let Json.NET throw it's pretty-fied error message.
        return new JsonSerializer().Deserialize(reader, objectType);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        writer.WriteValue(value);
    }
}