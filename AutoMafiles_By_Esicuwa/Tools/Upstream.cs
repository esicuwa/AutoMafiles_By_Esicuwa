using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AutoMafiles_By_Esicuwa.Tools
{
    public class Upstream
    {
        private readonly string ProxyHost;
        private readonly int ProxyPort;
        private readonly string ProxyUser;
        private readonly string ProxyPass;
        private readonly int LocalPort;

        private TcpListener listener;

        // Глобальные счётчики (необязательно)
        public static long totalUpload = 0;
        public static long totalDownload = 0;
        public static readonly object lockObj = new();

        // Вспомогательный метод для парсинга строки прокси
        public static (string host, int port, string user, string pass) ParseProxyString(string proxyString)
        {
            // Формат: host:port:user:pass
            var parts = proxyString?.Trim().Split(':');
            if (parts?.Length >= 4)
            {
                string host = parts[0].Trim();
                int.TryParse(parts[1].Trim(), out int port);
                string user = parts[2].Trim();
                string pass = string.Join(":", parts.Skip(3)).Trim(); // На случай, если в пароле есть двоеточия

              //  Console.WriteLine($"[PARSE] Host: '{host}', Port: {port}, User: '{user}', Pass: '{pass}'");
                return (host, port, user, pass);
            }

           // Console.WriteLine($"[PARSE] Неверный формат прокси: {proxyString}");
            return (null, 0, null, null);
        }

        public Upstream(string proxyHost, int proxyPort, string user, string pass, int localPort)
        {
            ProxyHost = proxyHost?.Trim();
            ProxyPort = proxyPort;
            // Убираем любые лишние пробелы и символы переноса строки
            ProxyUser = user?.Trim().Replace("\r", "").Replace("\n", "");
            ProxyPass = pass?.Trim().Replace("\r", "").Replace("\n", "");
            LocalPort = localPort;

            //Console.WriteLine($"[INIT] Proxy: {ProxyHost}:{ProxyPort}");
           // Console.WriteLine($"[INIT] User: '{ProxyUser}' (length: {ProxyUser?.Length ?? 0})");
            //Console.WriteLine($"[INIT] Pass: '{ProxyPass}' (length: {ProxyPass?.Length ?? 0})");
        }

        public async Task StartAsync(CancellationToken token)
        {
            listener = new TcpListener(IPAddress.Any, LocalPort);
            listener.Start();
            Console.WriteLine($"[Local Proxy] Запущен на порту {LocalPort}");

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var acceptTask = listener.AcceptTcpClientAsync();

                    // Ждём либо нового клиента, либо отмену через токен
                    var completedTask = await Task.WhenAny(acceptTask, Task.Delay(Timeout.Infinite, token));

                    if (completedTask == acceptTask)
                    {
                        TcpClient client = acceptTask.Result;
                        _ = HandleClientAsync(client);
                    }
                    else
                    {
                        // Отмена токена сработала — выходим из цикла
                        break;
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // listener был остановлен - нормальное завершение
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Local Proxy] Ошибка: {ex.Message}");
            }
            finally
            {
                listener?.Stop();
                Console.WriteLine($"[Local Proxy] Остановлен.");
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            TcpClient server = null;
            try
            {
                server = new TcpClient();

                // Увеличиваем таймаут подключения
                server.ReceiveTimeout = 10000;
                server.SendTimeout = 10000;

                await server.ConnectAsync(ProxyHost, ProxyPort);

                var clientStream = client.GetStream();
                var serverStream = server.GetStream();

                // Чтение запроса от клиента (например, CONNECT steamcommunity.com:443 HTTP/1.1)
                var buffer = new byte[4096];
                int bytesRead = await clientStream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                   //Console.WriteLine("[DEBUG] Клиент закрыл соединение без данных");
                    return;
                }

                string clientRequest = Encoding.UTF8.GetString(buffer, 0, bytesRead);
             //   Console.WriteLine($"[DEBUG] Запрос от клиента:\n{clientRequest}");

                // Получаем адрес назначения из CONNECT-запроса клиента
                string[] requestLines = clientRequest.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (requestLines.Length == 0 || !requestLines[0].StartsWith("CONNECT"))
                {
                   // Console.WriteLine("[!] Не CONNECT-запрос. Отклонено.");
                    await clientStream.WriteAsync(Encoding.UTF8.GetBytes("HTTP/1.1 400 Bad Request\r\n\r\n"));
                    return;
                }

                string[] connectParts = requestLines[0].Split(' ');
                if (connectParts.Length < 2)
                {
                    //Console.WriteLine("[!] Неверный формат CONNECT-запроса");
                    await clientStream.WriteAsync(Encoding.UTF8.GetBytes("HTTP/1.1 400 Bad Request\r\n\r\n"));
                    return;
                }

                string connectTarget = connectParts[1]; // example.com:443

                // Валидация адреса назначения
                if (string.IsNullOrWhiteSpace(connectTarget) || !connectTarget.Contains(':'))
                {
                    //Console.WriteLine($"[!] Неверный адрес назначения: '{connectTarget}'");
                    await clientStream.WriteAsync(Encoding.UTF8.GetBytes("HTTP/1.1 400 Bad Request\r\n\r\n"));
                    return;
                }

                var targetParts = connectTarget.Split(':');
                if (targetParts.Length != 2 || !int.TryParse(targetParts[1], out int targetPort) || targetPort <= 0 || targetPort > 65535)
                {
                   // Console.WriteLine($"[!] Неверный формат адреса или порта: '{connectTarget}'");
                    await clientStream.WriteAsync(Encoding.UTF8.GetBytes("HTTP/1.1 400 Bad Request\r\n\r\n"));
                    return;
                }

                // Проверка на валидность хоста (не должен быть просто числом)
                if (int.TryParse(targetParts[0], out _))
                {
                  //  Console.WriteLine($"[!] Некорректный хост (только число): '{targetParts[0]}'");
                    await clientStream.WriteAsync(Encoding.UTF8.GetBytes("HTTP/1.1 400 Bad Request\r\n\r\n"));
                    return;
                }

                // Формируем правильный CONNECT-запрос для прокси с корректной авторизацией
                string cleanUser = ProxyUser?.Trim().Replace("\r", "").Replace("\n", "");
                string cleanPass = ProxyPass?.Trim().Replace("\r", "").Replace("\n", "");
                string credentials = $"{cleanUser}:{cleanPass}";

                string auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
               // Console.WriteLine($"[DEBUG] Подключение к {connectTarget} через прокси");

                string proxyRequest = $"CONNECT {connectTarget} HTTP/1.1\r\n" +
                                      $"Host: {connectTarget}\r\n" +
                                      $"Proxy-Authorization: Basic {auth}\r\n" +
                                      $"User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36\r\n" +
                                      $"Proxy-Connection: keep-alive\r\n" +
                                      $"\r\n";

               // Console.WriteLine($"[DEBUG] Отправляю прокси:\n{proxyRequest}");

                byte[] proxyRequestBytes = Encoding.UTF8.GetBytes(proxyRequest);
                await serverStream.WriteAsync(proxyRequestBytes, 0, proxyRequestBytes.Length);
                await serverStream.FlushAsync();

                // Чтение ответа от прокси с таймаутом
                var responseBuffer = new byte[4096];

                var readTask = serverStream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
                var timeoutTask = Task.Delay(15000); // 15 секунд таймаут

                var completedTask = await Task.WhenAny(readTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                   // Console.WriteLine("[!] Таймаут ожидания ответа от прокси");
                    await clientStream.WriteAsync(Encoding.UTF8.GetBytes("HTTP/1.1 504 Gateway Timeout\r\n\r\n"));
                    return;
                }

                int responseBytes = await readTask;
                if (responseBytes == 0)
                {
                    //Console.WriteLine("[!] Прокси закрыл соединение без ответа");
                    await clientStream.WriteAsync(Encoding.UTF8.GetBytes("HTTP/1.1 502 Bad Gateway\r\n\r\n"));
                    return;
                }

                string proxyResponse = Encoding.UTF8.GetString(responseBuffer, 0, responseBytes);
               // Console.WriteLine($"[DEBUG] Ответ от прокси:\n{proxyResponse}");

                if (!proxyResponse.Contains("200 Connection established") && !proxyResponse.Contains("200 OK"))
                {
                    //Console.WriteLine($"[!] Ошибка установки туннеля: {proxyResponse.Trim()}");

                    // Перенаправляем ошибку клиенту
                    if (proxyResponse.Contains("407"))
                    {
                        await clientStream.WriteAsync(Encoding.UTF8.GetBytes("HTTP/1.1 407 Proxy Authentication Required\r\n\r\n"));
                    }
                    else if (proxyResponse.Contains("403"))
                    {
                        await clientStream.WriteAsync(Encoding.UTF8.GetBytes("HTTP/1.1 403 Forbidden\r\n\r\n"));
                    }
                    else
                    {
                        await clientStream.WriteAsync(Encoding.UTF8.GetBytes("HTTP/1.1 502 Bad Gateway\r\n\r\n"));
                    }
                    return;
                }

               // Console.WriteLine($"[SUCCESS] Туннель установлен для {connectTarget}");

                // Отправляем клиенту подтверждение, что туннель установлен
                string connectionEstablishedResponse = "HTTP/1.1 200 Connection established\r\n\r\n";
                await clientStream.WriteAsync(Encoding.UTF8.GetBytes(connectionEstablishedResponse));
                await clientStream.FlushAsync();

                // Прямое копирование данных в обоих направлениях
                var uploadTask = ForwardAsync(clientStream, serverStream, true, connectTarget);
                var downloadTask = ForwardAsync(serverStream, clientStream, false, connectTarget);

                await Task.WhenAny(uploadTask, downloadTask);

                //Console.WriteLine($"[INFO] Соединение с {connectTarget} завершено");
            }
            catch (Exception ex)
            {
              //  Console.WriteLine($"[!] Ошибка обработки клиента: {ex.Message}");
               // Console.WriteLine($"[!] StackTrace: {ex.StackTrace}");

                try
                {
                    var clientStream = client?.GetStream();
                    if (clientStream != null && clientStream.CanWrite)
                    {
                        await clientStream.WriteAsync(Encoding.UTF8.GetBytes("HTTP/1.1 500 Internal Server Error\r\n\r\n"));
                    }
                }
                catch { /* игнорируем ошибки при отправке ошибки */ }
            }
            finally
            {
                try
                {
                    server?.Close();
                    client?.Close();
                }
                catch { /* игнорируем ошибки при закрытии */ }
            }
        }

        private async Task ForwardAsync(NetworkStream input, NetworkStream output, bool isUpload, string target = "")
        {
            var buffer = new byte[8192];
            int bytesRead;
            long bytesThisSession = 0;

            try
            {
                while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await output.WriteAsync(buffer, 0, bytesRead);
                    await output.FlushAsync();
                    bytesThisSession += bytesRead;

                    lock (lockObj)
                    {
                        if (isUpload)
                            totalUpload += bytesRead;
                        else
                            totalDownload += bytesRead;
                    }
                }
            }
            catch (Exception ex)
            {
               // Console.WriteLine($"[DEBUG] Поток {(isUpload ? "исходящий" : "входящий")} прерван: {ex.Message}");
            }

           // Console.WriteLine($"[INFO] {(isUpload ? "Исходящий" : "Входящий")} поток завершён ({target}). Передано: {bytesThisSession} байт.");
        }
    }
}