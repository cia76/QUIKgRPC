import logging  # Выводим лог на консоль и в файл
from datetime import datetime  # Дата и время

from QUIKgRPC.Python.QUIKgRPCPy import QUIKgRPCPy  # Работа с сервером QUIKgRPC из Python
from QUIKgRPC.Python.grpc.interaction_pb2 import ParamExRequest
from QUIKgRPC.Python.grpc.structures_pb2 import ClassSecCode, QuikString, Security

if __name__ == '__main__':  # Точка входа при запуске этого скрипта
    logger = logging.getLogger('QUIKgRPC.Ticker')  # Будем вести лог
    qgp_provider = QUIKgRPCPy()  # Подключение к локальному серверу QUIKgRPC по порту по умолчанию

    logging.basicConfig(format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',  # Формат сообщения
                        datefmt='%d.%m.%Y %H:%M:%S',  # Формат даты
                        level=logging.DEBUG,  # Уровень логируемых событий NOTSET/DEBUG/INFO/WARNING/ERROR/CRITICAL
                        handlers=[logging.FileHandler('Ticker.log', encoding='utf-8'), logging.StreamHandler()])  # Лог записываем в файл и выводим на консоль
    logging.Formatter.converter = lambda *args: datetime.now(tz=qgp_provider.tz_msk).timetuple()  # В логе время указываем по МСК

    # Формат короткого имени для фьючерсов: <Код тикера><Месяц экспирации: 3-H, 6-M, 9-U, 12-Z><Последняя цифра года>. Пример: SiU3, RIU3
    # datanames = ('SBER',)  # Тикер без режима торгов
    datanames = ('TQBR.SBER', 'TQBR.VTBR', 'SPBFUT.SiU4', 'SPBFUT.RIU4')  # Кортеж тикеров

    for dataname in datanames:  # Пробегаемся по всем тикерам
        class_code, sec_code = qgp_provider.dataname_to_class_sec_codes(dataname)  # Код режима торгов и тикер
        si: Security = qgp_provider.interaction_stub.GetSecurityInfo(ClassSecCode(class_code=class_code, sec_code=sec_code))  # Получаем информацию о тикере
        logger.debug(f'Ответ от сервера: {si}')
        logger.info(f'Информация о тикере {si.class_code}.{si.code} ({si.short_name}):')  # Короткое наименование инструмента
        logger.info(f'- Валюта: {si.face_unit}')
        logger.info(f'- Лот: {si.lot_size}')
        logger.info(f'- Шаг цены: {round(si.min_price_step, si.scale)}')
        logger.info(f'- Кол-во десятичных знаков: {si.scale}')
        trade_account = qgp_provider.interaction_stub.GetTradeAccount(QuikString(value=class_code)).value  # Торговый счет для класса тикера
        logger.info(f'- Торговый счет: {trade_account}')
        last_price = float(qgp_provider.interaction_stub.GetParamEx(ParamExRequest(class_sec_code=ClassSecCode(class_code=class_code, sec_code=sec_code), param_name='LAST')).param_value)  # Последняя цена сделки
        logger.info(f'- Последняя цена сделки: {last_price}')

    qgp_provider.close_channel()  # Закрываем канал перед выходом
