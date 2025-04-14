using QuikGrpc.Services;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args); // Будем строить веб приложение
builder.Services.AddGrpc(); // Добавляем к нему сервисы gRPC
var app = builder.Build(); // Строим веб приложение
app.MapGrpcService<GRPCServiceFunctions>(); // Подключаем сервисные функции
app.MapGrpcService<GRPCInteractionFunctions>(); // Подключаем функции взаимодействия
app.MapGrpcService<GRPCCallbackFunctions>(); // Подключаем функции обратного вызова
app.MapGet("/", () => "Для связи с сервером QUIKgRPC требуется клиент"); // Сообщение, которое будет выдаваться при доступе к сервису, например, из браузера

Version version = Assembly.GetEntryAssembly()!.GetName().Version!; // Версия текущей сборки
app.Logger.LogInformation($"Версия сервера {version.Major}.{version.Minor}.{version.Build}");

app.Run(); // Запускаем веб приложение
