using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingBot;

public struct LogStruct
{
    public string message;
    public string logName;

    public LogStruct(string message, string logName)
    {
        this.logName = logName;
        this.message = message;
    }
}

public static class Logger
{
    public const string logFolder = "/logs";

    public static string currentFolder = "";

    // public static string lastLog = "";
    private static Queue<LogStruct> logs;

    static Logger()
    {
        logs = new Queue<LogStruct>();
        currentFolder = Program.CurrentFolder;

        if (Directory.Exists(currentFolder + logFolder) == false)
        {
            Directory.CreateDirectory(currentFolder + logFolder);
        }

        Init();
    }

    public static void WriteLog(string message, string logName)
    {
        try
        {
            logs.Enqueue(new LogStruct($"{DateTime.UtcNow} : {message}", logName));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw e;
        }
    }

    public static void WriteLog(object message, string logName)
    {
        try
        {
            logs.Enqueue(new LogStruct($"{DateTime.UtcNow} : {message?.ToString()}", logName));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw e;
        }
    }

    private static void ReallyWriteLog(LogStruct logStruct)
    {
        try
        {
            using (StreamWriter writer = new StreamWriter($"{currentFolder}{logFolder}/{logStruct.logName}.txt", true))
            {
                writer.WriteLine(logStruct.message);
                // lastLog = logStruct.message;
            }

        }
        catch (System.Exception e)
        {
            Console.WriteLine(e);
            throw e;
        }
        // if (logStruct.message == lastLog) return;

    }

    private static void Init()
    {
        Task.Run(Run);
    }

    public async static Task Run()
    {
        try
        {
            while (Program.Running)
            {
                await Task.Delay(100);
                if (logs.Count > 0)
                {
                    ReallyWriteLog(logs.Dequeue());
                }
            }
        }
        catch (System.Exception ex)
        {
            Console.WriteLine("Log Error");
            Console.WriteLine(ex);
            WriteLog($"{ex}", "logger");
        }
    }

    public static string LogPath(string fileName) => $"{currentFolder}{logFolder}/{fileName}.txt";

    public static string ReadLog(string fileName)
    {
        try
        {
            return File.ReadAllText($"{currentFolder}{logFolder}/{fileName}.txt");

        }
        catch (System.Exception e)
        {
            WriteLog(e.ToString(), "logger");
            return null;
        }
    }
}

