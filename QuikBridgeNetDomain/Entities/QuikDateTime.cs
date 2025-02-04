namespace QuikBridgeNetDomain.Entities;

public class QuikDateTime
{
    public int mcs {get; set;} // Микросекунды
    public int ms {get; set;} // Миллисекунды
    public int sec {get; set;} // Секунды
    public int min {get; set;} // Минуты
    public int hour {get; set;} // Часы
    public int day {get; set;} // День
    public int week_day {get; set;} // Номер дня недели
    public int month {get; set;} // Месяц
    public int year {get; set;} // Год

}