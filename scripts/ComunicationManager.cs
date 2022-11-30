using System.Threading.Tasks;
using TradingBot.Telegram;

public static class ComunicationManager
{
    public static void SendMessage(string message, long userID)
    {
        TelegramBotServer.Instance?.SendMessage(chatId: userID, message: message);
    }

    public async static Task SendMessageAsync(string message, long userID)
    {
        TelegramBotServer.Instance?.SendMessage(chatId: userID, message: message);
    }
}