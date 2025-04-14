using Google.Protobuf;
using Grpc.Core;
using System.Text.Json.Nodes;

namespace QuikGrpc.Services
{
    public class GRPCCallbackFunctions(ILogger<GRPCCallbackFunctions> logger) : CallbackFunctions.CallbackFunctionsBase
    {
        private readonly ILogger<GRPCCallbackFunctions> _logger = logger; // Будем вести лог
        private readonly Guid _id = Guid.NewGuid(); // Идентификатор канала для глобальных подписок
        private readonly List<Tuple<string, string>> _subscriptions = []; // Локальные подписки

        public override async Task Callback(IAsyncStreamReader<CallbackRequest> requestStream, IServerStreamWriter<CallbackResponse> responseStream, ServerCallContext context)
        {
            _logger.LogDebug($"Создан канал {_id}");
            Core.OnNewCallback += NewCallback; // Подписываемся на событие получения функции обратного вызова
            try // При закрытии канала со стороны клиента получаем исключение IOException
            {
                await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken)) // Ждем команды на подписку / отмену подписки до отмены
                {
                    string event_name = string.Empty, data = string.Empty; // Событие и параметры для запроса
                    bool subscribe = false; // Подписка / отмена подписки
                    switch (request.PayloadCase) // Смотрим тип подписки / отмены подписки
                    {
                        case CallbackRequest.PayloadOneofCase.Firm: // 2.2.1 OnFirm Новая фирма
                            event_name = "OnFirm"; // Событие
                            subscribe = request.Firm; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.AllTrade: // 2.2.2 OnAllTrade Новая обезличенная сделка
                            event_name = "OnAllTrade"; // Событие
                            string class_code = request.AllTrade.ClassSecCode.ClassCode; // Код режима торгов
                            string sec_code = request.AllTrade.ClassSecCode.SecCode; // Тикер
                            data = $"{class_code}|{sec_code}"; // Параметры для запроса
                            subscribe = request.AllTrade.Subscribe; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.Trade: // 2.2.3 OnTrade Новая сделка / Изменение существующей сделки
                            event_name = "OnTrade"; // Событие
                            subscribe = request.Trade; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.Order: // 2.2.4 OnOrder Новая заявка / Изменение существующей заявки
                            event_name = "OnOrder"; // Событие
                            subscribe = request.Order; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.AccountBalance: // 2.2.5 OnAccountBalance Изменение текущей позиции по счету
                            event_name = "OnAccountBalance"; // Событие
                            subscribe = request.AccountBalance; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.FuturesLimitChange: // 2.2.6 OnFuturesLimitChange Изменение ограничений по срочному рынку
                            event_name = "OnFuturesLimitChange"; // Событие
                            subscribe = request.FuturesLimitChange; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.FuturesLimitDelete: // 2.2.7 OnFuturesLimitDelete Удаление ограничений по срочному рынку
                            event_name = "OnFuturesLimitDelete"; // Событие
                            subscribe = request.FuturesLimitDelete; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.FuturesClientHolding: // 2.2.8 OnFuturesClientHolding Изменение позиции по срочному рынку
                            event_name = "OnFuturesClientHolding"; // Событие
                            subscribe = request.FuturesClientHolding; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.MoneyLimit: // 2.2.9 OnMoneyLimit Изменение денежной позиции
                            event_name = "OnMoneyLimit"; // Событие
                            subscribe = request.MoneyLimit; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.MoneyLimitDelete: // 2.2.10 OnMoneyLimitDelete Удаление денежной позиции
                            event_name = "OnMoneyLimitDelete"; // Событие
                            subscribe = request.MoneyLimitDelete; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.DepoLimit: // 2.2.11 OnDepoLimit Изменение позиций по инструментам
                            event_name = "OnDepoLimit"; // Событие
                            subscribe = request.DepoLimit; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.DepoLimitDelete: // 2.2.12 OnDepoLimitDelete Удаление позиции по инструментам
                            event_name = "OnDepoLimitDelete"; // Событие
                            subscribe = request.DepoLimitDelete; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.AccountPosition: // 2.2.13 OnAccountPosition Изменение денежных средств
                            event_name = "OnAccountPosition"; // Событие
                            subscribe = request.AccountPosition; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.StopOrder: // 2.2.16 OnStopOrder Новая стоп заявка / Изменение существующей стоп заявки
                            event_name = "OnStopOrder"; // Событие
                            subscribe = request.StopOrder; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.TransReply: // 2.2.17 OnTransReply Ответ на транзакцию пользователя
                            event_name = "OnTransReply"; // Событие
                            subscribe = request.TransReply; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.Param: // 2.2.18 OnParam Изменение текущих параметров
                            event_name = "OnParam"; // Событие
                            subscribe = request.Param; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.Quote: // 2.2.19 OnQuote Изменение стакана котировок
                            event_name = "OnQuote"; // Событие
                            subscribe = request.Quote.Subscribe; // Подписка / Отмена подписки
                            class_code = request.Quote.ClassSecCode.ClassCode; // Код режима торгов
                            sec_code = request.Quote.ClassSecCode.SecCode; // Тикер
                            data = $"{class_code}|{sec_code}"; // Параметры для запроса
                            break;
                        case CallbackRequest.PayloadOneofCase.Disconnected: // 2.2.20 OnDisconnected Отключение терминала от сервера QUIK
                            event_name = "OnDisconnected"; // Событие
                            subscribe = request.Disconnected; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.Connected: // 2.2.21 OnConnected Соединение терминала с сервером QUIK
                            event_name = "OnConnected"; // Событие
                            subscribe = request.Connected; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.Close: // 2.2.23 OnClose Закрытие терминала QUIK
                            event_name = "OnClose"; // Событие
                            subscribe = request.Close; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.Stop: // 2.2.24 OnStop Остановка LUA скрипта в терминале QUIK / закрытие терминала QUIK
                            event_name = "OnStop"; // Событие
                            subscribe = request.Stop; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.Init: // 2.2.25 OnInit Запуск LUA скрипта в терминале QUIK
                            event_name = "OnInit"; // Событие
                            subscribe = request.Init; // Подписка / отмена подписки
                            break;
                        case CallbackRequest.PayloadOneofCase.Candle: // QUIK# NewCandle Новая свечка
                            event_name = "NewCandle"; // Событие
                            subscribe = request.Candle.Subscribe; // Подписка / Отмена подписки
                            class_code = request.Candle.ClassSecCode.ClassCode; // Код режима торгов
                            sec_code = request.Candle.ClassSecCode.SecCode; // Тикер
                            int timeframe = (int)request.Candle.Timeframe; // Временной интервал в минутах
                            string param = string.IsNullOrEmpty(request.Candle.Param) ? "-" : request.Candle.Param; // Необязательный параметр
                            data = $"{class_code}|{sec_code}|{timeframe}|{param}"; // Параметры для запроса
                            break;
                        case CallbackRequest.PayloadOneofCase.Error: // QUIK# lua_error Сообщение об ошибке
                            event_name = "lua_error"; // Событие
                            subscribe = request.Error; // Подписка / отмена подписки
                            break;
                        default: // Для остальных событий (проверка на всякий случай)
                            return; // выходим, дальше не продолжаем
                    }
                    TryToSubscribeUnsubscribe(event_name, data, subscribe); // Пытаемся подписаться / отменить подписку
                }
            }
            catch (IOException) // The client reset the request stream
            {
                _logger.LogDebug($"Канал {_id} закрыт клиентом");
                var subs_to_delete = new List<Tuple<string, string>>(_subscriptions); // Копируем текущие открытые подписки для их отмены
                foreach (var sub in subs_to_delete) // Пробегаемся по всем подпискам             
                    TryToUnsubscribe(sub.Item1, sub.Item2); // Пытаемся отменить подписку
                _logger.LogDebug($"Все подписки канала {_id} отменены");
            }
            Core.OnNewCallback -= NewCallback; // Отписываемся от события получения функции обратного вызова
            //Core.CancellationToken.Cancel(); // Отменяем получения функций обратного вызова

