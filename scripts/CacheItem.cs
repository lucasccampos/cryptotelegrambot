using System;

public class CacheItem<T>
{
    public T Value { get; private set; }
    public TimeSpan ExpiresAfter { get; private set; }
    public DateTimeOffset CreatedDate { get; }

    public CacheItem(T value, TimeSpan expiresAfter)
    {
        Value = value;
        CreatedDate = DateTimeOffset.UtcNow;
        this.ExpiresAfter = expiresAfter;
    }

    public CacheItem(T value, TimeSpan expiresAfter, DateTimeOffset createdDate)
    {
        Value = value;
        CreatedDate = createdDate;
        this.ExpiresAfter = expiresAfter;
    }

    bool isExpired(DateTimeOffset now)
    {
        return (now - CreatedDate) >= ExpiresAfter;
    }

    bool isExpired() => isExpired(DateTimeOffset.UtcNow);
}