namespace QuikBridgeNetDomain.Entities;

public class AllTrade
{
    public int accruedint {get; set;} // Накопленный купонный доход
    public string benchmark {get; set;} // ?
    public string class_code {get; set;} // Код класса
    public QuikDateTime datetime {get; set;}	// Дата и время
    public string exchange_code {get; set;} // Код биржи в торговой системе
    public string exec_market {get; set;} // Площадка исполнения
    public int flags {get; set;} // Набор битовых флагов
    public int open_interest {get; set;} // Открытый интерес
    public int period {get; set;} // Период торговой сессии. Возможные значения: «0» – Открытие; «1» – Нормальный; «2» – Закрытие
    public double price {get; set;} // Цена
    public int qty {get; set;} // Количество бумаг в последней сделке в лотах
    public int repo2value {get; set;} // Объем выкупа РЕПО
    public int reporate {get; set;} // Ставка РЕПО (%)
    public int repoterm {get; set;} // ?
    public int repovalue {get; set;} // Сумма РЕПО
    public string seccode {get; set;} // Код бумаги заявки
    public string sec_code {get; set;} // Код бумаги заявки
    public string settlecode {get; set;} // Код расчетов
    public string trade_num {get; set;} // Номер сделки в торговой системе
    public string tradenum {get; set;} // Номер сделки в торговой системе
    public double value {get; set;} // Объем в денежных средствах
    public int yield {get; set;} // Доходность
   
    
    
    
    
    
    
    
    
    
    
    
}