            async Task NewCallback(JsonNode jsonNode)
            {
                var response = new CallbackResponse();
                switch (jsonNode["cmd"]?.ToString()) // Разбираем пришедшую команду события
                {
                    case "OnFirm": // 2.2.1 Функция вызывается терминалом QUIK при получении описания новой фирмы от сервера
                        if (!Subscribed("OnFirm", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.Firm = new JsonParser(Core.json_settings).Parse<Firm>(jsonNode["data"]!.ToString()); // Переводим полученные данные из JSON в proto класс
                        break;
                    case "OnAllTrade": // 2.2.2 Функция вызывается терминалом QUIK при получении обезличенной сделки
                        string class_code = jsonNode["data"]!["class_code"]!.ToString(); // Код режима торгов
                        string sec_code = jsonNode["data"]!["sec_code"]!.ToString(); // Тикер
                        string data = $"{class_code}|{sec_code}"; // Параметры для запроса/подписки
                        if (!Subscribed("OnAllTrade", data)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.AllTrade = new JsonParser(Core.json_settings).Parse<AllTrade>(jsonNode["data"]!.ToString()); // Переводим полученные данные из JSON в proto класс
                        break;
                    case "OnTrade": // 2.2.3 Функция вызывается терминалом QUIK при получении сделки или при изменении параметров существующей сделки
                        if (!Subscribed("OnTrade", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.Trade = new JsonParser(Core.json_settings).Parse<Trade>(jsonNode["data"]!.ToString()); // Переводим полученные данные из JSON в proto класс
                        break;
                    case "OnOrder": // 2.2.4 Функция вызывается терминалом QUIK при получении новой заявки или при изменении параметров существующей заявки
                        if (!Subscribed("OnOrder", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.Order = new JsonParser(Core.json_settings).Parse<Order>(jsonNode["data"]!.ToString()); // Переводим полученные данные из JSON в proto класс
                        break;
                    case "OnAccountBalance": // 2.2.5 Функция вызывается терминалом QUIK при получении изменений текущей позиции по счету
                        if (!Subscribed("OnAccountBalance", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.AccountBalance = new JsonParser(Core.json_settings).Parse<AccountBalance>(jsonNode["data"]!.ToString()); // Переводим полученные данные из JSON в proto класс
                        break;
                    case "OnFuturesLimitChange": // 2.2.6 Функция вызывается терминалом QUIK при получении изменений ограничений по срочному рынку
                        if (!Subscribed("OnFuturesLimitChange", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.FuturesLimitChange = new JsonParser(Core.json_settings).Parse<FuturesLimit>(jsonNode["data"]!.ToString()); // Переводим полученные данные из JSON в proto класс
                        break;
                    case "OnFuturesLimitDelete": // 2.2.7 Функция вызывается терминалом QUIK при удалении лимита по срочному рынку
                        if (!Subscribed("OnFuturesLimitDelete", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.FuturesLimitDelete = new JsonParser(Core.json_settings).Parse<FuturesLimitDelete>(jsonNode["data"]!.ToString()); // Переводим полученные данные из JSON в proto класс
                        break;
                    case "OnFuturesClientHolding": // 2.2.8 Функция вызывается терминалом QUIK при изменении позиции по срочному рынку
                        if (!Subscribed("OnFuturesClientHolding", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.FuturesClientHolding = new JsonParser(Core.json_settings).Parse<FuturesClientHolding>(jsonNode["data"]!.ToString()); // Переводим полученные данные из JSON в proto класс
                        break;
                    case "OnMoneyLimit": // 2.2.9 Функция вызывается терминалом QUIK при получении изменений по денежной позиции клиента
                        if (!Subscribed("OnMoneyLimit", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.MoneyLimit = new JsonParser(Core.json_settings).Parse<MoneyLimit>(jsonNode["data"]!.ToString()); // Переводим полученные данные из JSON в proto класс
                        break;
                    case "OnMoneyLimitDelete": // 2.2.10 Функция вызывается терминалом QUIK при удалении денежной позиции
                        if (!Subscribed("OnMoneyLimitDelete", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.MoneyLimitDelete = new JsonParser(Core.json_settings).Parse<MoneyLimitDelete>(jsonNode["data"]!.ToString()); // Переводим полученные данные из JSON в proto класс
                        break;
                    case "OnDepoLimit": // 2.2.11 Функция вызывается терминалом QUIK при получении изменений позиции по инструментам
                        if (!Subscribed("OnDepoLimit", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.DepoLimit = new JsonParser(Core.json_settings).Parse<DepoLimit>(jsonNode["data"]!.ToString()); // Переводим полученные данные из JSON в proto класс
                        break;
                    case "OnDepoLimitDelete": // 2.2.12 Функция вызывается терминалом QUIK при удалении позиции клиента по инструментам
                        if (!Subscribed("OnDepoLimitDelete", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.DepoLimitDelete = new JsonParser(Core.json_settings).Parse<DepoLimitDelete>(jsonNode["data"]!.ToString()); // Переводим полученные данные из JSON в proto класс
                        break;
                    case "OnAccountPosition": // 2.2.13 Функция вызывается терминалом QUIK при изменении позиции участника по денежным средствам
                        if (!Subscribed("OnAccountPosition", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.AccountPosition = new JsonParser(Core.json_settings).Parse<AccountPosition>(jsonNode["data"]!.ToString()); // Переводим полученные данные из JSON в proto класс
                        break;

                    // 2.2.14 (OnNegDeal) Функция вызывается терминалом QUIK при получении внебиржевой заявки или при изменении параметров существующей внебиржевой заявки
                    // 2.2.15 (OnNegTrade) Функция вызывается терминалом QUIK при получении сделки для исполнения или при изменении параметров существующей сделки для исполнения

                    case "OnStopOrder": // 2.2.16 Функция вызывается терминалом QUIK при получении новой стоп-заявки или при изменении параметров существующей стоп-заявки
                        if (!Subscribed("OnStopOrder", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.StopOrder = new JsonParser(Core.json_settings).Parse<StopOrder>(jsonNode["data"]!.ToString()); // Переводим полученные данные из JSON в proto класс
                        break;
                    case "OnTransReply": // 2.2.17 Функция вызывается терминалом QUIK при получении ответа на транзакцию пользователя,
                                         // отправленную с помощью любого плагина Рабочего места QUIK (в том числе QLua). Для транзакций,
                                         // отправленных с помощью Trans2quik.dll, QPILE или динамической загрузки транзакций из файла, функция не вызывается
                        if (!Subscribed("OnTransReply", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.TransReply = new JsonParser(Core.json_settings).Parse<TransReply>(jsonNode["data"]!.ToString()); // Переводим полученные данные из JSON в proto класс
                        break;
                    case "OnParam": // 2.2.18 OnParam Функция вызывается терминалом QUIK при изменении текущих параметров
                        if (!Subscribed("OnParam", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.Param = new JsonParser(Core.json_settings).Parse<ClassSecCode>(jsonNode["data"]!.ToString()); // Переводим полученные данные из JSON в proto класс
                        break;
                    case "OnQuote": // 2.2.19 OnQuote Функция вызывается терминалом QUIK при получении изменения стакана котировок
                        class_code = jsonNode["data"]!["class_code"]!.ToString(); // Код режима торгов
                        sec_code = jsonNode["data"]!["sec_code"]!.ToString(); // Тикер
                        data = $"{class_code}|{sec_code}"; // Параметры для запроса/подписки
                        if (!Subscribed("OnQuote", data)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.Quote = new JsonParser(Core.json_settings).Parse<Quote>(jsonNode["data"]!.ToString()); // Переводим полученные данные из JSON в proto класс
                        break;
                    case "OnDisconnected": // 2.2.20 Функция вызывается терминалом QUIK при отключении от сервера QUIK
                        if (!Subscribed("OnDisconnected", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.Disconnected = true; // Отключение
                        break;
                    case "OnConnected": // 2.2.21 Функция вызывается терминалом QUIK при установлении связи с сервером QUIK и получении терминалом описания хотя бы одного класса
                        if (!Subscribed("OnConnected", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.Connected = true; // Подключение
                        break;

                    // 2.2.22 (OnCleanUp) Функция вызывается терминалом QUIK в следующих случаях: смена сервера QUIK внутри торговой сессии; смена пользователя, которым выполняется подключение к серверу QUIK, внутри торговой сессии; смена сессии

                    case "OnClose": // 2.2.23 Функция вызывается перед закрытием терминала QUIK и при выгрузке файла qlua.dll
                        if (!Subscribed("OnClose", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.Close = new Empty(); // Возвращаем пустое значение. Важен сам факт закрытия
                        break;
                    case "OnStop": // 2.2.24 Функция вызывается терминалом QUIK при остановке скрипта из диалога управления и при закрытии терминала QUIK
                        if (!Subscribed("OnStop", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.Stop = new JsonParser(Core.json_settings).Parse<QuikInt>(jsonNode["data"]!.ToString()); // Переводим полученные данные из JSON в proto класс
                        break;
                    case "OnInit": // 2.2.25 Функция вызывается терминалом QUIK перед вызовом функции main(). В качестве параметра принимает значение полного пути к запускаемому скрипту
                        if (!Subscribed("OnInit", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.Init = new JsonParser(Core.json_settings).Parse<QuikString>(jsonNode["data"]!.ToString()); // Переводим полученные данные из JSON в proto класс
                        break;

                    // 2.2.26 (main) Функция, реализующая основной поток выполнения в скрипте. Для ее выполнения терминал QUIK создает отдельный поток.
                    // Скрипт считается работающим, пока работает функция Руководство пользователя Интерпретатора языка Lua 21 main().
                    // При завершении работы функции main() скрипт переходит в состояние «остановлен».
                    // Если скрипт находится в состоянии «остановлен», то не происходит вызовов функций обработки событий терминала QUIK, содержащихся в этом скрипте

                    case "NewCandle": // QUIK# Новая свечка
                        class_code = jsonNode["data"]!["class"]!.ToString(); // Код режима торгов
                        sec_code = jsonNode["data"]!["sec"]!.ToString(); // Тикер
                        string timeframe = jsonNode["data"]!["interval"]!.ToString(); // Временной интервал
                        string param = jsonNode["data"]!["param"] == null ? "-" : jsonNode["data"]!["param"]!.ToString(); // Необязательный параметр
                        data = $"{class_code}|{sec_code}|{timeframe}|{param}"; // Параметры для запроса/подписки
                        if (!Subscribed("NewCandle", data)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.Candle = new JsonParser(Core.json_settings).Parse<Candle>(jsonNode["data"]!.ToString());
                        break;
                    case "lua_error": // QUIK# Сообщение об ошибке при подписке/запросе стакана
                        if (!Subscribed("lua_error", string.Empty)) // Если подписки нет в локальных подписках
                            return; // то выходим, дальше не продолжаем
                        response.Error = new JsonParser(Core.json_settings).Parse<QuikString>(jsonNode["data"]!.ToString());
                        break;
                    default:
                        break;
                }
                await responseStream.WriteAsync(response);
            }
        }

        /// <summary>
        /// Команда подписки / отмена подписки
        /// </summary>
        /// <param name="event_name">Событие подписки</param>
        /// <param name="subscribe">True - подписка, False - отмена подписки</param>
        /// <returns>Команда подписки / отмена подписки</returns>
        private string GetCommand(string event_name, bool subscribe)
        {
            switch (event_name)
            {
                case "OnQuote": // Стакан
                    return subscribe ? "Subscribe_Level_II_Quotes" : "Unsubscribe_Level_II_Quotes"; // Команда для подписки / отмены подписки на стакан
                case "NewCandle": // Новая свечка
                    return subscribe ? "subscribe_to_candles" : "unsubscribe_from_candles"; // Команда подписки / отмены подписки на новые свечки
                default: // Для остальных событий
                    return string.Empty; // Команда подписки / отмена подписки не требуется
            }
        }

        /// <summary>
        /// Попытка подписки / отмены подписки
        /// </summary>
        /// <param name="event_name">Событие подписки</param>
        /// <param name="data">Параметры команды подписки</param>
        /// <param name="subscribe">True - подписка, False - отмена подписки</param>
        private void TryToSubscribeUnsubscribe(string event_name, string data, bool subscribe)
        {
            if (subscribe) // Если подписка
                TryToSubscribe(event_name, data); // то пытаемся подписаться
            else // Если отмена подписки
                TryToUnsubscribe(event_name, data); // то пытаемся отменить подписку
        }

        /// <summary>
        /// Попытка подписки
        /// </summary>
        /// <param name="event_name">Событие подписки</param>
        /// <param name="data">Параметры команды подписки</param>
        private void TryToSubscribe(string event_name, string data)
        {
            var new_subscription = new Tuple<string, string>(event_name, data); // Новая локальная подписка
            var new_core_subscription = new Tuple<Guid, string, string>(_id, event_name, data); // Новая глобальная подписка
            if (!Core.Subscriptions.Contains(new_core_subscription)) // Если этой подписки нет в глобальных подписках
            {
                if (!(from s in Core.Subscriptions where s.Item2 == event_name && s.Item3 == data select s).Any()) // Если подписки нет в глобальных подписках из других клиентов
                {
                    string command = GetCommand(event_name, true); // Получаем команду подписки
                    if (!string.IsNullOrEmpty(command)) // Если есть команда подписки
                    {
                        string result = Core.ProcessRequest(command, data)["data"]!.ToString(); // то пытаемся подписаться
                        if (result == "false") // Если не подписались
                        {
                            _logger.LogError($"Ошибка подписки на событие {event_name} {command} {data}");
                            return; // то выходим, дальше не продолжаем
                        }
                    }
                    Core.Subscriptions.Add(new_core_subscription); // то добавляем подписку в глобальные подписки
                    if (!_subscriptions.Contains(new_subscription)) // Если этой подписки нет в локальных подписках
                        _subscriptions.Add(new_subscription); // то добавляем подписку в локальные подписки
                }
                else // Если подписка есть в глобальных подписках из других клиентов (проверка на всякий случай)
                {
                    Core.Subscriptions.Add(new_core_subscription); // то добавляем подписку в глобальные подписки
                    if (!_subscriptions.Contains(new_subscription)) // Если этой подписки нет в локальных подписках
                        _subscriptions.Add(new_subscription); // то добавляем подписку в локальные подписки
                }
            }
            else // Если эта подписка есть в глобальных подписках (дубль подписки)
            {
                if (!_subscriptions.Contains(new_subscription)) // Если этой подписки нет в локальных подписках
                    _subscriptions.Add(new_subscription); // то добавляем подписку в локальные подписки
            }
            _logger.LogDebug($"Канал {_id} Подписка {event_name}({data})");
        }

        /// <summary>
        /// Попытка отмены подписки
        /// </summary>
        /// <param name="event_name">Событие подписки</param>
        /// <param name="data">Параметры команды отмены подписки</param>
        private void TryToUnsubscribe(string event_name, string data)
        {
            var existing_subscription = new Tuple<string, string>(event_name, data); // Локальная подписка
            var existing_core_subscription = new Tuple<Guid, string, string>(_id, event_name, data); // Глобальная подписка
            Core.Subscriptions.Remove(existing_core_subscription); // Удаляем подписку из глобальных подписок
            _subscriptions.Remove(existing_subscription); // Удаляем подписку из локальных подписок
            _logger.LogDebug($"Канал {_id} Отмена подписки {event_name}({data})");

            string command = GetCommand(event_name, false); // Получаем команду отмены подписки
            if (string.IsNullOrEmpty(command)) // Если у подписки нет команды отмены
                return; // то выходим, дальше не продолжаем
            if ((from s in Core.Subscriptions where s.Item2 == event_name && s.Item3 == data select s).Any()) // Если подписка есть в глобальных подписках из других клиентов
                return; // то выходим, дальше не продолжаем
            string result = Core.ProcessRequest(command, data)["data"]!.ToString(); // Пытаемся отменить подписку
            if (result == "false") // Если не отменили подписку
                _logger.LogError($"Ошибка отмены подписки на событие {event_name} {command} {data}");

        }

        /// <summary>
        /// Существует ли локальная подписка
        /// </summary>
        /// <param name="event_name">Событие подписки</param>
        /// <param name="data">Параметры команды подписки</param>
        /// <returns>Существует ли локальная подписка</returns>
        private bool Subscribed(string event_name, string data) => _subscriptions.Contains(new Tuple<string, string>(event_name, data));
    }
}
