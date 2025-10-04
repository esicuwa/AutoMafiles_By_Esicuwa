using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Proxy;
using MailKit.Search;
using MailKit.Security;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace AutoMafiles_By_Esicuwa.Tools
{

    


    public class MailCheckerMafile
    {

        public class ResultData
        {
            public bool Success { get; set; }
            public string Message { get; set; }
        }



        public MailCheckerMafile(string Login)

        {
            this.Login = Login;

        }
        public string Login { get; set; }

        private static UniqueId? lastProcessedUid = null;
        private string? AccessToken = null;


        public async Task<bool> LoadLastProcessedUidAsync(object Account, string imapServer, int localPort)
        {
            try
            {

                string? newAccessToken;
                OAuthHelper? oauthHelper;
                using var client = new ImapClient();
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                client.ProxyClient = new HttpConnectProxyClient("127.0.0.1", localPort);

                await client.ConnectAsync(imapServer, 993, true);

                switch (Account)
                {

                    case Formats_Steam.Standart s:
                        await client.AuthenticateAsync(s.Email, s.EmailPassword);
                        break;

                    case Formats_Steam.Standart_IMAP_Password s:

                        await client.AuthenticateAsync(s.Email, s.ImapPassword);
                        break;

                    case Formats_Steam.Outlook o:
                        if (AccessToken == null)
                        {
                            oauthHelper = new OAuthHelper(o.ClientId);

                            newAccessToken = await oauthHelper.RefreshAccessTokenAsync(o.RefreshToken, localPort);


                            if (newAccessToken != null)
                            {
                                AccessToken = newAccessToken;
                                Console.WriteLine($"[{Login}] Access token обновлен");
                                var oauth2 = new SaslMechanismOAuth2(o.Email, newAccessToken);
                                await client.AuthenticateAsync(oauth2);
                            }

                            else
                                Console.WriteLine($"[{Login}] Не удалось обновить токен");
                        }
                        else
                        {
                            var oauth2 = new SaslMechanismOAuth2(o.Email, AccessToken);
                            await client.AuthenticateAsync(oauth2);
                        }
                        break;

                    case Formats_Steam.Outlook_No_Password o:
                        if (AccessToken == null)
                        {
                            oauthHelper = new OAuthHelper(o.ClientId);

                            newAccessToken = await oauthHelper.RefreshAccessTokenAsync(o.RefreshToken, localPort);

                            if (newAccessToken != null)
                            {
                                AccessToken = newAccessToken;

                                Console.WriteLine($"[{Login}] Access token обновлен");
                                var oauth2 = new SaslMechanismOAuth2(o.Email, newAccessToken);
                                await client.AuthenticateAsync(oauth2);
                            }

                            else
                                Console.WriteLine($"[{Login}] Не удалось обновить токен");
                        }
                        else
                        {
                            var oauth2 = new SaslMechanismOAuth2(o.Email, AccessToken);
                            await client.AuthenticateAsync(oauth2);
                        }
                        break;

                    default:
                        Console.WriteLine($"[{Login}] Неизвестный формат.");
                        break;
                }


                var inbox = client.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadWrite);

                var recentUids = await inbox.SearchAsync(SearchQuery.All);
                var last10Uids = recentUids.OrderByDescending(u => u).Take(10).ToList();

                if (last10Uids.Count > 0)
                {
                    lastProcessedUid = last10Uids.Max();
                    Console.WriteLine($"[{Login}] Загружен последний обработанный UID: {lastProcessedUid}");
                }
                else
                {
                    lastProcessedUid = null;
                    Console.WriteLine($"[{Login}] Письма не найдены, последний UID не установлен.");
                }

                await client.DisconnectAsync(true);
                return true;
            }
            catch(Exception ex) 
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }
        public async Task<ResultData> CheckMailAsync(object Account, string imapServer, int localPort)
        {
            string newAccessToken = null;
            var timeout = TimeSpan.FromMinutes(1);
            var pollingInterval = TimeSpan.FromSeconds(10);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            OAuthHelper? oauthHelper;

            ImapClient client = new ImapClient();
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;
            client.ProxyClient = new HttpConnectProxyClient("127.0.0.1", localPort);
            client.Connect(imapServer, 993, true);

            switch (Account)
            {
                case Formats_Steam.Standart s:
                    await client.AuthenticateAsync(s.Email, s.EmailPassword);
                    break;

                case Formats_Steam.Standart_IMAP_Password s:
                    await client.AuthenticateAsync(s.Email, s.ImapPassword);
                    break;

                case Formats_Steam.Outlook o:
                    if (AccessToken == null)
                    {
                        oauthHelper = new OAuthHelper(o.ClientId);

                        newAccessToken = await oauthHelper.RefreshAccessTokenAsync(o.RefreshToken, localPort);


                        if (newAccessToken != null)
                        {
                            AccessToken = newAccessToken;

                            Console.WriteLine($"[{Login}] Access token обновлен");
                            var oauth2 = new SaslMechanismOAuth2(o.Email, newAccessToken);
                            await client.AuthenticateAsync(oauth2);
                        }

                        else
                            Console.WriteLine($"[{Login}] Не удалось обновить токен");
                    }
                    else
                    {
                        var oauth2 = new SaslMechanismOAuth2(o.Email, AccessToken);
                        await client.AuthenticateAsync(oauth2);
                    }
                    break;

                case Formats_Steam.Outlook_No_Password o:
                    if (AccessToken == null)
                    {
                        oauthHelper = new OAuthHelper(o.ClientId);

                        newAccessToken = await oauthHelper.RefreshAccessTokenAsync(o.RefreshToken, localPort);

                        if (newAccessToken != null)
                        {
                            AccessToken = newAccessToken;

                            Console.WriteLine($"[{Login}] Access token обновлен");
                            var oauth2 = new SaslMechanismOAuth2(o.Email, newAccessToken);
                            await client.AuthenticateAsync(oauth2);
                        }

                        else
                            Console.WriteLine($"[{Login}] Не удалось обновить токен");
                    }
                    else
                    {
                        var oauth2 = new SaslMechanismOAuth2(o.Email, AccessToken);
                        await client.AuthenticateAsync(oauth2);
                    }
                    break;

                default:
                    Console.WriteLine($"[{Login}] Неизвестный формат.");
                    break;
            }

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadWrite);

            int reconnectEveryNCycles = 3;
            int currentCycle = 0;

            while (stopwatch.Elapsed < timeout)
            {
                if (currentCycle % reconnectEveryNCycles == 0 && currentCycle != 0)
                {
                    Console.WriteLine($"[{Login}] Переподключение для обновления содержимого...");
                    await client.DisconnectAsync(true);
                    client.Dispose();

                    client = new ImapClient();
                    await client.ConnectAsync(imapServer, 993, true);
                    switch (Account)
                    {
                        case Formats_Steam.Standart s:
                            await client.AuthenticateAsync(s.Email, s.EmailPassword);
                            break;

                        case Formats_Steam.Standart_IMAP_Password s:
                            await client.AuthenticateAsync(s.Email, s.ImapPassword);
                            break;

                        case Formats_Steam.Outlook o:
                            if (AccessToken == null)
                            {
                                oauthHelper = new OAuthHelper(o.ClientId);

                                newAccessToken = await oauthHelper.RefreshAccessTokenAsync(o.RefreshToken, localPort);


                                if (newAccessToken != null)
                                {
                                    AccessToken = newAccessToken;

                                    Console.WriteLine($"[{Login}] Access token обновлен");
                                    var oauth2 = new SaslMechanismOAuth2(o.Email, newAccessToken);
                                    await client.AuthenticateAsync(oauth2);
                                }

                                else
                                    Console.WriteLine($"[{Login}] Не удалось обновить токен");
                            }
                            else
                            {
                                var oauth2 = new SaslMechanismOAuth2(o.Email, AccessToken);
                                await client.AuthenticateAsync(oauth2);
                            }
                            break;

                        case Formats_Steam.Outlook_No_Password o:
                            if (AccessToken == null)
                            {
                                oauthHelper = new OAuthHelper(o.ClientId);


                                newAccessToken = await oauthHelper.RefreshAccessTokenAsync(o.RefreshToken, localPort);

                                if (newAccessToken != null)
                                {
                                    AccessToken = newAccessToken;

                                    Console.WriteLine($"[{Login}] Access token обновлен");
                                    var oauth2 = new SaslMechanismOAuth2(o.Email, newAccessToken);
                                    await client.AuthenticateAsync(oauth2);
                                }

                                else
                                    Console.WriteLine($"[{Login}] Не удалось обновить токен");
                            }
                            else
                            {
                                var oauth2 = new SaslMechanismOAuth2(o.Email, AccessToken);
                                await client.AuthenticateAsync(oauth2);
                            }
                            break;

                        default:
                            Console.WriteLine($"[{Login}] Неизвестный формат.");
                            break;
                    }
                    inbox = client.Inbox;
                    await inbox.OpenAsync(FolderAccess.ReadWrite);
                }

                var allUids = await inbox.SearchAsync(SearchQuery.All);

                var uidsToProcess = lastProcessedUid.HasValue
    ? allUids.Where(u => u.Id > lastProcessedUid.Value.Id)
    : allUids.OrderByDescending(u => u).Take(10);

                foreach (var uid in uidsToProcess.OrderBy(u => u))
                {
                    var message = await inbox.GetMessageAsync(uid);
                    string htmlBody = message.HtmlBody;

                    if (!string.IsNullOrEmpty(htmlBody))
                    {
                        var doc = new HtmlAgilityPack.HtmlDocument();
                        doc.LoadHtml(htmlBody);

                        var linkNode = doc.DocumentNode.SelectSingleNode(
                            "//td[contains(@class, 'title-48') and contains(@class, 'c-blue1') and contains(@class, 'fw-b') and contains(@class, 'a-center')]");

                        if (linkNode != null)
                        {
                            var link = linkNode.InnerText;
                            var correctedLink = link.Replace("&amp;", "&");

                            Console.WriteLine($"[{Login}] Найдена новый код: " + link.Trim().Replace("\n"," "));
                            bool success = false;
                            await Task.Delay(new Random().Next(5000, 10000));
                            await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true); 


                            await client.DisconnectAsync(true);
                            return new ResultData { 
                                Success = true,
                                Message = link.Trim().Replace("\n", " ")

                            };
                        }
                        else
                        {
                            linkNode = doc.DocumentNode.SelectSingleNode(
                            "//td[contains(@class, 'x_title-48') and contains(@class, 'x_c-blue1') and contains(@class, 'x_fw-b') and contains(@class, 'x_a-center')]");
                            if (linkNode != null)
                            {
                                var link = linkNode.InnerText;
                                var correctedLink = link.Replace("&amp;", "&");

                                Console.WriteLine($"[{Login}] Найдена новый код: " + link.Trim().Replace("\n", " "));
                                bool success = false;
                                await Task.Delay(new Random().Next(5000, 10000));
                                await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true); 

                                await client.DisconnectAsync(true);
                                return new ResultData
                                {
                                    Success = true,
                                    Message = link.Trim().Replace("\n", " ")

                                };
                            }
                        }
                    }

                    await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true);
                    lastProcessedUid = uid;
                }

                await Task.Delay(pollingInterval);
                currentCycle++;
            }

            await client.DisconnectAsync(true);
            Console.WriteLine($"[{Login}] Таймаут: письмо не получено в течение минуты.");
            return new ResultData
            {
                Success = false,
                Message = "Timeout"

            }; ;
        }

      

        public class OAuthHelper
        {
            private readonly string clientId;
            private readonly string tenant; 

            public OAuthHelper(string clientId, string tenant = "common")
            {
                this.clientId = clientId;
                this.tenant = tenant;
            }

            public async Task<string?> RefreshAccessTokenAsync(string refreshToken, int localPort)
            {
                var handler = new HttpClientHandler
                {
                    Proxy = new WebProxy($"http://127.0.0.1:{localPort}"), 
                    UseProxy = true
                };

                using var httpClient = new HttpClient(handler);

                var values = new Dictionary<string, string>
    {
        { "client_id", clientId },
        { "refresh_token", refreshToken },
        { "grant_type", "refresh_token" },
        { "scope", "https://outlook.office.com/IMAP.AccessAsUser.All offline_access" }
    };

                var content = new FormUrlEncodedContent(values);

                var response = await httpClient.PostAsync(
                    $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token", content);

                var responseBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Ошибка обновления токена: {response.StatusCode}, ответ: {responseBody}");
                    return null;
                }

                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("access_token", out var tokenElement))
                {
                    return tokenElement.GetString();
                }

                return null;
            }
        }


        public class HttpConnectProxyClient : IProxyClient
        {
            public string ProxyHost { get; }
            public int ProxyPort { get; }

            public HttpConnectProxyClient(string host, int port)
            {
                ProxyHost = host;
                ProxyPort = port;
            }

            public IPEndPoint LocalEndPoint { get; set; } = null;

            public NetworkCredential ProxyCredentials { get; set; } = null;

            public Stream Connect(string host, int port, CancellationToken cancellationToken = default)
            {
                var tcp = new TcpClient();
                if (LocalEndPoint != null)
                    tcp.Client.Bind(LocalEndPoint);

                tcp.ConnectAsync(ProxyHost, ProxyPort).Wait(cancellationToken);

                var stream = tcp.GetStream();

                SendHttpConnectRequest(stream, host, port);

                return stream;
            }

            public async Task<Stream> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
            {
                var tcp = new TcpClient();
                if (LocalEndPoint != null)
                    tcp.Client.Bind(LocalEndPoint);

                await tcp.ConnectAsync(ProxyHost, ProxyPort, cancellationToken);

                var stream = tcp.GetStream();

                await SendHttpConnectRequestAsync(stream, host, port, cancellationToken);

                return stream;
            }

            public Stream Connect(string host, int port, int timeout, CancellationToken cancellationToken = default)
            {
                var tcp = new TcpClient();
                if (LocalEndPoint != null)
                    tcp.Client.Bind(LocalEndPoint);

                var connectTask = tcp.ConnectAsync(ProxyHost, ProxyPort);

                if (!connectTask.Wait(timeout, cancellationToken))
                    throw new TimeoutException($"Соединение с прокси {ProxyHost}:{ProxyPort} не удалось за {timeout} мс");

                var stream = tcp.GetStream();

                SendHttpConnectRequest(stream, host, port);

                return stream;
            }

            public async Task<Stream> ConnectAsync(string host, int port, int timeout, CancellationToken cancellationToken = default)
            {
                var tcp = new TcpClient();
                if (LocalEndPoint != null)
                    tcp.Client.Bind(LocalEndPoint);

                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(timeout);

                    await tcp.ConnectAsync(ProxyHost, ProxyPort, cts.Token);
                }

                var stream = tcp.GetStream();

                await SendHttpConnectRequestAsync(stream, host, port, cancellationToken);

                return stream;
            }

            private void SendHttpConnectRequest(Stream stream, string host, int port)
            {
                var connectRequest = $"CONNECT {host}:{port} HTTP/1.1\r\nHost: {host}:{port}\r\n";

                if (ProxyCredentials != null)
                {
                    var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{ProxyCredentials.UserName}:{ProxyCredentials.Password}"));
                    connectRequest += $"Proxy-Authorization: Basic {auth}\r\n";
                }

                connectRequest += "\r\n";

                var requestBytes = Encoding.ASCII.GetBytes(connectRequest);
                stream.Write(requestBytes, 0, requestBytes.Length);

                using (var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, true))
                {
                    string line;
                    bool success = false;
                    while (!string.IsNullOrEmpty(line = reader.ReadLine()))
                    {
                        if (line.StartsWith("HTTP/1.1 200"))
                            success = true;
                    }

                    if (!success)
                        throw new IOException("Не удалось установить HTTP CONNECT-туннель через прокси");
                }
            }

            private async Task SendHttpConnectRequestAsync(Stream stream, string host, int port, CancellationToken cancellationToken)
            {
                var connectRequest = $"CONNECT {host}:{port} HTTP/1.1\r\nHost: {host}:{port}\r\n";

                if (ProxyCredentials != null)
                {
                    var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{ProxyCredentials.UserName}:{ProxyCredentials.Password}"));
                    connectRequest += $"Proxy-Authorization: Basic {auth}\r\n";
                }

                connectRequest += "\r\n";

                var requestBytes = Encoding.ASCII.GetBytes(connectRequest);
                await stream.WriteAsync(requestBytes, 0, requestBytes.Length, cancellationToken);

                using (var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, true))
                {
                    string line;
                    bool success = false;
                    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                    {
                        if (line.StartsWith("HTTP/1.1 200"))
                            success = true;
                    }

                    if (!success)
                        throw new IOException("Не удалось установить HTTP CONNECT-туннель через прокси");
                }
            }
        }
    }

}
