namespace QuikBridgeNetDomain.Entities;

public class Quotation
{
    public string price { get; set; }
    public string quantity { get; set; }
}
public class Bid : Quotation
{
}

public class Offer : Quotation
{
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