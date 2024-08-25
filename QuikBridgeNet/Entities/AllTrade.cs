namespace QuikBridgeNet.Entities;

public class AllTrade
{
    public int trade_num {get; set;} // Номер сделки в торговой системе
    public int price {get; set;} // Цена
    public int qty {get; set;} // Количество бумаг в последней сделке в лотах
    public int value {get; set;} // Объем в денежных средствах
    public int accruedint {get; set;} // Накопленный купонный доход
    public int yield {get; set;} // Доходность
    public int flags {get; set;} // Набор битовых флагов
    public string settlecode {get; set;} // Код расчетов
    public int reporate {get; set;} // Ставка РЕПО (%)
    public int repovalue {get; set;} // Сумма РЕПО
    public int repo2value {get; set;} // Объем выкупа РЕПО
    public int sec_code {get; set;} // Код бумаги заявки
    public int class_code {get; set;} // Код класса
    public QuikDateTime datetime {get; set;}	// Дата и время
    public int period {get; set;} // Период торговой сессии. Возможные значения: «0» – Открытие; «1» – Нормальный; «2» – Закрытие
    public int open_interest {get; set;} // Открытый интерес
    public string exchange_code {get; set;} // Код биржи в торговой системе
    public string exec_market {get; set;} // Площадка исполнения
}