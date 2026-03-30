import logging  # Выводим лог на консоль и в файл
from datetime import datetime  # Дата и время

from QUIKgRPC.Python.QUIKgRPCPy import QUIKgRPCPy  # Работа с сервером QUIKgRPC из Python
from QUIKgRPC.Python.grpc.interaction_pb2 import FuturesLimitRequest, SecurityClassRequest, ParamExRequest
from QUIKgRPC.Python.grpc.structures_pb2 import Empty, QuikStrings, TradeAccounts, MoneyLimits, DepoLimits, Orders, \
    StopOrders, ClassSecCode, Security, FuturesLimit, QuikString

futures_firm_id = 'SPBFUT'  # Код фирмы для фьючерсов. Измените, если требуется, на фирму, которую для фьючерсов поставил ваш брокер


if __name__ == '__main__':  # Точка входа при запуске этого скрипта
    logger = logging.getLogger('QUIKgRPC.Accounts')  # Будем вести лог
    qgp_provider = QUIKgRPCPy()  # Подключение к локальному серверу QUIKgRPC по порту по умолчанию

    logging.basicConfig(format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',  # Формат сообщения
                        datefmt='%d.%m.%Y %H:%M:%S',  # Формат даты
                        level=logging.DEBUG,  # Уровень логируемых событий NOTSET/DEBUG/INFO/WARNING/ERROR/CRITICAL
                        handlers=[logging.FileHandler('Accounts.log', encoding='utf-8'), logging.StreamHandler()])  # Лог записываем в файл и выводим на консоль
    logging.Formatter.converter = lambda *args: datetime.now(tz=qgp_provider.tz_msk).timetuple()  # В логе время указываем по МСК

    class_codes: QuikStrings = qgp_provider.interaction_stub.GetClassesList(Empty())  # Все режимы торгов
    trade_accounts: TradeAccounts = qgp_provider.interaction_stub.GetTradeAccounts(Empty())  # Все торговые счета
    money_limits: MoneyLimits = qgp_provider.interaction_stub.GetMoneyLimits(Empty())  # Все денежные лимиты (остатки на счетах)
    depo_limits: DepoLimits = qgp_provider.interaction_stub.GetAllDepoLimits(Empty())  # Все лимиты по бумагам (позиции по инструментам)
    orders: Orders = qgp_provider.interaction_stub.GetAllOrders(Empty())  # Все заявки
    stop_orders: StopOrders = qgp_provider.interaction_stub.GetAllStopOrders(Empty())  # Все стоп заявки

    for trade_account in trade_accounts.trade_account:  # Пробегаемся по всем счетам (Коды клиента/Фирма/Счет)
        # trade_account_class_codes = trade_account.class_codes[1:-1].split('|')  # Режимы торгов счета. Удаляем первую и последнюю вертикальную черту, разбиваем значения по вертикальной черте
        # intersection_class_codes = list(set(trade_account_class_codes).intersection(class_codes.value))  # Режимы торгов, которые есть и в списке и в торговом счете
        # for class_code in intersection_class_codes:  # Пробегаемся по всем режимам торгов
        #     class_info: Class = qgp_provider.interaction_stub.GetClassInfo(QuikString(value=class_code))  # Информация о режиме торгов
        #     logger.info(f'- Режим торгов {class_code} ({class_info.name}), Тикеров {class_info.nsecs}')
        #     class_securities = qgp_provider.interaction_stub.GetClassSecurities(QuikStrings(value=(class_code,)))  # Список инструментов режима торгов. Удаляем последнюю запятую, разбиваем значения по запятой
        #     logger.info(f'  - Тикеры ({class_securities.value})')

        firm_id = trade_account.firmid  # Фирма
        trade_account_id = trade_account.trdaccid  # Счет
        client_code = next((moneyLimit.client_code for moneyLimit in money_limits.money_limit if moneyLimit.firmid == firm_id), None)  # Код клиента
        logger.info(f'Учетная запись: Код клиента {client_code if client_code else "не задан"}, Фирма {firm_id}, Счет {trade_account_id} ({trade_account.description})')
        if firm_id == futures_firm_id:  # Для фирмы фьючерсов
            active_futures_holdings = [futuresHolding for futuresHolding in qgp_provider.interaction_stub.GetFuturesHoldings(Empty()).futures_client_holding if futuresHolding.totalnet != 0]  # Активные фьючерсные позиции
            for active_futures_holding in active_futures_holdings:  # Пробегаемся по всем активным фьючерсным позициям
                si: Security = qgp_provider.interaction_stub.GetSecurityInfo(ClassSecCode(class_code='SPBFUT', sec_code=active_futures_holding.symbol))  # Информация о тикере
                logger.info(f'- Позиция {si.class_code}.{si.code} ({si.short_name}) {active_futures_holding.totalnet} @ {active_futures_holding.cbplused}')
            futures_limit: FuturesLimit = qgp_provider.interaction_stub.GetFuturesLimit(FuturesLimitRequest(firmid=firm_id, trdaccid=trade_account_id,limit_type=0, currcode="SUR"))
            value = futures_limit.cbplused  # Стоимость позиций
            cash = futures_limit.cbplimit  # Свободные средства
            logger.info(f'- Позиции {value:.2f} + Свободные средства {cash:.2f} = {(value + cash):.2f} {futures_limit.currcode}')
        else:  # Для остальных фирм
            firm_money_limits = [moneyLimit for moneyLimit in money_limits.money_limit if moneyLimit.firmid == firm_id]  # Денежные лимиты по фирме
            for firm_money_limit in firm_money_limits:  # Пробегаемся по всем денежным лимитам
                limit_kind = firm_money_limit.limit_kind  # День лимита
                firm_kind_depo_limits = [depoLimit for depoLimit in depo_limits.depo_limit if
                                         depoLimit.firmid == firm_id and
                                         depoLimit.limit_kind == limit_kind and
                                         depoLimit.currentbal != 0]  # Берем только открытые позиции по фирме и дню
                for firm_kind_depo_limit in firm_kind_depo_limits:  # Пробегаемся по всем позициям
                    sec_code = firm_kind_depo_limit.symbol  # Код тикера
                    class_code = qgp_provider.interaction_stub.GetSecurityClass(SecurityClassRequest(classes_list=class_codes.value, sec_code=sec_code)).value  # Код режима торгов из всех режимов по тикеру
                    entry_price = qgp_provider.quik_price_to_price(class_code, sec_code, float(firm_kind_depo_limit.wa_position_price))  # Цена входа
                    last_price = qgp_provider.quik_price_to_price(class_code, sec_code, float(qgp_provider.interaction_stub.GetParamEx(ParamExRequest(class_sec_code=ClassSecCode(class_code=class_code, sec_code=sec_code), param_name='LAST')).param_value))  # Последняя цена сделки
                    si: Security = qgp_provider.interaction_stub.GetSecurityInfo(ClassSecCode(class_code=class_code, sec_code=sec_code))  # Информация о тикере
                    logger.info(f'- Позиция {class_code}.{sec_code} ({si.short_name}) {int(firm_kind_depo_limit.currentbal)} @ {entry_price} / {last_price}')
                logger.info(f'- T{limit_kind}: Свободные средства {firm_money_limit.currentbal} {firm_money_limit.currcode}')
        firm_orders = [order for order in orders.order if order.firmid == firm_id and order.flags & 0b1 == 0b1]  # Активные заявки по фирме
        for firm_order in firm_orders:  # Пробегаемся по всем заявкам
            buy = firm_order.flags & 0b100 != 0b100  # Заявка на покупку
            class_code = firm_order.board  # Код режима торгов
            sec_code = firm_order.symbol  # Тикер
            order_price = qgp_provider.quik_price_to_price(class_code, sec_code, firm_order.price)  # Цена заявки
            si: Security = qgp_provider.interaction_stub.GetSecurityInfo(ClassSecCode(class_code=class_code, sec_code=sec_code))  # Информация о тикере
            order_qty = firm_order.qty * si.lot_size  # Кол-во в штуках
            logger.info(f'- Заявка номер {firm_order.order_num} {"Покупка" if buy else "Продажа"} {class_code}.{sec_code} {order_qty} @ {order_price}')
        firm_stop_orders = [stopOrder for stopOrder in stop_orders.stop_order if stopOrder.firmid == firm_id and stopOrder.flags & 0b1 == 0b1]  # Активные стоп заявки по фирме
        for firm_stop_order in firm_stop_orders:  # Пробегаемся по всем стоп заявкам
            buy = firm_stop_order.flags & 0b100 != 0b100  # Заявка на покупку
            class_code = firm_stop_order.board  # Код режима торгов
            sec_code = firm_stop_order.symbol  # Тикер
            stop_order_price = qgp_provider.quik_price_to_price(class_code, sec_code, firm_stop_order.price)  # Цена срабатывания стоп заявки
            si: Security = qgp_provider.interaction_stub.GetSecurityInfo(ClassSecCode(class_code=class_code, sec_code=sec_code))  # Информация о тикере
            stop_order_qty = firm_stop_order.qty * si.lot_size  # Кол-во в штуках
            logger.info(f'- Стоп заявка номер {firm_stop_order.order_num} {"Покупка" if buy else "Продажа"} {class_code}.{sec_code} {stop_order_qty} @ {stop_order_price}')

    qgp_provider.close_channel()  # Закрываем канал перед выходом
