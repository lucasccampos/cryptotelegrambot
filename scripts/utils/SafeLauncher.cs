using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Numerics;
using Nethereum.Web3;

public static class SafeLauncher
{
    public async static Task<string> AllUpcomingProjects()
    {
        HttpClient httpClient = new HttpClient();

        string response = "";
        response += "Private Deals\n";

        string jsonData = await httpClient.GetStringAsync("https://api.safelaunch.io/deal/deals");
        List<Project_Launch> projects = JObject.Parse(jsonData)["upcoming"].Children().ToList().Select(x => x.ToObject<Project_Launch>()).ToList();

        projects.Sort((x, y) => (String.IsNullOrEmpty(y.desiredStartTime) ? DateTimeOffset.MinValue : y.DesiredStartTime().Value).CompareTo(!String.IsNullOrEmpty(x.desiredStartTime) ? x.DesiredStartTime().Value : DateTimeOffset.MinValue));

        if (projects.Count > 0)
        {
            foreach (var item in projects)
            {
                response += item.ToString();
            }
        }
        else
        {
            response += "Zero Upcoming projects";
        }

        response += "\nLaunchPad Deals\n";

        jsonData = await httpClient.GetStringAsync("https://api.safelaunch.io/deal/idos");
        projects = JObject.Parse(jsonData)["upcoming"].Children().ToList().Select(x => x.ToObject<Project_Launch>()).ToList();

        if (projects.Count > 0)
        {
            foreach (var item in projects)
            {
                response += item.ToString();
            }
        }
        else
        {
            response += "\nZero Upcoming projects";
        }

        return response;
    }

    public class Project_Launch
    {
        public string name { get; set; }
        public string about { get; set; }
        public string contractAddress { get; set; }
        public string weightedAveragePrice { get; set; }
        public string dealSize { get; set; }
        public bool started { get; set; }
        public bool isIdo { get; set; }
        public string desiredStartTime { get; set; }

        public DateTimeOffset? DesiredStartTime()
        {
            if (String.IsNullOrEmpty(desiredStartTime))
                return null;
            else
                return DateJsonConverter.ConvertStringToDate(desiredStartTime, new System.Globalization.CultureInfo("en-US"));
        }

        public float DealSize()
        {
            try
            {
                BigInteger.TryParse(dealSize, System.Globalization.NumberStyles.Any, null, out var result);
                return (float)Web3.Convert.FromWei(result);
            }
            catch (System.Exception)
            {
                return 0f;
            }
        }

        public override string ToString()
        {
            DateTimeOffset? date = this.DesiredStartTime();

            return ("\n" +
            $"*Name:* {this.name}\n" +
            $"Contract: {this.contractAddress}\n" +
            $"Start Date: {(date.HasValue ? date.Value.ToUniversalTime().ToString(DateJsonConverter.fmt) : "Null")}\n" +
            $"Price: {this.weightedAveragePrice}\n" +
            $"Size: {this.DealSize().ToString("N0")} BUSD\n"
            );
        }
    }
}


