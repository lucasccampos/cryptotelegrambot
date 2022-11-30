public class DexData
{
    public string ROUTER { get; set; }
    public string FACTORY { get; set; }

    public DexData() { }

    public DexData(string router, string factory)
    {
        this.ROUTER = router;
        this.FACTORY = factory;
    }

    public string ToJson()
    {
        return $"[{ROUTER},{FACTORY}]";
    }
}