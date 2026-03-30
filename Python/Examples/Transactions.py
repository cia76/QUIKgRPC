import logging  # Выводим лог на консоль и в файл
from datetime import datetime  # Дата и время
from time import sleep  # Задержка в секундах перед выполнением операций
import itertools  # Итератор для уникальных номеров транзакций

from QUIKgRPC.Python.QUIKgRPCPy import QUIKgRPCPy  # Работа с сервером QUIKgRPC из Python
from QUIKgRPC.Python.grpc.callback_pb2 import CallbackRequest
from QUIKgRPC.Python.grpc.interaction_pb2 import ParamExRequest
from QUIKgRPC.Python.grpc.structures_pb2 import ClassSecCode, QuikString, TransReply

if __name__ == '__main__':  # Точка входа при запуске этого скрипта
    logger = logging.getLogger('QUIKgRPC.Transactions')  # Будем вести лог
    qgp_provider = QUIKgRPCPy()  # Подключение к локальному серверу QUIKgRPC по порту по умолчанию

    logging.basicConfig(format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',  # Формат сообщения
                        datefmt='%d.%m.%Y %H:%M:%S',  # Формат даты
                        level=logging.DEBUG,  # Уровень логируемых событий NOTSET/DEBUG/INFO/WARNING/ERROR/CRITICAL
                        handlers=[logging.FileHandler('Transactions.log', encoding='utf-8'), logging.StreamHandler()])  # Лог записываем в файл и выводим на консоль
    logging.Formatter.converter = lambda *args: datetime.now(tz=qgp_provider.tz_msk).timetuple()  # В логе время указываем по МСК

    account = qgp_provider.accounts[1]  # Счет фондового рынка
    class_code = 'TQBR'  # Класс тикера
    sec_code = 'SBER'  # Тикер
    quantity = 1  # Кол-во в лотах

    client_code = account['client_code'] if account['client_code'] else ''  # Для фьючерсов кода клиента нет
    trade_account_id = account['trade_account_id']  # Счет
    last_price = float(qgp_provider.interaction_stub.GetParamEx(ParamExRequest(class_sec_code=ClassSecCode(class_code=class_code, sec_code=sec_code), param_name='LAST')).param_value)  # Последняя цена сделки
    market_price = qgp_provider.price_to_quik_price(class_code, sec_code, last_price * 1.01) if account['futures'] else 0  # Цена исполнения по рынку. Для фьючерсных заявок цена больше последней при покупке и меньше последней при продаже. Для остальных заявок цена = 0
    order_num = 0  # 19-и значный номер заявки на бирже / номер стоп заявки на сервере. Будет устанавливаться в обработчике события ответа на транзакцию пользователя
    trans_id = itertools.count(1)  # Номер транзакции задается пользователем. Он будет начинаться с 1 и каждый раз увеличиваться на 1

    # Обработчики подписок
    def on_trans_reply(data: TransReply):
        """Обработчик события ответа на транзакцию пользователя"""
        logger.info(f'OnTransReply: {data}')
        global order_num
        order_num = int(data.order_num)  # Номер заявки на бирже
        logger.info(f'Номер транзакции: {data.trans_id}, Номер заявки: {order_num}')

    qgp_provider.on_trans_reply = on_trans_reply  # Ответ на транзакцию пользователя. Если транзакция выполняется из QUIK, то не вызывается
    qgp_provider.callback_requests_queue.put(CallbackRequest(trans_reply=True))  # Подписываемся на транзакции
    qgp_provider.on_order = lambda data: logger.info(f'OnOrder: {data}')  # Получение новой / изменение существующей заявки
    qgp_provider.callback_requests_queue.put(CallbackRequest(order=True))  # Подписываемся на заявки
    qgp_provider.on_stop_order = lambda data: logger.info(f'OnStopOrder: {data}')  # Получение новой / изменение существующей стоп заявки
    qgp_provider.callback_requests_queue.put(CallbackRequest(stop_order=True))  # Подписываемся на стоп заявки
    # qgp_provider.on_trade = lambda data: logger.info(f'OnTrade: {data}')  # Получение новой / изменение существующей сделки
    # qgp_provider.callback_requests_queue.put(CallbackRequest(trade=True))  # Подписываемся на сделки
    # qgp_provider.on_futures_client_holding = lambda data: logger.info(f'OnFuturesClientHolding: {data}')  # Изменение позиции по срочному рынку
    # qgp_provider.callback_requests_queue.put(CallbackRequest(futures_client_holding=True))  # Подписываемся на позиции по срочному рынку
    # qgp_provider.on_depo_limit = lambda data: logger.info(f'OnDepoLimit: {data}')  # Изменение позиции по инструментам
    # qgp_provider.callback_requests_queue.put(CallbackRequest(depo_limit=True))  # Подписываемся на позиции по инструментам
    # qgp_provider.on_depo_limit_delete = lambda data: logger.info(f'OnDepoLimitDelete: {data}')  # Удаление позиции по инструментам
    # qgp_provider.callback_requests_queue.put(CallbackRequest(trans_reply=True))  # Подписываемся на удаление позиции по инструментам

    # Новая рыночная заявка (открытие позиции)
    # logger.info(f'Заявка {class_code}.{sec_code} на покупку минимального лота по рыночной цене')
    # transaction = {  # Все значения должны передаваться в виде строк
    #     'TRANS_ID': str(next(trans_id)),  # Следующий номер транзакции
    #     'CLIENT_CODE': client_code,  # Код клиента
    #     'ACCOUNT': trade_account_id,  # Счет
    #     'ACTION': 'NEW_ORDER',  # Тип заявки: Новая лимитная/рыночная заявка
    #     'CLASSCODE': class_code,  # Код режима торгов
    #     'SECCODE': sec_code,  # Код тикера
    #     'OPERATION': 'B',  # B = покупка, S = продажа
    #     'PRICE': str(market_price),  # Цена исполнения по рынку
    #     'QUANTITY': str(quantity),  # Кол-во в лотах
    #     'TYPE': 'M'}  # L = лимитная заявка (по умолчанию), M = рыночная заявка
    # logger.info(f'Заявка отправлена на рынок: {qgp_provider.interaction_stub.SendTransaction(QuikString(value=str(transaction))).value}')
    #
    # sleep(10)  # Ждем 10 секунд

    # Новая рыночная заявка (закрытие позиции)
    # logger.info(f'Заявка {class_code}.{sec_code} на продажу минимального лота по рыночной цене')
    # transaction = {  # Все значения должны передаваться в виде строк
    #     'TRANS_ID': str(next(trans_id)),  # Следующий номер транзакции
    #     'CLIENT_CODE': client_code,  # Код клиента
    #     'ACCOUNT': trade_account_id,  # Счет
    #     'ACTION': 'NEW_ORDER',  # Тип заявки: Новая лимитная/рыночная заявка
    #     'CLASSCODE': class_code,  # Код режима торгов
    #     'SECCODE': sec_code,  # Код тикера
    #     'OPERATION': 'S',  # B = покупка, S = продажа
    #     'PRICE': str(market_price),  # Цена исполнения по рынку
    #     'QUANTITY': str(quantity),  # Кол-во в лотах
    #     'TYPE': 'M'}  # L = лимитная заявка (по умолчанию), M = рыночная заявка
    # logger.info(f'Заявка отправлена на рынок: {qgp_provider.interaction_stub.SendTransaction(QuikString(value=str(transaction))).value}')
    #
    # sleep(10)  # Ждем 10 секунд

    # Новая лимитная заявка
    limit_price = qgp_provider.price_to_quik_price(class_code, sec_code, last_price * 0.99)  # Лимитная цена на 1% ниже последней цены сделки
    logger.info(f'Заявка {class_code}.{sec_code} на покупку минимального лота по лимитной цене {limit_price}')
    transaction = {  # Все значения должны передаваться в виде строк
        'TRANS_ID': str(next(trans_id)),  # Следующий номер транзакции
        'CLIENT_CODE': client_code,  # Код клиента
        'ACCOUNT': trade_account_id,  # Счет
        'ACTION': 'NEW_ORDER',  # Тип заявки: Новая лимитная/рыночная заявка
        'CLASSCODE': class_code,  # Код режима торгов
        'SECCODE': sec_code,  # Код тикера
        'OPERATION': 'B',  # B = покупка, S = продажа
        'PRICE': str(limit_price),  # Цена исполнения
        'QUANTITY': str(quantity),  # Кол-во в лотах
        'TYPE': 'L'}  # L = лимитная заявка (по умолчанию), M = рыночная заявка
    logger.info(f'Заявка отправлена в стакан: {qgp_provider.interaction_stub.SendTransaction(QuikString(value=str(transaction))).value}')

    sleep(10)  # Ждем 10 секунд

    # Удаление существующей лимитной заявки
    transaction = {  # Все значения должны передаваться в виде строк
        'TRANS_ID': str(next(trans_id)),  # Следующий номер транзакции
        'ACTION': 'KILL_ORDER',  # Тип заявки: Удаление существующей заявки
        'CLASSCODE': class_code,  # Код режима торгов
        'SECCODE': sec_code,  # Код тикера
        'ORDER_KEY': str(order_num)}  # Номер заявки
    logger.info(f'Удаление заявки {order_num} из стакана: {qgp_provider.interaction_stub.SendTransaction(QuikString(value=str(transaction))).value}')

    sleep(10)  # Ждем 10 секунд

    # Новая стоп заявка
    stop_price = qgp_provider.price_to_quik_price(class_code, sec_code, last_price * 1.01)  # Стоп цена на 1% выше последней цены сделки
    transaction = {  # Все значения должны передаваться в виде строк
        'TRANS_ID': str(next(trans_id)),  # Следующий номер транзакции
        'CLIENT_CODE': client_code,  # Код клиента
        'ACCOUNT': trade_account_id,  # Счет
        'ACTION': 'NEW_STOP_ORDER',  # Тип заявки: Новая стоп заявка
        'CLASSCODE': class_code,  # Код режима торгов
        'SECCODE': sec_code,  # Код тикера
        'OPERATION': 'B',  # B = покупка, S = продажа
        'PRICE': str(last_price),  # Цена исполнения
        'QUANTITY': str(quantity),  # Кол-во в лотах
        'STOPPRICE': str(stop_price),  # Стоп цена исполнения
        'EXPIRY_DATE': 'GTC'}  # Срок действия до отмены
    logger.info(f'Стоп заявка отправлена на сервер: {qgp_provider.interaction_stub.SendTransaction(QuikString(value=str(transaction))).value}')

    sleep(10)  # Ждем 10 секунд

    # Удаление существующей стоп заявки
    transaction = {
        'TRANS_ID': str(next(trans_id)),  # Следующий номер транзакции
        'ACTION': 'KILL_STOP_ORDER',  # Тип заявки: Удаление существующей заявки
        'CLASSCODE': class_code,  # Код режима торгов
        'SECCODE': sec_code,  # Код тикера
        'STOP_ORDER_KEY': str(order_num)}  # Номер заявки
    print(f'Удаление стоп заявки с сервера: {qgp_provider.interaction_stub.SendTransaction(QuikString(value=str(transaction))).value}')

    sleep(10)  # Ждем 10 секунд

    qgp_provider.close_channel()  # Закрываем канал перед выходом
