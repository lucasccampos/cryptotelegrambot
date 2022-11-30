using System;
using System.Collections.Generic;

public static class HelperFunctions
{
    public static char GetRandomCharacterFromString(string input)
    {
        // Do some basic null-checking
        if (input == null)
        {
            return char.MinValue; // Or throw an exception or, or, or...
        }

        var random = new Random();
        var inputAsCharArray = input.ToCharArray();
        var index = random.Next(0, input.Length);

        return inputAsCharArray[index];
    }

    public static void AddRange<T, S>(this Dictionary<T, S> source, Dictionary<T, S> collection)
    {
        if (collection == null)
        {
            throw new ArgumentNullException("Collection is null");
        }

        foreach (var item in collection)
        {
            if (!source.ContainsKey(item.Key))
            {
                source.Add(item.Key, item.Value);
            }
            else
            {
                // handle duplicate key issue here
            }
        }
    }

    public static IEnumerable<string> ChunksUpto(string str, int maxChunkSize)
    {
        for (int i = 0; i < str.Length; i += maxChunkSize)
            yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
    }

}
