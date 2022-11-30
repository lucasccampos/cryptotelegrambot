using System;

public class MessageException : Exception
{

    public MessageException()
    {
    }

    public MessageException(string message)
        : base(message)
    {
    }

    public MessageException(string message, Exception inner)
        : base(message, inner)
    {
    }
}