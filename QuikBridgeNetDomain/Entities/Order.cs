namespace QuikBridgeNetDomain.Entities;

public class Order
{
    public string order_num {get; set;}	// Номер заявки в торговой системе
    public int flags {get; set;}	// Набор битовых флагов
    public string brokerref {get; set;} // Комментарий, обычно: <код клиента>/<номер поручения>
    public string userid {get; set;} // Идентификатор трейдера
    public string firmid {get; set;} // Идентификатор фирмы
    public string account {get; set;} // Торговый счет
    public double price {get; set;} // Цена
    public int qty {get; set;} // Количество в лотах
    public int balance {get; set;} // Остаток
    public double value {get; set;} // Объем в денежных средствах
    public double accruedint {get; set;} // 	Накопленный купонный доход
    public string trans_id {get; set;} // Идентификатор транзакции
    public string client_code {get; set;} // Код клиента
    public double price2 {get; set;} // Цена выкупа
    public string settlecode {get; set;} // Код расчетов
    public int uid {get; set;} // Идентификатор пользователя
    public int canceled_uid {get; set;} // Идентификатор пользователя, снявшего заявку
    public string exchange_code {get; set;} // Код биржи в торговой системе
    public int activation_time {get; set;} // Время активации
    public string linkedorder {get; set;} // Номер заявки в торговой системе
    public int expiry {get; set;} // Дата окончания срока действия заявки
    public string sec_code {get; set;} // Код бумаги заявки
    public string class_code {get; set;} // Код класса заявки
    public QuikDateTime datetime {get; set;}	// Дата и время
    public QuikDateTime withdraw_datetime {get; set;}	// Дата и время снятия заявки
    public string bank_acc_id {get; set;} // Идентификатор расчетного счета/кода в клиринговой организации
    public int value_entry_type {get; set;} // Способ указания объема заявки. Возможные значения: «0» – по количеству, «1» – по объему
    public int repoterm {get; set;} // Срок РЕПО, в календарных днях
    public double repovalue {get; set;} // Сумма РЕПО на текущую дату. Отображается с точностью 2 знака
    public double repo2value {get; set;} // Объём сделки выкупа РЕПО. Отображается с точностью 2 знака
    public double repo_value_balance {get; set;} // Остаток суммы РЕПО за вычетом суммы привлеченных или предоставленных по сделке РЕПО денежных средств в неисполненной части заявки, по состоянию на текущую дату. Отображается с точностью 2 знака
    public int start_discount {get; set;} // Начальный дисконт, в %
    public string reject_reason {get; set;} // Причина отклонения заявки брокером
    public int ext_order_flags {get; set;} // Битовое поле для получения специфических параметров с западных площадок
    public int min_qty {get; set;} // Минимально допустимое количество, которое можно указать в заявке по данному инструменту. Если имеет значение 0, значит ограничение по количеству не задано
    public int exec_type {get; set;} // Тип исполнения заявки. Возможные значения: «0» – «Значение не указано»; «1» – «Немедленно или отклонить»; «2» – «Поставить в очередь»; «3» – «Снять остаток»; «4» – «До снятия»; «5» – «До даты»; «6» – «В течение сессии»; «7» – «Открытие»; «8» – «Закрытие»; «9» – «Кросс»; «11» – «До следующей сессии»; «13» – «До отключения»; «15» – «До времени»; «16» –«Следующий аукцион»
    public int side_qualifier {get; set;} // Поле для получения параметров по западным площадкам. Если имеет значение «0», значит значение не задано
    public int acnt_type {get; set;} // Поле для получения параметров по западным площадкам. Если имеет значение «0», значит значение не задано
    public int capacity {get; set;} // Поле для получения параметров по западным площадкам. Если имеет значение «0», значит значение не задано
    public int passive_only_order {get; set;} // Поле для получения параметров по западным площадкам. Если имеет значение «0», значит значение не задано
    public int visible {get; set;} // Видимое количество. Параметр айсберг-заявок, для обычных заявок выводится значение: «0».
}