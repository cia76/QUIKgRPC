import logging  # Выводим лог на консоль и в файл
from datetime import datetime  # Дата и время
import time  # Подписка на события по времени

from QUIKgRPC.Python.QUIKgRPCPy import QUIKgRPCPy  # Работа с сервером QUIKgRPC из Python
from QUIKgRPC.Python.grpc.callback_pb2 import CallbackRequest, ClassSecCodeSubscribtion, CandleSubscribtion
from QUIKgRPC.Python.grpc.structures_pb2 import ClassSecCode, TimeFrame


if __name__ == '__main__':  # Точка входа при запуске этого скрипта
    logger = logging.getLogger('QUIKgRPC.Stream')  # Будем вести лог
    qgp_provider = QUIKgRPCPy()  # Подключение к локальному серверу QUIKgRPC по порту по умолчанию

    logging.basicConfig(format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',  # Формат сообщения
                        datefmt='%d.%m.%Y %H:%M:%S',  # Формат даты
                        level=logging.DEBUG,  # Уровень логируемых событий NOTSET/DEBUG/INFO/WARNING/ERROR/CRITICAL
                        handlers=[logging.FileHandler('Stream.log', encoding='utf-8'), logging.StreamHandler()])  # Лог записываем в файл и выводим на консоль
    logging.Formatter.converter = lambda *args: datetime.now(tz=qgp_provider.tz_msk).timetuple()  # В логе время указываем по МСК

    class_code = 'TQBR'  # Класс тикера
    sec_code = 'SBER'  # Тикер

    # class_code = 'SPBFUT'  # Класс тикера
    # sec_code = 'SiU4'  # Для фьючерсов: <Код тикера><Месяц экспирации: 3-H, 6-M, 9-U, 12-Z><Последняя цифра года>

    # Запрос текущего стакана. Чтобы получать, в QUIK открыть таблицу "Котировки", указать тикер
    # logger.info(f'Текущий стакан {class_code}.{sec_code}: {qgp_provider.interaction_stub.GetQuoteLevel2(ClassSecCode(class_code=class_code, sec_code=sec_code))}')

    # Подписка на стакан. Чтобы отмена подписки работала корректно, в QUIK должна быть ЗАКРЫТА таблица "Котировки" тикера
    qgp_provider.on_quote = lambda data: logger.info(data)  # Обработчик изменения стакана котировок
    qgp_provider.callback_requests_queue.put(CallbackRequest(
        quote=ClassSecCodeSubscribtion(class_sec_code=ClassSecCode(class_code=class_code, sec_code=sec_code), subscribe=True)))  # Подписываемся на стакан
    logger.info(f'Подписка на изменения стакана {class_code}.{sec_code}')
    sleep_sec = 3  # Кол-во секунд получения котировок
    logger.info(f'Секунд котировок: {sleep_sec}')
    time.sleep(sleep_sec)  # Ждем кол-во секунд получения котировок
    qgp_provider.callback_requests_queue.put(CallbackRequest(
        quote=ClassSecCodeSubscribtion(class_sec_code=ClassSecCode(class_code=class_code, sec_code=sec_code), subscribe=False)))  # Отменяем подписку на стакан
    logger.info(f'Отмена подписки на изменения стакана')
    qgp_provider.on_quote = qgp_provider.default_handler  # Возвращаем обработчик по умолчанию

    # Подписка на обезличенные сделки. Чтобы получать, в QUIK открыть "Таблицу обезличенных сделок", указать тикер
    qgp_provider.on_all_trade = lambda data: logger.info(data)  # Обработчик получения обезличенной сделки
    qgp_provider.callback_requests_queue.put(CallbackRequest(
        all_trade=ClassSecCodeSubscribtion(class_sec_code=ClassSecCode(class_code=class_code, sec_code=sec_code), subscribe=True)))  # Подписываемся на обезличенные сделки
    logger.info(f'Подписка на обезличенные сделки {class_code}.{sec_code}')
    sleep_sec = 3  # Кол-во секунд получения обезличенных сделок
    logger.info(f'Секунд обезличенных сделок: {sleep_sec}')
    time.sleep(sleep_sec)  # Ждем кол-во секунд получения обезличенных сделок
    qgp_provider.callback_requests_queue.put(CallbackRequest(
        all_trade=ClassSecCodeSubscribtion(class_sec_code=ClassSecCode(class_code=class_code, sec_code=sec_code), subscribe=False)))  # Отменяем подписку на обезличенные сделки
    logger.info(f'Отмена подписки на обезличенные сделки')
    qgp_provider.on_all_trade = qgp_provider.default_handler  # Возвращаем обработчик по умолчанию

    # Просмотр изменений состояния соединения терминала QUIK с сервером брокера
    qgp_provider.on_connected = lambda data: logger.info(data)  # Нажимаем кнопку "Установить соединение" в QUIK
    qgp_provider.on_disconnected = lambda data: logger.info(data)  # Нажимаем кнопку "Разорвать соединение" в QUIK

    # Подписка на новые свечки. При первой подписке получим все свечки с начала прошлой сессии
    qgp_provider.on_new_candle = lambda data: logger.info(data)  # Обработчик получения новой свечки
    for interval in (TimeFrame.M1,):  # (TimeFrame.M1, TimeFrame.M60, TimeFrame.D1) = Минутки, часовки, дневки
        qgp_provider.callback_requests_queue.put(CallbackRequest(
            candle=CandleSubscribtion(class_sec_code=ClassSecCode(class_code=class_code, sec_code=sec_code), timeframe=interval, subscribe=True)))  # Подписываемся на новые свечки
        logger.info(f'Подписка на свечи интервал {interval}: {class_code}.{sec_code}')
    input('Enter - отмена\n')
    for interval in (TimeFrame.M1,):  # (TimeFrame.M1, TimeFrame.M60, TimeFrame.D1) = Минутки, часовки, дневки
        qgp_provider.callback_requests_queue.put(CallbackRequest(
            candle=CandleSubscribtion(class_sec_code=ClassSecCode(class_code=class_code, sec_code=sec_code), timeframe=interval, subscribe=False)))  # Отменяем подписку на новые свечки
        logger.info(f'Отмена подписки на свечи интервал {interval}')

    # Выход
    qgp_provider.close_channel()  # Закрываем канал перед выходом
