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
using System.Text;
using System.Threading.Tasks;
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
        public static async Task MafilesRemoves() {
            List<Task> tasks = new List<Task>();

            for (int i = 9080; i < 10080; i++)
            {
                ports.Add(i);
            }
            string jsonContent = File.ReadAllText(ConfigFilePath);
            JObject config = JObject.Parse(jsonContent);

            folderPath = config["MaFile_Path"].ToString();
            string[] files = Directory.GetFiles(folderPath, "*.maFile");
            List<string> DataAccounts = new List<string>();
            foreach (var file in files)
            {
                DataAccounts.Add(File.ReadAllText(file));
            }
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
                    var account = JsonConvert.DeserializeObject<SteamAuth.SteamGuardAccount>(Account);

                    int errors = 0;
                    var cts = new CancellationTokenSource();

                    try
                    {
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


                        // Методы обновления токенов
                        if (account.Session.IsRefreshTokenExpired())
                        {

                        }
                        else
                        {
                            throw new CustomException($"Сессия недействительна. Обновите mafile", 4001);

                        }

                        if (account.Session.IsAccessTokenExpired())
                        {
                            try
                            {
                                await account.Session.RefreshAccessToken("127.0.0.1", local_port_t);
                            }
                            catch (Exception ex)
                            {
                                throw new CustomException($"Ошибка обновления токена.", 4002);
                            }
                        }

                        bool success = await account.DeactivateAuthenticator(2, "127.0.0.1", local_port_t);

                        if (success)
                        {

                            string FileName = account.AccountName;
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

}
