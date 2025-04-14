using Google.Protobuf;
using Grpc.Core;
using System.Text.Json.Nodes;

namespace QuikGrpc.Services
{
    public class GRPCInteractionFunctions : InteractionFunctions.InteractionFunctionsBase
    {
        private readonly ILogger<GRPCInteractionFunctions> _logger; // Будем вести лог

        public GRPCInteractionFunctions(ILogger<GRPCInteractionFunctions> logger)
        {
            _logger = logger; // Лог
        }

        #region 3. Функции взаимодействия скрипта Lua и Рабочего места QUIK

        #region Функции для обращения к строкам произвольных таблиц QUIK#

        public override Task<TradeAccounts> GetTradeAccounts(Empty request, ServerCallContext context) // Торговые счета, у которых указаны поддерживаемые классы инструментов
        {
            var response = Core.ProcessRequest("getTradeAccounts");
            var result = new TradeAccounts(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.TradeAccount.Add(new JsonParser(Core.json_settings).Parse<TradeAccount>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        public override Task<QuikString> GetTradeAccount(QuikString request, ServerCallContext context) => // Торговый счет для режима торгов
            Task.FromResult(new QuikString { Value = Core.ProcessRequest("getTradeAccount", request.Value)["data"]!.ToString() });

        public override Task<Orders> GetAllOrders(Empty request, ServerCallContext context) // Все заявки
        {
            var response = Core.ProcessRequest("get_orders");
            var result = new Orders(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.Order.Add(new JsonParser(Core.json_settings).Parse<Order>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        public override Task<Orders> GetOrders(ClassSecCode request, ServerCallContext context) // Заявки по тикеру
        {
            var response = Core.ProcessRequest("get_orders", $"{request.ClassCode}|{request.SecCode}");
            var result = new Orders(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.Order.Add(new JsonParser(Core.json_settings).Parse<Order>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        public override Task<Order> GetOrderByNumber(QuikString request, ServerCallContext context) // Заявка по номеру
        {
            var response = Core.ProcessRequest("getOrder_by_Number", request.Value);
            return Task.FromResult(new JsonParser(Core.json_settings).Parse<Order>(response["data"]!.ToString())); // Переводим полученные данные из JSON в proto класс
        }

        public override Task<Order> GetOrderById(OrderByIdRequest request, ServerCallContext context) // Заявка по тикеру и коду транзакции заявки
        {
            var response = Core.ProcessRequest("getOrder_by_ID", $"{request.ClassSecCode.ClassCode}|{request.ClassSecCode.SecCode}|{request.OrderTransId}");
            return Task.FromResult(new JsonParser(Core.json_settings).Parse<Order>(response["data"]!.ToString())); // Переводим полученные данные из JSON в proto класс
        }

        public override Task<Order> GetOrderByClassNumber(OrderByClassNumberRequest request, ServerCallContext context) // Заявка по режиму торгов и номеру
        {
            var response = Core.ProcessRequest("getOrder_by_Number", $"{request.ClassCode}|{request.OrderId}");
            return Task.FromResult(new JsonParser(Core.json_settings).Parse<Order>(response["data"]!.ToString())); // Переводим полученные данные из JSON в proto класс
        }

        public override Task<MoneyLimits> GetMoneyLimits(Empty request, ServerCallContext context) // Все позиции по деньгам
        {
            var response = Core.ProcessRequest("getMoneyLimits");
            var result = new MoneyLimits(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.MoneyLimit.Add(new JsonParser(Core.json_settings).Parse<MoneyLimit>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        public override Task<QuikString> GetClientCode(Empty request, ServerCallContext context) => // Основной (первый) код клиента
            Task.FromResult(new QuikString { Value = Core.ProcessRequest("getClientCode")["data"]!.ToString() });

        public override Task<QuikStrings> GetClientCodes(Empty request, ServerCallContext context) // Все коды клиента
        {
            var response = Core.ProcessRequest("getClientCodes");
            var result = new QuikStrings(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                if (!result.Value.Contains(item!.ToString())) // Если значения нет в списке
                    result.Value.Add(item!.ToString()); // то добавляем значение в список
            return Task.FromResult(result);
        }

        public override Task<DepoLimits> GetAllDepoLimits(Empty request, ServerCallContext context) // Лимиты по всем инструментам
        {
            var response = Core.ProcessRequest("get_depo_limits");
            var result = new DepoLimits(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.DepoLimit.Add(new JsonParser(Core.json_settings).Parse<DepoLimit>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        public override Task<DepoLimit> GetAllDepoLimit(QuikString request, ServerCallContext context) // Лимиты по инструменту
        {
            var response = Core.ProcessRequest("get_depo_limits", request.Value);
            return Task.FromResult(new JsonParser(Core.json_settings).Parse<DepoLimit>(response["data"]!.ToString())); // Переводим полученные данные из JSON в proto класс
        }

        public override Task<Trades> GetAllTrades(Empty request, ServerCallContext context) // Все сделки
        {
            var response = Core.ProcessRequest("get_trades");
            var result = new Trades(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.Trade.Add(new JsonParser(Core.json_settings).Parse<Trade>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        public override Task<Trades> GetTrades(ClassSecCode request, ServerCallContext context) // Сделки по инструменту
        {
            var response = Core.ProcessRequest("get_trades", $"{request.ClassCode}|{request.SecCode}");
            var result = new Trades(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.Trade.Add(new JsonParser(Core.json_settings).Parse<Trade>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        public override Task<Trades> GetTradesByOrderNumber(QuikString request, ServerCallContext context) // Сделки по номеру заявки
        {
            var response = Core.ProcessRequest("get_Trades_by_OrderNumber", request.Value);
            var result = new Trades(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.Trade.Add(new JsonParser(Core.json_settings).Parse<Trade>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        public override Task<StopOrders> GetAllStopOrders(Empty request, ServerCallContext context) // Все стоп заявки
        {
            var response = Core.ProcessRequest("get_stop_orders");
            var result = new StopOrders(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.StopOrder.Add(new JsonParser(Core.json_settings).Parse<StopOrder>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        public override Task<StopOrders> GetStopOrders(ClassSecCode request, ServerCallContext context) // Стоп заявки по инструменту
        {
            var response = Core.ProcessRequest("get_stop_orders", $"{request.ClassCode}|{request.SecCode}");
            var result = new StopOrders(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.StopOrder.Add(new JsonParser(Core.json_settings).Parse<StopOrder>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        public override Task<AllTrades> GetAllTrade(Empty request, ServerCallContext context) // Все обезличенные сделки
        {
            var response = Core.ProcessRequest("get_all_trades");
            var result = new AllTrades(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.AllTrade.Add(new JsonParser(Core.json_settings).Parse<AllTrade>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        public override Task<AllTrades> GetTrade(ClassSecCode request, ServerCallContext context) // Обезличенные сделки по инструменту
        {
            var response = Core.ProcessRequest("get_all_trades", $"{request.ClassCode}|{request.SecCode}");
            var result = new AllTrades(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.AllTrade.Add(new JsonParser(Core.json_settings).Parse<AllTrade>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        #endregion

        #region 3.2 Функции для обращения к спискам доступных параметров

        public override Task<QuikStrings> GetClassesList(Empty request, ServerCallContext context) // 3.2.1 Функция предназначена для получения списка режимов торгов, переданных с сервера в ходе сеанса связи
        {
            var response = Core.ProcessRequest("getClassesList"); // Коды классов в списке разделяются запятой. В конце полученной строки также запятая
            string response_str = response["data"]!.ToString().TrimEnd(','); // Переводим значение в строку без последней запятой
            string[] response_list = response_str.Split(','); // Разделяем коды классов по запятой и добавляем их в массив строк
            var result = new QuikStrings(); // Результат будуем выдавать в виде списка строк
            foreach (string str in response_list) // Пробегаемся по каждой строке в массиве строк
                result.Value.Add(str); // Добавляем строку в результат
            return Task.FromResult(result);
        }

        public override Task<Class> GetClassInfo(QuikString request, ServerCallContext context) // 3.2.2 Функция предназначена для получения информации о режиме торгов
        {
            var response = Core.ProcessRequest("getClassInfo", request.Value);
            return Task.FromResult(new JsonParser(Core.json_settings).Parse<Class>(response["data"]!.ToString())); // Переводим полученные данные из JSON в proto класс
        }

        public override Task<QuikStrings> GetClassSecurities(QuikStrings request, ServerCallContext context) // 3.2.3 Функция предназначена для получения списка кодов инструментов для списка режимов торгов, заданного списком кодов
        {
            var response = Core.ProcessRequest("getClassSecurities", string.Join('|', [.. request.Value])); // Коды инструментов в списке разделяются запятой. В конце полученной строки также запятая
            string response_str = response["data"]!.ToString().TrimEnd(','); // Переводим значение в строку без последней запятой
            string[] response_list = response_str.Split(','); // Разделяем коды классов по запятой и добавляем их в массив строк
            var result = new QuikStrings(); // Результат будуем выдавать в виде списка строк
            foreach (string str in response_list) // Пробегаемся по каждой строке в массиве строк
                result.Value.Add(str); // Добавляем строку в результат
            return Task.FromResult(result);
        }

        public override Task<OptionBoard> GetOptionBoard(ClassSecCode request, ServerCallContext context) // QUIK# Доска опционов
        {
            var response = Core.ProcessRequest("getOptionBoard", $"{request.ClassCode}|{request.SecCode}");
            return Task.FromResult(new JsonParser(Core.json_settings).Parse<OptionBoard>(response["data"]!.ToString())); // Переводим полученные данные из JSON в proto класс
        }

        #endregion

        #region 3.3 Функция для получения информации по денежным средствам

        public override Task<MoneyPositions> GetMoney(MoneyRequest request, ServerCallContext context) // 3.3.1 Функция предназначена для получения информации по денежным позициям 
        {
            var response = Core.ProcessRequest("getMoney", $"{request.ClientCode}|{request.Firmid}|{request.Tag}|{request.Currcode}");
            var result = new MoneyPositions(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.MoneyPosition.Add(new JsonParser(Core.json_settings).Parse<MoneyPosition>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        public override Task<MoneyLimits> GetMoneyEx(MoneyExRequest request, ServerCallContext context) // 3.3.2 Функция предназначена для получения информации по денежным позициям указанного типа
        {
            var response = Core.ProcessRequest("getMoneyEx", $"{request.MoneyRequest.Firmid}|{request.MoneyRequest.ClientCode}|{request.MoneyRequest.Tag}|{request.MoneyRequest.Currcode}|{request.LimitKind}");
            var result = new MoneyLimits(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.MoneyLimit.Add(new JsonParser(Core.json_settings).Parse<MoneyLimit>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        #endregion

        #region 3.4 Функция для получения позиций по инструментам

        public override Task<SecPositions> GetDepo(DepoRequest request, ServerCallContext context) // 3.4.1 Функция предназначена для получения позиций по инструментам
        {
            var response = Core.ProcessRequest("getDepo", $"{request.ClientCode}|{request.Firmid}|{request.SecCode}|{request.Trdaccid}");
            var result = new SecPositions(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.SecPosition.Add(new JsonParser(Core.json_settings).Parse<SecPosition>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        public override Task<DepoLimits> GetDepoEx(DepoExRequest request, ServerCallContext context) // 3.4.2 Функция предназначена для получения позиций по инструментам указанного типа
        {
            var response = Core.ProcessRequest("getDepoEx", $"{request.Firmid}|{request.ClientCode}|{request.SecCode}|{request.Trdaccid}|{request.LimitKind}");
            var result = new DepoLimits(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.DepoLimit.Add(new JsonParser(Core.json_settings).Parse<DepoLimit>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        #endregion

        #region 3.5 Функция для получения информации по фьючерсным лимитам

        public override Task<FuturesLimit> GetFuturesLimit(FuturesLimitRequest request, ServerCallContext context) // 3.5.1 Функция предназначена для получения информации по фьючерсным лимитам
        {
            var response = Core.ProcessRequest("getFuturesLimit", $"{request.Firmid}|{request.Trdaccid}|{request.LimitType}|{request.Currcode}");
            return Task.FromResult(new JsonParser(Core.json_settings).Parse<FuturesLimit>(response["data"]!.ToString())); // Переводим полученные данные из JSON в proto класс
        }

        public override Task<FuturesLimits> GetFuturesClientLimits(Empty request, ServerCallContext context) // QUIK# Все фьючерсные лимиты
        {
            var response = Core.ProcessRequest("getFuturesClientLimits");
            var result = new FuturesLimits(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.FuturesLimit.Add(new JsonParser(Core.json_settings).Parse<FuturesLimit>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        #endregion

        #region 3.6 Функция для получения информации по фьючерсным позициям

        public override Task<FuturesClientHolding> GetFuturesHolding(FuturesHoldingRequest request, ServerCallContext context) // 3.6.1 Функция предназначена для получения информации по фьючерсным позициям
        {
            var response = Core.ProcessRequest("getFuturesHolding", $"{request.Firmid}|{request.Trdaccid}|{request.SecCode}|{request.Type}");
            return Task.FromResult(new JsonParser(Core.json_settings).Parse<FuturesClientHolding>(response["data"]!.ToString())); // Переводим полученные данные из JSON в proto класс
        }

        public override Task<FuturesClientHoldings> GetFuturesHoldings(Empty request, ServerCallContext context) // QUIK# Все фьючерсные позиции
        {
            var response = Core.ProcessRequest("getFuturesClientHoldings");
            var result = new FuturesClientHoldings(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.FuturesClientHolding.Add(new JsonParser(Core.json_settings).Parse<FuturesClientHolding>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        #endregion

        #region 3.7 Функция для получения информации по инструменту

        public override Task<Security> GetSecurityInfo(ClassSecCode request, ServerCallContext context) // 3.7.1 Функция предназначена для получения информации по инструменту
        {
            var response = Core.ProcessRequest("getSecurityInfo", $"{request.ClassCode}|{request.SecCode}");
            return Task.FromResult(new JsonParser(Core.json_settings).Parse<Security>(response["data"]!.ToString())); // Переводим полученные данные из JSON в proto класс
        }

        public override Task<Securities> GetSecurityInfoBulk(SecurityInfoBulkRequest request, ServerCallContext context) // QUIK# Информация по инструментам
        {
            var request_array = new JsonArray();
            foreach (var item in request.ClassSecCode) // Пробегаемся по всем инструментам
                request_array.Add($"{item.ClassCode}|{item.SecCode}"); // Добавляем инструмент в таблицу
            var response = Core.ProcessRequest("getSecurityInfoBulk", request_array.ToJsonString());
            var result = new Securities(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                // TODO legs -> leg_<N>
                result.Security.Add(new JsonParser(Core.json_settings).Parse<Security>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        public override Task<QuikString> GetSecurityClass(SecurityClassRequest request, ServerCallContext context) // QUIK# Режим торгов по коду инструмента из заданных режимов торгов
        {
            string classes = string.Empty; // Строка режимов торгов
            foreach (string class_code in request.ClassesList) // Пробегаемся по всем режимам торгов
                classes += class_code + ','; // Добавляем режим торгов в строку, разделяем запятой
            // TODO legs -> leg_<N>
            return Task.FromResult(new QuikString { Value = Core.ProcessRequest("getSecurityClass", $"{classes}|{request.SecCode}")["data"]!.ToString() });
        }

        #endregion

        #region 3.9 Функция для получения стакана по указанному режиму торгов и инструменту

        public override Task<Quote> GetQuoteLevel2(ClassSecCode request, ServerCallContext context) // 3.9.1 Функция предназначена для получения стакана по указанному режиму торгов и инструменту
        {
            var response = Core.ProcessRequest("GetQuoteLevel2", $"{request.ClassCode}|{request.SecCode}");
            return Task.FromResult(new JsonParser(Core.json_settings).Parse<Quote>(response["data"]!.ToString())); // Переводим полученные данные из JSON в proto класс
        }

        #endregion

        #region 3.10 Функции для работы с графиками

        public override Task<QuikInt> GetNumCandles(QuikString request, ServerCallContext context) => // 3.10.2 Функция предназначена для получения информации о количестве свечек по выбранному идентификатору
            Task.FromResult(new QuikInt { Value = int.Parse(Core.ProcessRequest("get_num_candles", request.Value)["data"]!.ToString()) });

        public override Task<Candles> GetCandles(CandlesRequest request, ServerCallContext context) // QUIK# Свечи по идентификатору графика
        {
            var response = Core.ProcessRequest("get_candles", $"{request.Tag}|{request.Line}|{request.FirstCandle}|{request.Count}");
            var result = new Candles(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.Candle.Add(new JsonParser(Core.json_settings).Parse<Candle>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        public override Task<Candles> GetCandlesFromDataSource(CandlesFromDataSourceRequest request, ServerCallContext context) // QUIK# Свечи
        {
            string param = string.IsNullOrEmpty(request.Param) ? "-" : request.Param; // Необязательный параметр
            var response = Core.ProcessRequest("get_candles_from_data_source", $"{request.ClassSecCode.ClassCode}|{request.ClassSecCode.SecCode}|{(int)request.Interval}|{request.Param}|{request.Count}");
            var result = new Candles(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.Candle.Add(new JsonParser(Core.json_settings).Parse<Candle>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        #endregion

        #region 3.11 Функции для работы с заявками

        //public override Task<QuikBool> SendTransaction(QuikString request, ServerCallContext context) => // 3.11.1 Функция предназначена для отправки транзакций в торговую систему
        //    Task.FromResult(new QuikBool { Value = Core.ProcessRequest("sendTransaction", request.Value)["data"]!.ToString() == "true" });

        public override Task<QuikBool> SendTransaction(QuikString request, ServerCallContext context)
        {
            // 3.11.1 Функция предназначена для отправки транзакций в торговую систему
            var result = Core.ProcessRequest("sendTransaction", request.Value);
            return Task.FromResult(new QuikBool { Value = result["data"]!.ToString() == "true" });
        }
        #endregion

        #region 3.12 Функции для получения значений таблицы Текущие торги

        public override Task<ParamExResponse> GetParamEx(ParamExRequest request, ServerCallContext context) // 3.12.1 Функция предназначена для получения значений всех параметров биржевой информации из таблицы Текущие торги. С помощью этой функции можно получить любое из значений Таблицы текущих торгов для заданных кодов класса и инструмента
        {
            var response = Core.ProcessRequest("getParamEx", $"{request.ClassSecCode.ClassCode}|{request.ClassSecCode.SecCode}|{request.ParamName}");
            return Task.FromResult(new JsonParser(Core.json_settings).Parse<ParamExResponse>(response["data"]!.ToString())); // Переводим полученные данные из JSON в proto класс
        }

        public override Task<ParamExResponse> GetParamEx2(ParamExRequest request, ServerCallContext context) // 3.12.2 Функция предназначена для получения значений всех параметров биржевой информации из Таблицы текущих торгов с возможностью в дальнейшем отказаться от получения определенных параметров, заказанных с помощью функции ParamRequest
        {
            var response = Core.ProcessRequest("getParamEx2", $"{request.ClassSecCode.ClassCode}|{request.ClassSecCode.SecCode}|{request.ParamName}");
            return Task.FromResult(new JsonParser(Core.json_settings).Parse<ParamExResponse>(response["data"]!.ToString())); // Переводим полученные данные из JSON в proto класс
        }

        public override Task<ParamExResponses> GetParamEx2Bulk(ParamExBulkRequest request, ServerCallContext context) // QUIK# Таблица текущих торгов по инструментам с возможностью отказа от получения
        {
            var request_array = new JsonArray();
            foreach (var item in request.ClassSecCodeParam) // Пробегаемся по всем режимам торгов/инструментам/параметрам
                request_array.Add($"{item.ClassCode}|{item.SecCode}|{item.ParamName}"); // Добавляем режим торгов/инструмент/параметр в таблицу
            var response = Core.ProcessRequest("getParamEx2Bulk", request_array.ToJsonString());
            var result = new ParamExResponses(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.ParamExResponse.Add(new JsonParser(Core.json_settings).Parse<ParamExResponse>(item!.ToString())); // Добавляем значение в список
            return Task.FromResult(result);
        }

        #endregion

        #region 3.13 Функции для получения параметров таблицы Клиентский портфель

        public override Task<PortfolioInfo> GetPortfolioInfo(FirmClientCodeRequest request, ServerCallContext context) // 3.13.1 Функция предназначена для получения значений параметров таблицы Клиентский портфель, соответствующих идентификатору участника торгов firmid, коду клиента client_code и сроку расчетов limit_kind со значением 0
        {
            var response = Core.ProcessRequest("getPortfolioInfo", $"{request.Firmid}|{request.ClientCode}");
            return Task.FromResult(new JsonParser(Core.json_settings).Parse<PortfolioInfo>(response["data"]!.ToString())); // Переводим полученные данные из JSON в proto класс
        }

        public override Task<PortfolioInfo> GetPortfolioInfoEx(PortfolioInfoExRequest request, ServerCallContext context) // 3.13.2 Функция предназначена для получения значений параметров таблицы Клиентский портфель, соответствующих идентификатору участника торгов firmid, коду клиента client_code и сроку расчетов limit_kind со значением, заданным пользователем.
        {
            var response = Core.ProcessRequest("getPortfolioInfoEx", $"{request.Firmid}|{request.ClientCode}|{request.LimitKind}");
            return Task.FromResult(new JsonParser(Core.json_settings).Parse<PortfolioInfo>(response["data"]!.ToString())); // Переводим полученные данные из JSON в proto класс
        }

        #endregion

        #region 3.16 Функции для работы с метками

        public override Task<QuikInt> AddLabel(LabelRequest request, ServerCallContext context) => // 3.16.1 Добавляет метку с заданными параметрами
            Task.FromResult(new QuikInt { Value = int.Parse(Core.ProcessRequest("AddLabel", $"{request.Price}|{request.CurDate}|{request.CurTime}|{request.Qty}|{request.Path}|{request.ChartTag}|{request.Alignment}|{request.Background}")["data"]!.ToString()) });

        public override Task<QuikBool> DelLabel(ChartLabelRequest request, ServerCallContext context) => // 3.16.2 Удаляет метку с заданными параметрами
            Task.FromResult(new QuikBool { Value = Core.ProcessRequest("DelLabel", $"{request.ChartTag}|{request.LabelId}")["data"]!.ToString() == "true" });

        public override Task<QuikBool> DelAllLabels(QuikString request, ServerCallContext context) => // 3.16.3 Команда удаляет все метки на диаграмме с указанным графиком
            Task.FromResult(new QuikBool { Value = Core.ProcessRequest("DelAllLabels", request.Value)["data"]!.ToString() == "true" });

        public override Task<LabelParams> GetLabelParams(ChartLabelRequest request, ServerCallContext context) // 3.16.4 Команда позволяет получить параметры метки
        {
            var response = Core.ProcessRequest("GetLabelParams", $"{request.ChartTag}|{request.LabelId}");
            return Task.FromResult(new JsonParser(Core.json_settings).Parse<LabelParams>(response["data"]!.ToString())); // Переводим полученные данные из JSON в proto класс
        }

        #endregion

        #region 3.18 Функции для заказа параметров Таблицы текущих торгов

        public override Task<QuikBool> ParamRequest(ParamExRequest request, ServerCallContext context) => // 3.18.1 Функция заказывает получение параметров Таблицы текущих торгов
            Task.FromResult(new QuikBool { Value = Core.ProcessRequest("paramRequest", $"{request.ClassSecCode.ClassCode}|{request.ClassSecCode.SecCode}|{request.ParamName}")["data"]!.ToString() == "true" });

        public override Task<QuikBool> CancelParamRequest(ParamExRequest request, ServerCallContext context) => // 3.18.2 Функция отменяет заказ на получение параметров Таблицы текущих торгов
            Task.FromResult(new QuikBool { Value = Core.ProcessRequest("cancelParamRequest", $"{request.ClassSecCode.ClassCode}|{request.ClassSecCode.SecCode}|{request.ParamName}")["data"]!.ToString() == "true" });

        public override Task<QuikBools> ParamRequestBulk(ParamExBulkRequest request, ServerCallContext context) // QUIK# Заказ получения таблицы текущих торгов по инструментам
        {
            var request_array = new JsonArray();
            foreach (var item in request.ClassSecCodeParam) // Пробегаемся по всем режимам торгов/инструментам/параметрам
                request_array.Add($"{item.ClassCode}|{item.SecCode}|{item.ParamName}"); // Добавляем режим торгов/инструмент/параметр в таблицу
            var response = Core.ProcessRequest("paramRequestBulk", request_array.ToJsonString());
            var result = new QuikBools(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.Value.Add(item!.ToString() == "true"); // Добавляем значение в список
            return Task.FromResult(result);
        }

        public override Task<QuikBools> CancelParamRequestBulk(ParamExBulkRequest request, ServerCallContext context) // QUIK# Отмена заказа получения таблицы текущих торгов по инструментам
        {
            var request_array = new JsonArray();
            foreach (var item in request.ClassSecCodeParam) // Пробегаемся по всем режимам торгов/инструментам/параметрам
                request_array.Add($"{item.ClassCode}|{item.SecCode}|{item.ParamName}"); // Добавляем режим торгов/инструмент/параметр в таблицу
            var response = Core.ProcessRequest("cancelParamRequestBulk", request_array.ToJsonString());
            var result = new QuikBools(); // Список значений
            foreach (var item in (JsonArray)response["data"]!) // Пробегаемся по всем полученным значениям
                result.Value.Add(item!.ToString() == "true"); // Добавляем значение в список
            return Task.FromResult(result);
        }

        #endregion

        #region 3.19 Функции для получения информации по единой денежной позиции

        public override Task<QuikString> GetTrdAccByClientCode(FirmClientCodeRequest request, ServerCallContext context) => // 3.19.1 Функция возвращает торговый счет срочного рынка, соответствующий коду клиента фондового рынка с единой денежной позицией
            Task.FromResult(new QuikString { Value = Core.ProcessRequest("getTrdAccByClientCode", $"{request.Firmid}|{request.ClientCode}")["data"]!.ToString() });

        public override Task<QuikString> GetClientCodeByTrdAcc(ClientCodeByTrdAccRequest request, ServerCallContext context) => // 3.19.2 Функция возвращает код клиента фондового рынка с единой денежной позицией, соответствующий торговому счету срочного рынка
            Task.FromResult(new QuikString { Value = Core.ProcessRequest("getClientCodeByTrdAcc", $"{request.Firmid}|{request.TradeAccountId}")["data"]!.ToString() });

        public override Task<QuikBool> IsUcpClient(FirmClientCodeRequest request, ServerCallContext context) => // 3.19.3 Функция предназначена для получения признака, указывающего имеет ли клиент единую денежную позицию
            Task.FromResult(new QuikBool { Value = Core.ProcessRequest("IsUcpClient")["data"]!.ToString() == "true" });

        #endregion

        #endregion
    }
}
