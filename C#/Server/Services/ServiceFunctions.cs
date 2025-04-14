using Google.Protobuf;
using Grpc.Core;

namespace QuikGrpc.Services
{
    public class GRPCServiceFunctions : ServiceFunctions.ServiceFunctionsBase
    {
        #region Фукнции отладки QUIK#

        public override Task<QuikString> Ping(QuikString request, ServerCallContext context) =>
            Task.FromResult(new QuikString { Value = Core.ProcessRequest("ping", request.Value)["data"]!.ToString() }); // Выполняем запрос Ping и получаем строку Pong

        public override Task<QuikString> Echo(QuikString request, ServerCallContext context) =>
            Task.FromResult(new QuikString { Value = Core.ProcessRequest("echo", request.Value)["data"]!.ToString() }); // Выполняем запрос и получаем строку обратно

        public override Task<Empty> DivideStringByZero(Empty request, ServerCallContext context)
        {
            Core.ProcessRequest("divide_string_by_zero"); // Выполняем запрос с ошибкой
            return Task.FromResult(new Empty()); // Возвращаем пустое значение
        }

        public override Task<QuikBool> IsQuik(Empty request, ServerCallContext context) =>
            Task.FromResult(new QuikBool { Value = Core.ProcessRequest("is_quik")["data"]!.ToString() == "1" }); // Выполняем запрос и возвращаем True, если скрипт запущен в QUIK

        #endregion

        #region 2.1 Сервисные функции

        public override Task<QuikBool> IsConnected(Empty request, ServerCallContext context) =>
            Task.FromResult(new QuikBool { Value = Core.ProcessRequest("isConnected")["data"]!.ToString() == "1" }); // Выполняем запрос и возвращаем статус соединения как логическое значение

        public override Task<QuikString> GetScriptPath(Empty request, ServerCallContext context) =>
            Task.FromResult(new QuikString { Value = Core.ProcessRequest("getScriptPath")["data"]!.ToString() }); // Выполняем запрос и возвращаем путь скрипта в виде строки

        public override Task<QuikString> GetInfoParam(InfoParamRequest request, ServerCallContext context) =>
            Task.FromResult(new QuikString { Value = Core.ProcessRequest("getInfoParam", request.ParamName.ToString().ToUpper())["data"]!.ToString() }); // Выполняем запрос и возвращаем параметр информационного окна в виде строки

        public override Task<Empty> Message(MessageRequest request, ServerCallContext context)
        {
            string command = request.IconType == MessageIconType.Info ? "message" : request.IconType == MessageIconType.Warning ? "warning_message" : "error_message"; // Тип сообщения в зависимости от выбранной иконки
            Core.ProcessRequest(command, request.Message); // Выполняем запрос
            return Task.FromResult(new Empty()); // Возвращаем пустое значение
        }

        public override Task<Empty> Sleep(QuikInt request, ServerCallContext context)
        {
            Core.ProcessRequest("sleep", request.ToString()); // Выполняем запрос
            return Task.FromResult(new Empty()); // Возвращаем пустое значение
        }

        public override Task<QuikString> GetWorkingFolder(Empty request, ServerCallContext context) =>
            Task.FromResult(new QuikString { Value = Core.ProcessRequest("getWorkingFolder")["data"]!.ToString() }); // Выполняем запрос и возвращаем путь, по которому находится файл info.exe без завершающего обратного слеша (\)

        public override Task<Empty> PrintDbgStr(QuikString request, ServerCallContext context)
        {
            Core.ProcessRequest("PrintDbgStr", request.Value); // Выполняем запрос
            return Task.FromResult(new Empty()); // Возвращаем пустое значение
        }

        #endregion
    }
}
