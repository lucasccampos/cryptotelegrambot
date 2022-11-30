using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class DateJsonConverter : JsonConverter
{

    public static CultureInfo fmt = new CultureInfo("fr-fr");

    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(string);
    }

    public static DateTimeOffset ConvertStringToDate(string data, CultureInfo culture=null)
    {
        DateTimeOffset.TryParse(input: data?.ToLower()?.Trim(), result: out DateTimeOffset date, formatProvider: culture == null ? fmt : culture, styles: DateTimeStyles.None);
        return date;
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        return ConvertStringToDate(reader.Value.ToString() + " +00:00");

        // If we reach here, we're pretty much going to throw an error so let's let Json.NET throw it's pretty-fied error message.
        return new JsonSerializer().Deserialize(reader, objectType);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        writer.WriteValue(((DateTimeOffset)value).ToString(fmt));
    }
}