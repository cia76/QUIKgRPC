import logging  # Выводим лог на консоль и в файл
import sys  # Выход из точки входа
from datetime import datetime  # Дата и время

from QUIKgRPC.Python.QUIKgRPCPy import QUIKgRPCPy  # Работа с сервером QUIKgRPC из Python
from QUIKgRPC.Python.grpc.callback_pb2 import CallbackRequest
from QUIKgRPC.Python.grpc.service_pb2 import MessageRequest, MessageIconType, InfoParamRequest, InfoParamName
from QUIKgRPC.Python.grpc.structures_pb2 import Empty, QuikString


if __name__ == '__main__':  # Точка входа при запуске этого скрипта
    logger = logging.getLogger('QUIKgRPC.Connect')  # Будем вести лог
    qgp_provider = QUIKgRPCPy()  # Подключение к локальному серверу QUIKgRPC по порту по умолчанию
    # qgp_provider = QUIKgRPCPy(host='<Адрес IP>')  # Подключение к удаленному QUIK по портам по умолчанию
    # qgp_provider = QUIKgRPCPy(host='<Адрес IP>', port='<Порт>')  # Подключение к удаленному QUIK по другому порту

    logging.basicConfig(format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',  # Формат сообщения
                        datefmt='%d.%m.%Y %H:%M:%S',  # Формат даты
                        level=logging.DEBUG,  # Уровень логируемых событий NOTSET/DEBUG/INFO/WARNING/ERROR/CRITICAL
                        handlers=[logging.FileHandler('Connect.log'), logging.StreamHandler()])  # Лог записываем в файл и выводим на консоль
    logging.Formatter.converter = lambda *args: datetime.now(tz=qgp_provider.tz_msk).timetuple()  # В логе время указываем по МСК

    # Проверяем соединение с терминалом QUIK
    is_connected = qgp_provider.service_stub.IsConnected(Empty()).value  # Состояние подключения терминала к серверу QUIK
    logger.info(f'Терминал QUIK подключен к серверу: {is_connected}')
    logger.info(f'Отклик QUIK на команду Ping: {qgp_provider.service_stub.Ping(QuikString(value="Ping")).value}')  # Проверка работы скрипта QuikSharp. Должен вернуть Pong
    msg = 'Hello from Python!'
    logger.info(f'Отправка сообщения в QUIK: {msg}{qgp_provider.service_stub.Message(MessageRequest(message=msg, icon_type=MessageIconType.INFO))}')  # Проверка работы QUIK. Сообщение в QUIK должно показаться как информационное
    if not is_connected:  # Если нет подключения терминала QUIK к серверу
        qgp_provider.close_channel()  # Закрываем канал перед выходом
        sys.exit()  # Выходим, дальше не продолжаем

    # Проверяем работу запрос/ответ
    trade_date = qgp_provider.service_stub.GetInfoParam(InfoParamRequest(param_name=InfoParamName.TRADEDATE)).value  # Дата на сервере в виде строки dd.mm.yyyy
    server_time = qgp_provider.service_stub.GetInfoParam(InfoParamRequest(param_name=InfoParamName.SERVERTIME)).value  # Время на сервере в виде строки hh:mi:ss
    dt = datetime.strptime(f'{trade_date} {server_time}', '%d.%m.%Y %H:%M:%S')  # Переводим строки в дату и время
    logger.info(f'Дата и время на сервере: {dt}')

    # Проверяем работу подписок
    qgp_provider.on_connected = lambda data: logger.info(data)  # Нажимаем кнопку "Установить соединение" в QUIK
    qgp_provider.on_disconnected = lambda data: logger.info(data)  # Нажимаем кнопку "Разорвать соединение" в QUIK
    qgp_provider.on_param = lambda data: logger.info(data)  # Текущие параметры изменяются постоянно. Будем их смотреть, пока не нажмем Enter в консоли
    qgp_provider.callback_requests_queue.put(CallbackRequest(connected=True))  # Подписываемся на соединение терминала с сервером QUIK
    qgp_provider.callback_requests_queue.put(CallbackRequest(disconnected=True))  # Подписываемся на отключение терминала от сервера QUIK
    qgp_provider.callback_requests_queue.put(CallbackRequest(param=True))  # Подписываемся на изменение текущих параметров

    # Выход
    input('Enter - закрытие подписок\n')
    qgp_provider.callback_requests_queue.put(CallbackRequest(connected=False))  # Отменяем подписку на соединение терминала с сервером QUIK
    qgp_provider.callback_requests_queue.put(CallbackRequest(disconnected=False))  # Отменяем подписку на отключение терминала от сервера QUIK
    qgp_provider.callback_requests_queue.put(CallbackRequest(param=False))  # Отменяем подписку на изменение текущих параметров
    input('Enter - выход\n')
    qgp_provider.close_channel()  # Закрываем канал перед выходом
