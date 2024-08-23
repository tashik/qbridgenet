namespace QuikBridgeNet.Entities;

public class Bid
{
    public string price { get; set; }
    public string quantity { get; set; }
}

public class Offer
{
    public string price { get; set; } = "0";
    public string quantity { get; set; } = "0";
}

public class OrderBook
{
    public IList<Bid>? bid { get; set; }
    public string bid_count { get; set; } = "0";
    public IList<Offer>? offer { get; set; }
    public string offer_count { get; set; } = "0";

    public OrderBook()
    {
        offer = new List<Offer>();
        bid = new List<Bid>();
    }
}

public class OrderBookData
{
    public string method { get; set; } = "";
    public IList<OrderBook> result { get; set; }
}