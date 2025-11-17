using AutoMafiles_By_Esicuwa.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using SteamAuth;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static AutoMafiles_By_Esicuwa.MafileModule.MafileAdd;
using static AutoMafiles_By_Esicuwa.Tools.Formats_Steam;
using static Org.BouncyCastle.Bcpg.Attr.ImageAttrib;
namespace AutoMafiles_By_Esicuwa.MafileModule
{
    internal class MafileRemove
    {
        private static SemaphoreSlim semaphore;
        static List<string> proxies = new List<string>();
        static List<int> ports = new List<int>();
        private static int attempts_config = 3;
        private static string ConfigFilePath = GetExecutableDir() + "/config.json";
        private static string? folderPath;
        static List<string> DataAccounts = new List<string>();
        private static string[] Files;

        public static async Task MafilesRemoves() {
            List<Task> tasks = new List<Task>();

            for (int i = 9080; i < 10080; i++)
            {
                ports.Add(i);
            }
            string jsonContent = File.ReadAllText(ConfigFilePath);
            JObject config = JObject.Parse(jsonContent);
            folderPath = config["MaFile_Path"].ToString();
            Files = Directory.GetFiles(folderPath, "*.mafile");
            List<string> DataAccounts = File.ReadAllLines(config["Accounts_Path"].ToString()).ToList();
            proxies = File.ReadAllLines(config["Proxy_Path"].ToString()).ToList();
            semaphore = new SemaphoreSlim(config["Threads"].ToObject<int>());
            attempts_config = config["Attempts"].ToObject<int>();


            foreach (var Account in DataAccounts)
            {
                tasks.Add(MafRemove(Account));
            }

            await Task.WhenAll(tasks);

            Console.WriteLine("Все процессы завершены.");

            Console.ReadLine();



        }
        public static string GetExecutableDir()
        {
            return Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
        }
        public static async Task MafRemove(string Account)
        {
            if (string.IsNullOrWhiteSpace(Account))
            {
                Console.WriteLine("Пустая строка аккаунта, пропуск.");
                return;
            }
            string? proxy_t = null;
            Task? proxyTask = null;
            int local_port_t = 0;
            int attempts = 0;
            await semaphore.WaitAsync();
            try
            {
                while (attempts < attempts_config)
                {
                    string[] account = Account.Split(":");
                    //var account = JsonConvert.DeserializeObject<SteamAuth.SteamGuardAccount>(Account);

                    int errors = 0;
                    var cts = new CancellationTokenSource();

                    try
                    {


                        string foundFile = null;
                        foreach (var file in Files)
                        {
                            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                            if (string.Equals(fileNameWithoutExt, account[0], StringComparison.OrdinalIgnoreCase))
                            {
                                // Console.WriteLine($"Файл найден: {file}");
                                foundFile = file;
                                break;
                            }
                        }
                        if (foundFile == null)
                        {
                            Console.WriteLine($"Ошибка: .mafile для аккаунта {account[0]} не найден.");
                            return;
                        }

                        var MafData = JsonConvert.DeserializeObject<SteamAuth.SteamGuardAccount>(File.ReadAllText(foundFile));

                        if (proxies.Count == 0)
                        {
                            Console.WriteLine($" Нет доступных прокси. Ожидание...");
                            await Task.Delay(5000);
                            continue;
                        }

                        lock (proxies)
                        {
                            proxy_t = proxies[0];
                            proxies.RemoveAt(0);
                            local_port_t = ports[0];
                            ports.RemoveAt(0);
                        }
                        ProxyOptions? opt;
                        try
                        {

                            var proxies_data = proxy_t.Split('@');

                            opt = new ProxyOptions
                            {
                                ProxyHost = $"{proxies_data[1].Split(':')[0]}",
                                ProxyPort = Convert.ToInt32(proxies_data[1].Split(':')[1]),
                                ProxyUser = $"{proxies_data[0].Split(':')[0]}",
                                ProxyPass = $"{proxies_data[0].Split(':')[1]}",
                                LocalPort = local_port_t

                            };

                            var proxy = new Upstream(
                                opt.ProxyHost,
                                opt.ProxyPort,
                                opt.ProxyUser,
                                opt.ProxyPass,
                                opt.LocalPort);

                            proxyTask = proxy.StartAsync(cts.Token);

                        }
                        catch
                        {
                            throw new CustomException($" Неверный формат прокси", 1001);
                        }

                        var config = SteamConfiguration.Create(b => b
                          .WithHttpClientFactory((httpMessage) =>
                          {
                              var handler = new HttpClientHandler()
                              {
                                  Proxy = new WebProxy($"http://127.0.0.1:{local_port_t}"),
                                  UseProxy = true,
                                  UseCookies = true,
                                  CookieContainer = new CookieContainer(),
                                  AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                              };

                              return new HttpClient(handler)
                              {
                                  Timeout = TimeSpan.FromSeconds(30)
                              };
                          })
                          );
                        SteamClient steamClient = new SteamClient(config);
                        steamClient.Connect();

                        while (!steamClient.IsConnected)
                        {
                            await Task.Delay(500);
                            errors++;
                            if (errors == 120)
                            {
                                throw new CustomException($"Ошибка подключения к серверам steam", 3005);
                            }
                        }
                        CredentialsAuthSession authSession = null;

                        bool succ = false;
                        string account_name = MafData.AccountName;
                        foreach (var acc_data in DataAccounts) {
                            if (acc_data.Split(":")[0] == account_name) {   
                                

                                try
                                {
                                    authSession = await steamClient.Authentication.BeginAuthSessionViaCredentialsAsync(new AuthSessionDetails
                                    {
                                        Username = acc_data.Split(":")[0],
                                        Password = acc_data.Split(":")[1],
                                        IsPersistentSession = false,
                                        PlatformType = EAuthTokenPlatformType.k_EAuthTokenPlatformType_MobileApp,
                                        ClientOSType = EOSType.Android9,
                                        Authenticator = new UserAuthenticator(MafData.SharedSecret)

                                    });

                                }

                                catch (Exception ex)
                                {
                                    throw new CustomException($" Ошибка входа в аккаунт: {ex.Message}", 1004);
                                }

                                succ = true;


                            }

                        }
                        if (!succ) {

                            throw new CustomException($"Не найдены данные для входа в аккаунт.", 6000);


                        }

                        AuthPollResult pollResponse;

                        try
                        {
                            pollResponse = await authSession.PollingWaitForResultAsync();
                        }
                        catch (Exception ex)
                        {
                            throw new CustomException($"Ошибка при получении данных: {ex.Message}", 2001);
                        }

                        MafData.Session.AccessToken = pollResponse.AccessToken;
                        // account.Session.RefreshToken = pollResponse.RefreshToken; /////


                       

                        bool success = await MafData.DeactivateAuthenticator(2, "127.0.0.1", local_port_t);

                        if (success)
                        {

                            string FileName = MafData.AccountName;
                            string filePath = Path.Combine(folderPath, $"{FileName}.maFile");
                            File.Delete(filePath);
                            Console.WriteLine("Аунтификатор удален");

                            break;
                        }
                        else
                        {
                            throw new CustomException($"Ошибка удаления аунтификатора", 4000);
                        }

                    }
                    catch (CustomException ex)
                    {
                        Console.WriteLine($"{ex.Message}, код: {ex.ErrorCode}");

                        attempts++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($" Неизвестная ошибка: {ex.ToString()}");
                       
                        attempts++;
                    }
                    finally
                    {
                        cts.Cancel();
                        try
                        {
                            await proxyTask;
                        }
                        catch (Exception ex) { }


                        if (!string.IsNullOrWhiteSpace(proxy_t) && local_port_t != 0)
                        {
                            lock (proxies)
                            {
                                proxies.Add(proxy_t);
                                ports.Add(local_port_t);
                            }
                        }
                    }
                }
            }
            finally
            {
                

                semaphore.Release();

                Console.WriteLine($"Завершён поток.");
            }


        }
        class ProxyOptions
        {

            public string ProxyHost;
            public int ProxyPort;
            public string ProxyUser;
            public string ProxyPass;
            public int LocalPort;

        }
    }

    public class UserAuthenticator : IAuthenticator
    {


        public UserAuthenticator(string SharedSecret_)
        {
            _SharedSecret = SharedSecret_;
        }

        public string _SharedSecret { get; set; }
        


        /// <inheritdoc />
        public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
        {
            var account = new SteamGuardAccount { SharedSecret = _SharedSecret };
            var loginCode = account.GenerateSteamGuardCode();

            return Task.FromResult(loginCode);
        }

        /// <inheritdoc />
        public async Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
        {
            
            return await Task.FromResult("false");
        }

        /// <inheritdoc />
        public Task<bool> AcceptDeviceConfirmationAsync()
        {
            return Task.FromResult(false);
        }
    }
}
