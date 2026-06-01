using QuikBridgeNet.Entities.MessageMeta;
using QuikBridgeNetDomain.Entities;

namespace QuikBridgeNet;

internal static class QuikMessageMapper
{
    public static QMessage CreateQMessage(int id, string methodName, MetaData data)
    {
        var qMessage = new QMessage
        {
            Id = id,
            Method = methodName,
            MessageType = data.MessageType
        };

        switch (data)
        {
            case Subscription subscription:
                if (subscription.Ticker != "")
                {
                    qMessage.Ticker = subscription.Ticker;
                }

                if (subscription.InstrumentClass != "")
                {
                    qMessage.ClassCode = subscription.InstrumentClass;
                }

                if (subscription is DataSource dsInit)
                {
                    qMessage.Interval = dsInit.Interval;
                }

                if (subscription is ParamSubscription paramSubscription)
                {
                    qMessage.ParamName = paramSubscription.ParamName;
                }

                break;
            case DatasourceCallback datasourceCallback:
                qMessage.DataSource = datasourceCallback.DataSource;
                break;
            case AccountPositionRequest accountPositionRequest:
                qMessage.FirmId = accountPositionRequest.FirmId;
                qMessage.ClientCode = accountPositionRequest.ClientCode;
                qMessage.Ticker = accountPositionRequest.SecCode;
                qMessage.Account = accountPositionRequest.Account;
                qMessage.LimitKind = accountPositionRequest.LimitKind;
                break;
            case MoneyPositionRequest moneyPositionRequest:
                qMessage.FirmId = moneyPositionRequest.FirmId;
                qMessage.ClientCode = moneyPositionRequest.ClientCode;
                qMessage.Tag = moneyPositionRequest.Tag;
                qMessage.CurrencyCode = moneyPositionRequest.CurrencyCode;
                qMessage.LimitKind = moneyPositionRequest.LimitKind;
                break;
            case FuturesHoldingRequest futuresHoldingRequest:
                qMessage.FirmId = futuresHoldingRequest.FirmId;
                qMessage.Account = futuresHoldingRequest.Account;
                qMessage.Ticker = futuresHoldingRequest.SecCode;
                qMessage.PositionType = futuresHoldingRequest.PositionType;
                break;
            case FuturesLimitRequest futuresLimitRequest:
                qMessage.FirmId = futuresLimitRequest.FirmId;
                qMessage.Account = futuresLimitRequest.Account;
                qMessage.LimitKind = futuresLimitRequest.LimitType;
                qMessage.CurrencyCode = futuresLimitRequest.CurrencyCode;
                break;
        }

        return qMessage;
    }
}