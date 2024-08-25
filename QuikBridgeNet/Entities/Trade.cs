namespace QuikBridgeNet.Entities;

public class Trade
{
    public int trade_num {get; set;} // Номер сделки в торговой системе
    public int order_num {get; set;} // Номер заявки в торговой системе
    public int brokerref {get; set;} // Комментарий, обычно: <код клиента>/<номер поручения>
    public int userid {get; set;} // Идентификатор трейдера
    public int firmid {get; set;} // Идентификатор дилера
    public int canceled_uid {get; set;} // Идентификатор пользователя, отказавшегося от сделки
    public int account {get; set;} // Торговый счет
    public int price {get; set;} // Цена
    public int qty {get; set;} // Количество бумаг в последней сделке в лотах
    public int value {get; set;} // Объем в денежных средствах
    public int accruedint {get; set;} // Накопленный купонный доход
    public int settlecode {get; set;} // Код расчетов
    public int cpfirmid {get; set;} // Код фирмы партнера
    public int flags {get; set;} // Набор битовых флагов
    public int price2 {get; set;} // Цена выкупа
    public int reporate {get; set;} // Ставка РЕПО (%)
    public int client_code {get; set;} // Код клиента
    public int accrued2 {get; set;} // Доход (%) на дату выкупа
    public int repoterm {get; set;} // Срок РЕПО, в календарных днях
    public int repovalue {get; set;} // Сумма РЕПО
    public int repo2value {get; set;} // Объем выкупа РЕПО
    public int start_discount {get; set;} // Начальный дисконт (%)
    public int lower_discount {get; set;} // Нижний дисконт (%)
    public int upper_discount {get; set;} // Верхний дисконт (%)
    public int block_securities {get; set;} // Блокировка обеспечения («Да»/«Нет»)
    public int clearing_comission {get; set;} // Клиринговая комиссия биржи
    public int exchange_comission {get; set;} // Комиссия Фондовой биржи
    public int tech_center_comission {get; set;} // Комиссия Технического центра
    public int settle_date {get; set;} // Дата расчетов
    public int settle_currency {get; set;} // Валюта расчетов
    public int trade_currency {get; set;} // Валюта
    public int exchange_code {get; set;} // Код биржи в торговой системе
    public int station_id {get; set;} // Идентификатор рабочей станции
    public int sec_code {get; set;} // Код бумаги заявки
    public int class_code {get; set;} // Код класса
    public QuikDateTime datetime {get; set;}	// Дата и время
    public int bank_acc_id {get; set;} // Идентификатор расчетного счета/кода в клиринговой организации
    public int broker_comission {get; set;} // Комиссия брокера. Отображается с точностью до 2 двух знаков. Поле зарезервировано для будущего использования
    public int linked_trade {get; set;} // Номер витринной сделки в Торговой Системе для сделок РЕПО с ЦК и SWAP
    public int period {get; set;} // Период торговой сессии. Возможные значения: «0» – Открытие; «1» – Нормальный; «2» – Закрытие
    public int trans_id {get; set;} // Идентификатор транзакции
    public int kind {get; set;} // Тип сделки. Возможные значения: «1» – Обычная; «2» – Адресная; «3» – Первичное размещение; «4» – Перевод денег/бумаг; «5» – Адресная сделка первой части РЕПО; «6» – Расчетная по операции своп; «7» – Расчетная по внебиржевой операции своп; «8» – Расчетная сделка бивалютной корзины; «9» – Расчетная внебиржевая сделка бивалютной корзины; «10» – Сделка по операции РЕПО с ЦК; «11» – Первая часть сделки по операции РЕПО с ЦК; «12» – Вторая часть сделки по операции РЕПО с ЦК; «13» – Адресная сделка по операции РЕПО с ЦК; «14» – Первая часть адресной сделки по операции РЕПО с ЦК; «15» – Вторая часть адресной сделки по операции РЕПО с ЦК; «16» – Техническая сделка по возврату активов РЕПО с ЦК; «17» – Сделка по спреду между фьючерсами разных сроков на один актив; «18» – Техническая сделка первой части от спреда между фьючерсами; «19» – Техническая сделка второй части от спреда между фьючерсами; «20» – Адресная сделка первой части РЕПО с корзиной; «21» – Адресная сделка второй части РЕПО с корзиной; «22» – Перенос позиций срочного рынка
    public int clearing_bank_accid {get; set;} // Идентификатор счета в НКЦ (расчетный код)
    public QuikDateTime canceled_datetime {get; set;}	// Дата и время снятия сделки
    public int clearing_firmid {get; set;} // Идентификатор фирмы - участника клиринга
    public int system_ref {get; set;} // Дополнительная информация по сделке, передаваемая торговой системой
    public int uid {get; set;} // Идентификатор пользователя на сервере QUIK
}