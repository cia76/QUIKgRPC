from typing import Union  # Объединение типов
from queue import SimpleQueue  # Очередь команд на подписки / отмену подписок
from threading import Thread  # Поток событий функций обратного вызова
import logging  # Будем вести лог

from pytz import timezone  # Работаем с временнОй зоной
from grpc import insecure_channel, RpcError  # Канал

from QUIKgRPC.Python.grpc.structures_pb2 import Empty, MoneyLimits, TradeAccounts, ClassSecCode, Security
from QUIKgRPC.Python.grpc.service_pb2_grpc import ServiceFunctionsStub  # Фукнции отладки QUIK# / 2.1 Сервисные функции
from QUIKgRPC.Python.grpc.interaction_pb2 import SecurityClassRequest
from QUIKgRPC.Python.grpc.interaction_pb2_grpc import InteractionFunctionsStub  # 3. Функции взаимодействия скрипта Lua и Рабочего места QUIK
from QUIKgRPC.Python.grpc.callback_pb2_grpc import CallbackFunctionsStub  # 2.2 Функции обратного вызова
from QUIKgRPC.Python.grpc.callback_pb2 import CallbackRequest, CallbackResponse  # Команды подписки / отмены подписки, события


class QUIKgRPCPy:
    """Работа с сервером QUIKgRPC из Python
    Генерация кода в папку grpc осуществлена из proto контрактов: QUIKgRPC/protos
    """
    tz_msk = timezone('Europe/Moscow')  # QUIK работает по московскому времени
    currency = 'SUR'  # Суммы будем получать в российских рублях
    futures_firm_id = 'SPBFUT'  # Код фирмы для срочного рынка. Если ваш брокер поставил другую фирму для срочного рынка, то измените ее
    logger = logging.getLogger('QUIKgRPCPy')  # Будем вести лог

    def __init__(self, host='127.0.0.1', port=7222):
        """Инициализация

        :param str host: IP адрес или название хоста
        :param int port: Порт сервера QUIKgRPC
        """
        # 2.2 Функции обратного вызова
        self.on_firm = self.default_handler  # 2.2.1 Новая фирма
        self.on_all_trade = self.default_handler  # 2.2.2 Новая обезличенная сделка
        self.on_trade = self.default_handler  # 2.2.3 Новая сделка / Изменение существующей сделки
        self.on_order = self.default_handler  # 2.2.4 Новая заявка / Изменение существующей заявки
        self.on_account_balance = self.default_handler  # 2.2.5 Изменение текущей позиции по счету
        self.on_futures_limit_change = self.default_handler  # 2.2.6 Изменение ограничений по срочному рынку
        self.on_futures_limit_delete = self.default_handler  # 2.2.7 Удаление ограничений по срочному рынку
        self.on_futures_client_holding = self.default_handler  # 2.2.8 Изменение позиции по срочному рынку
        self.on_money_limit = self.default_handler  # 2.2.9 Изменение денежной позиции
        self.on_money_limit_delete = self.default_handler  # 2.2.10 Удаление денежной позиции
        self.on_depo_limit = self.default_handler  # 2.2.11 Изменение позиций по инструментам
        self.on_depo_limit_delete = self.default_handler  # 2.2.12 Удаление позиции по инструментам
        self.on_account_position = self.default_handler  # 2.2.13 Изменение денежных средств
        # on_neg_deal - 2.2.14 Новая внебиржевая заявка / Изменение существующей внебиржевой заявки
        # on_neg_trade - 2.2.15 Новая внебиржевая сделка / Изменение существующей внебиржевой сделки
        self.on_stop_order = self.default_handler  # 2.2.16 Новая стоп заявка / Изменение существующей стоп заявки
        self.on_trans_reply = self.default_handler  # 2.2.17 Ответ на транзакцию пользователя
        self.on_param = self.default_handler  # 2.2.18 Изменение текущих параметров
        self.on_quote = self.default_handler  # 2.2.19 Изменение стакана котировок
        self.on_disconnected = self.default_handler  # 2.2.20 Отключение терминала от сервера QUIK
        self.on_connected = self.default_handler  # 2.2.21 Соединение терминала с сервером QUIK
        # on_clean_up - 2.2.22 Смена сервера QUIK / Пользователя / Сессии
        self.on_close = self.default_handler  # 2.2.23 Закрытие терминала QUIK
        self.on_stop = self.default_handler  # 2.2.24 Остановка LUA скрипта в терминале QUIK / закрытие терминала QUIK
        self.on_init = self.default_handler  # 2.2.25 Запуск LUA скрипта в терминале QUIK
        # on_main - 2.2.26 Функция, реализующая основной поток выполнения в скрипте
        # Функции обратного вызова QUIK#
        self.on_new_candle = self.default_handler  # Новая свечка
        self.on_error = self.default_handler  # Сообщение об ошибке

        self.host = host  # IP адрес или название хоста
        self.port = port  # Порт сервера QUIKgRPC
        self.channel = insecure_channel(f'{self.host}:{self.port}')  # Незащищенный канал

        self.service_stub = ServiceFunctionsStub(self.channel)  # Фукнции отладки QUIK# / 2.1 Сервисные функции
        self.interaction_stub = InteractionFunctionsStub(self.channel)  # 3. Функции взаимодействия скрипта Lua и Рабочего места QUIK
        self.callback_stub = CallbackFunctionsStub(self.channel)  # Функции обратного вызова
        self.callback_requests_queue: SimpleQueue[CallbackRequest] = SimpleQueue()  # Очередь команд подписки / отмены подписки
        self.callback_thread = Thread(target=self.callback_handler, name='CallbackThread')  # Создаем поток событий функций обратного вызова
        self.callback_thread.start()  # Запускаем поток

        self.accounts = list()  # Счета
        money_limits: MoneyLimits = self.call_function(self.interaction_stub.GetMoneyLimits, Empty())  # Все денежные лимиты (остатки на счетах)
        trade_accounts: TradeAccounts = self.call_function(self.interaction_stub.GetTradeAccounts, Empty())  # Все торговые счета
        for account in trade_accounts.trade_account:  # Пробегаемся по всем торговым счетам
            firm_id = account.firmid  # Фирма
            client_code = next((moneyLimit.client_code for moneyLimit in money_limits.money_limit if moneyLimit.firmid == firm_id), None)  # Код клиента
            class_codes: list[str] = account.class_codes[1:-1].split('|')  # Список режимов торгов счета. Убираем первую и последнюю вертикальную черту, разбиваем по вертикальной черте
            self.accounts.append(dict(  # Добавляем торговый счет
                client_code=client_code, firm_id=firm_id, trade_account_id=account.trdaccid,  # Код клиента / Фирма / Счет
                class_codes=class_codes, futures=(firm_id == self.futures_firm_id)))  # Режимы торгов / Счет срочного рынка

    def __enter__(self):
        """Вход в класс, например, с with"""
        return self

    # Запросы

    def call_function(self, func, request):
        """Вызов функции"""
        response, _ = func.with_call(request=request)  # Вызываем функцию
        # self.logger.debug(f'call_function: Запрос: {request} Ответ: {response}')  # Для отладки
        return response  # и возвращаем ответ

    # Подписки (функции обратного вызова)

    def default_handler(self, data):
        """Пустой обработчик события по умолчанию. Его можно заменить на пользовательский"""
        pass

    def callback_handler(self):
        """Поток событий функций обратного вызова"""
        events = self.callback_stub.Callback(iter(self.callback_requests_queue.get, None))
        try:
            for event in events:  # Пробегаемся по событиям до закрытия канала
                e: CallbackResponse = event  # Приводим к событию
                self.logger.debug(f'callback_handler: Пришли данные подписки {e}')  # Для отладки
                # 2.2 Функции обратного вызова
                if e.HasField('firm'):
                    self.on_firm(e.firm)  # 2.2.1 Новая фирма
                elif e.HasField('all_trade'):
                    self.on_all_trade(e.all_trade)  # 2.2.2 Новая обезличенная сделка
                elif e.HasField('trade'):
                    self.on_trade(e.trade)  # 2.2.3 Новая сделка / Изменение существующей сделки
                elif e.HasField('order'):
                    self.on_order(e.order)  # 2.2.4 Новая заявка / Изменение существующей заявки
                elif e.HasField('account_balance'):
                    self.on_account_balance(e.account_balance)  # 2.2.5 Изменение текущей позиции по счету
                elif e.HasField('futures_limit_change'):
                    self.on_futures_limit_change(e.futures_limit_change)  # 2.2.6 Изменение ограничений по срочному рынку
                elif e.HasField('futures_limit_delete'):
                    self.on_futures_limit_delete(e.futures_limit_delete)  # 2.2.7 Удаление ограничений по срочному рынку
                elif e.HasField('futures_client_holding'):
                    self.on_futures_client_holding(e.futures_client_holding)  # 2.2.8 Изменение позиции по срочному рынку
                elif e.HasField('money_limit'):
                    self.on_money_limit(e.money_limit)  # 2.2.9 Изменение денежной позиции
                elif e.HasField('money_limit_delete'):
                    self.on_money_limit_delete(e.money_limit_delete)  # 2.2.10 Удаление денежной позиции
                elif e.HasField('depo_limit'):
                    self.on_depo_limit(e.depo_limit)  # 2.2.11 Изменение позиций по инструментам
                elif e.HasField('depo_limit_delete'):
                    self.on_depo_limit_delete(e.depo_limit_delete)  # 2.2.12 Удаление позиции по инструментам
                elif e.HasField('account_position'):
                    self.on_account_position(e.account_position)  # 2.2.13 Изменение денежных средств
                # on_neg_deal - 2.2.14 Новая внебиржевая заявка / Изменение существующей внебиржевой заявки
                # on_neg_trade - 2.2.15 Новая внебиржевая сделка / Изменение существующей внебиржевой сделки
                elif e.HasField('stop_order'):
                    self.on_stop_order(e.stop_order)  # 2.2.16 Новая стоп заявка / Изменение существующей стоп заявки
                elif e.HasField('trans_reply'):
                    self.on_trans_reply(e.trans_reply)  # 2.2.17 Ответ на транзакцию пользователя
                elif e.HasField('param'):
                    self.on_param(e.param)  # 2.2.18 Изменение текущих параметров
                elif e.HasField('quote'):
                    self.on_quote(e.quote)  # 2.2.19 Изменение стакана котировок
                elif e.HasField('disconnected'):
                    self.on_disconnected(e.disconnected)  # 2.2.20 Отключение терминала от сервера QUIK
                elif e.HasField('connected'):
                    self.on_connected(e.connected)  # 2.2.21 Соединение терминала с сервером QUIK
                # on_clean_up - 2.2.22 Смена сервера QUIK / Пользователя / Сессии
                elif e.HasField('close'):
                    self.on_close(e.close)  # 2.2.23 Закрытие терминала QUIK
                elif e.HasField('stop'):
                    self.on_stop(e.stop)  # 2.2.24 Остановка LUA скрипта в терминале QUIK / закрытие терминала QUIK
                elif e.HasField('init'):
                    self.on_init(e.init)  # 2.2.25 Запуск LUA скрипта в терминале QUIK
                # on_main - 2.2.26 Функция, реализующая основной поток выполнения в скрипте
                # Функции обратного вызова QUIK#
                elif e.HasField('candle'):
                    self.on_new_candle(e.candle)  # Новая свечка
                elif e.HasField('error'):
                    self.on_error(e.error)  # Сообщение об ошибке
        except RpcError:  # При закрытии канала попадем на эту ошибку (grpc._channel._MultiThreadedRendezvous)
            pass  # Все в порядке, ничего делать не нужно

    # Выход и закрытие

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close_channel()

    def __del__(self):
        self.close_channel()

    def close_channel(self):
        """Закрытие канала"""
        self.channel.close()

    # Функции конвертации

    def dataname_to_class_sec_codes(self, dataname) -> Union[tuple[str, str], None]:
        """Код режима торгов и тикер из названия тикера

        :param str dataname: Название тикера
        :return: Код режима торгов и тикер
        """
        symbol_parts = dataname.split('.')  # По разделителю пытаемся разбить тикер на части
        if len(symbol_parts) >= 2:  # Если тикер задан в формате <Код режима торгов>.<Код тикера>
            class_code = symbol_parts[0]  # Код режима торгов
            sec_code = '.'.join(symbol_parts[1:])  # Код тикера
        else:  # Если тикер задан без кода режима торгов
            sec_code = dataname  # Код тикера
            class_codes = self.interaction_stub.GetClassesList(Empty()).value  # Все режимы торгов
            class_code = self.interaction_stub.GetSecurityClass(SecurityClassRequest(classes_list=class_codes, sec_code=sec_code)).value  # Код режима торгов из всех режимов по тикеру
        return class_code, sec_code

    @staticmethod
    def class_sec_codes_to_dataname(class_code, sec_code):
        """Название тикера из кода режима торгов и кода тикера

        :param str class_code: Код режима торгов
        :param str sec_code: Код тикера
        :return: Название тикера
        """
        return f'{class_code}.{sec_code}'

    @staticmethod
    def timeframe_to_quik_timeframe(tf) -> tuple[int, bool]:
        """Перевод временнОго интервала во временной интервал QUIK

        :param str tf: Временной интервал https://ru.wikipedia.org/wiki/Таймфрейм
        :return: Временной интервал QUIK, внутридневной интервал
        """
        if 'MN' in tf:  # Месячный временной интервал
            return 23200, False
        if tf[0:1] == 'W':  # Недельный временной интервал
            return 10080, False
        if tf[0:1] == 'D':  # Дневной временной интервал
            return 1440, False
        if tf[0:1] == 'M':  # Минутный временной интервал
            minutes = int(tf[1:])  # Кол-во минут
            if minutes in (1, 2, 3, 4, 5, 6, 10, 15, 20, 30, 60, 120, 240):  # Разрешенные временнЫе интервалы в QUIK
                return minutes, True
        raise NotImplementedError  # С остальными временнЫми интервалами не работаем, в т.ч. и с тиками (интервал = 0)

    @staticmethod
    def quik_timeframe_to_timeframe(tf) -> tuple[str, bool]:
        """Перевод временнОго интервала QUIK во временной интервал

        :param int tf: Временной интервал QUIK
        :return: Временной интервал https://ru.wikipedia.org/wiki/Таймфрейм, внутридневной интервал
        """
        if tf == 23200:  # Месячный временной интервал
            return 'MN1', False
        if tf == 10080:  # Недельный временной интервал
            return 'W1', False
        if tf == 1440:  # Дневной временной интервал
            return 'D1', False
        if tf in (1, 2, 3, 4, 5, 6, 10, 15, 20, 30, 60, 120, 240):  # Минутный временной интервал
            return f'M{tf}', True
        raise NotImplementedError  # С остальными временнЫми интервалами не работаем , в т.ч. и с тиками (интервал = 0)

    def price_to_quik_price(self, class_code, sec_code, price) -> Union[int, float]:
        """Перевод цены в цену QUIK

        :param str class_code: Код режима торгов
        :param str sec_code: Тикер
        :param float price: Цена
        :return: Цена в QUIK
        """
        si: Security = self.interaction_stub.GetSecurityInfo(ClassSecCode(class_code=class_code, sec_code=sec_code))  # Информация о тикере
        if not si:  # Если тикер не найден
            return price  # то цена не изменяется
        min_step = si.min_price_step  # Шаг цены
        if class_code in ('TQOB', 'TQCB', 'TQRD', 'TQIR'):  # Для облигаций (Т+ Гособлигации, Т+ Облигации, Т+ Облигации Д, Т+ Облигации ПИР)
            quik_price = price * 100 / si.face_value  # Пункты цены для котировок облигаций представляют собой проценты номинала облигации
        elif class_code == 'SPBFUT':  # Для рынка фьючерсов
            quik_price = price * int(si.lot_size) if int(si.lot_size) > 0 else price  # Умножаем на размер лота
        else:  # В остальных случаях
            quik_price = price  # Цена не изменяется
        quik_price = round(quik_price // min_step * min_step, si['scale'])  # Округляем цену кратно шага цены
        return int(quik_price) if quik_price.is_integer() else quik_price  # Целое значение мы должны отправлять без десятичных знаков. Поэтому, если возможно, приводим цену к целому числу

    def quik_price_to_price(self, class_code, sec_code, quik_price) -> float:
        """Перевод цены QUIK в цену

        :param str class_code: Код режима торгов
        :param str sec_code: Тикер
        :param float quik_price: Цена в QUIK
        :return: Цена
        """
        si: Security = self.interaction_stub.GetSecurityInfo(ClassSecCode(class_code=class_code, sec_code=sec_code))  # Информация о тикере
        if not si:  # Если тикер не найден
            return quik_price  # то цена не изменяется
        min_step = si.min_price_step  # Шаг цены
        quik_price = quik_price // min_step * min_step  # Цена кратная шагу цены
        if class_code in ('TQOB', 'TQCB', 'TQRD', 'TQIR'):  # Для облигаций (Т+ Гособлигации, Т+ Облигации, Т+ Облигации Д, Т+ Облигации ПИР)
            price = quik_price / 100 * si.face_value  # Пункты цены для котировок облигаций представляют собой проценты номинала облигации
        elif class_code == 'SPBFUT':  # Для рынка фьючерсов
            price = quik_price / int(si.lot_size) if int(si.lot_size) > 0 else quik_price  # Делим на размер лота
        else:  # В остальных случаях
            price = quik_price  # Цена не изменяется
        return round(price, si.scale)  # Округляем цену до кол-ва десятичных знаков тикера
