namespace QuikBridgeNet;

public class MessageIndexer
{
    const int MaxId = 400;
    private int _lastTimeIndex = 0;
    private readonly object _locker = new Object();

    public int GetIndex(int traderId = 0)
    {
        int currenttimeindex = Convert.ToInt32((DateTime.Now - DateTime.Today).TotalMilliseconds / 10.0) - 3570000;
        lock (_locker)
        {
            currenttimeindex = (currenttimeindex <= _lastTimeIndex) ? _lastTimeIndex + 1 : currenttimeindex;
            _lastTimeIndex = currenttimeindex;
        }
        return MaxId * currenttimeindex + traderId;
    }

    public int GetNumberFromMsgId(int msgId)
    {
        return msgId % MaxId;
    }
}