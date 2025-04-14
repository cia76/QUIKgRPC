using Google.Protobuf;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace QuikGrpc
{
    internal class Core
    {
        private const string request_template = "{\"data\": \"\", \"id\": 0, \"cmd\": \"\", \"t\": \"\"}"; // Шаблон запроса JSON в QUIK#

        private static readonly string host; // IP адрес или название хоста
        private static readonly int requests_port; // Порт для отправки запросов и получения ответов
        private static readonly int callbacks_port; // Порт для функций обратного вызова
        private static readonly TcpClient requests_client; // Клиент для отправки запросов и получения ответов
        private static readonly TcpClient callbacks_client; // Клиент для функций обратного вызова
        private static readonly JsonSerializerOptions jso; // Опции для сериализации JSON
        private static readonly Encoding encoding; // LUA в QUIK использует кодировку Windows 1251. Будем конвертировать в нее перед отправкой запроса и из нее при получении ответа

        static Core()
        {
            jso = new JsonSerializerOptions // Опции для сериализации JSON
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Не нужно конвертировать русские буквы в коды UTF
            };
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // Регистрируем кодировки из .NET в .NET Core
            encoding = Encoding.GetEncoding(1251); // Будем использовать кодировку Windows 1251

            var config_builder = new ConfigurationBuilder() // Построение конфигурации
                .SetBasePath(Directory.GetCurrentDirectory()) // из текущего каталога
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true) // файла appsettings.json
                .AddEnvironmentVariables(); // с добавлением переменных окружения
            var config = config_builder.Build(); // Получаем конфигурацию
            var section = config.GetSection("ApplicationSettings"); // Настройки программы храним в разделе ApplicationSettings конфигурации
            host = section.GetValue<string>("Host") ?? "localhost"; // IP адрес или название хоста
            requests_port = section.GetValue<int>("RequestsPort"); // Порт для отправки запросов и получения ответов
            callbacks_port = section.GetValue<int>("CallbacksPort"); // Порт для функций обратного вызова

            requests_client = new TcpClient // Создаем клиента для отправки запросов и получения ответов
            {
                ExclusiveAddressUse = true, // Только один клиент может использовать порт
                NoDelay = true // Нет задержки для передачи/приема данных, когда буфер заполнен не полностью
            };
            requests_client.Connect(host, requests_port); // Подключаемся
            callbacks_client = new TcpClient // Создаем клиента для функций обратного вызова
            {
                ExclusiveAddressUse = true, // Только один клиент может использовать порт
                NoDelay = true // Нет задержки для передачи/приема данных, когда буфер заполнен не полностью
            };
            callbacks_client.Connect(host, callbacks_port); // Подключаемся
            WaitForCallbacks(); // Будем слушать что приходит
        }

        public static JsonNode ProcessRequest(string command, string data = "")
        {
            var json_request = JsonNode.Parse(request_template); // Переводим шаблон запроса в JSON
            json_request!["cmd"] = command; // Команда
            json_request["data"] = data.Replace('\'', '"'); // Параметры команды с заменой одинарных кавычек на двойные
            string json_string = string.Empty; // Ответ в виде строки JSON
            using (var stream = new NetworkStream(requests_client.Client)) // Клиент для отправки запросов и получения ответов
            using (var writer = new StreamWriter(stream, encoding)) // Будем отправлять запрос в клиента
            using (var reader = new StreamReader(stream, encoding)) // Затем сразу будем получать ответ от клиента
            {
                json_string = json_request.ToJsonString(jso); // Отправлять запрос будем в виде строки JSON без конвертации русских букв в коды UTF
                json_string = json_string.Replace("\"[", " {").Replace("]\"", "}"); // Меняем квадратные скобки на фигурные, убираем у них кавычки (bulk-запросы QUIK#)
                json_string = json_string.Replace("\"{", " {").Replace("}\"", "}"); // Убираем кавычки у фигурных скобок (sendTransaction)
                json_string = json_string.Replace("\\", string.Empty); // Убираем escape (\) перед двойными кавычками (bulk-запросы QUIK#)
                writer.WriteLine(json_string); // Отправляем запрос 
                writer.Flush(); // Очищаем буфер
                json_string = reader.ReadLine() ?? string.Empty; // Получаем ответ в виде строки
            }
            return JsonNode.Parse(json_string)!; // Переводим ответ в JSON
        }

        public static readonly JsonParser.Settings json_settings = JsonParser.Settings.Default.WithIgnoreUnknownFields(true); // При конвертации из JSON в класс Protobuf игнорировать неизвестные поля

        public static event Func<JsonNode, Task>? OnNewCallback; // Событие получения функции обратного вызова

        public static CancellationTokenSource CancellationToken = new(); // Токен отмены получения функций обратного вызова

        public static List<Tuple<Guid, string, string>> Subscriptions = []; // Глобальные подписки

        private static void WaitForCallbacks()
        {
            Task.Factory.StartNew(async () => // Слушать функции обратного вызова будем асинхронно в отдельной задаче
        {
            string text_response = string.Empty; // Ответ в виде строки
            using var stream = new NetworkStream(callbacks_client.Client); // Клиент для функций обратного вызова
            using var reader = new StreamReader(stream, encoding); // Будем получать ответ от клиента
            while (!CancellationToken.IsCancellationRequested) // До отмены
            {
                text_response = await reader.ReadLineAsync() ?? string.Empty; // Получаем ответ в виде строки
                OnNewCallback?.Invoke(JsonNode.Parse(text_response)!); // Переводим ответ в JSON, запускаем событие получения функции обратного вызова
            }
        });
        }

        public static float ToFloat(JsonNode jsonNode)
        {
            if (jsonNode is null)
                return 0;
            bool converted = float.TryParse(jsonNode.ToString(), CultureInfo.InvariantCulture, out float value);
            return converted ? value : 0;
        }

        public static int ToInt32(JsonNode jsonNode)
        {
            if (jsonNode is null)
                return 0;
            bool converted = int.TryParse(jsonNode.ToString(), CultureInfo.InvariantCulture, out int value);
            return converted ? value : 0;
        }

        public static ulong ToUInt64(JsonNode jsonNode)
        {
            if (jsonNode is null)
                return 0;
            bool converted = ulong.TryParse(jsonNode.ToString(), CultureInfo.InvariantCulture, out ulong value);
            return converted ? value : 0;
        }
    }
}
