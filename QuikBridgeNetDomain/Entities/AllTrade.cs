namespace QuikBridgeNetDomain.Entities;

public class AllTrade
{
    public int accruedint {get; set;} // Накопленный купонный доход
    public string benchmark {get; set;} = string.Empty; // ?
    public string class_code {get; set;} = string.Empty; // Код класса
    public QuikDateTime? datetime {get; set;}	// Дата и время
    public string exchange_code {get; set;} = string.Empty; // Код биржи в торговой системе
    public string exec_market {get; set;} = string.Empty; // Площадка исполнения
    public int flags {get; set;} // Набор битовых флагов
    public int open_interest {get; set;} // Открытый интерес
    public int period {get; set;} // Период торговой сессии. Возможные значения: «0» – Открытие; «1» – Нормальный; «2» – Закрытие
    public double price {get; set;} // Цена
    public int qty {get; set;} // Количество бумаг в последней сделке в лотах
    public int repo2value {get; set;} // Объем выкупа РЕПО
    public int reporate {get; set;} // Ставка РЕПО (%)
    public int repoterm {get; set;} // ?
    public int repovalue {get; set;} // Сумма РЕПО
    public string seccode {get; set;} = string.Empty; // Код бумаги заявки
    public string sec_code {get; set;} = string.Empty; // Код бумаги заявки
    public string settlecode {get; set;} = string.Empty; // Код расчетов
    public string trade_num {get; set;} = string.Empty; // Номер сделки в торговой системе
    public string tradenum {get; set;} = string.Empty; // Номер сделки в торговой системе
    public double value {get; set;} // Объем в денежных средствах
    public int yield {get; set;} // Доходность
   
    
    
    
    
    
    
    
    
    
    
    
}