using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingBot;

public static class TraderBotManager
{
    private static List<TraderBot> botList;
    private static int len_botList;

    private static bool looping = false;

    static TraderBotManager()
    {
        botList = new List<TraderBot>();
        len_botList = 0;
    }

    public static void Add(TraderBot bot)
    {
        botList.Add(bot);
        len_botList += 1;
        bot.ChangeState(BotStateEnum.Running, 2f);

        if (looping == false && Program.Running)
        {
            StartLoop();
        }
    }

    public static void Remove(TraderBot bot)
    {
        TokenTrackerManager.RemoveToken(bot.tokenTrackerSwap);
        TokenTrackerManager.RemoveToken(bot.tokenTrackerUSD);
        botList.Remove(bot);
        len_botList -= 1;
    }

    public static async void StartLoop()
    {
        looping = true;
        Task.Run(Loop);
    }


    private static async void Loop()
    {
        while (looping)
        {
            for (int i = 0; i < len_botList; i++)
            {
                try
                {
                    botList[i]?.Loop();
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            await Task.Delay(2);
        }
    }

}