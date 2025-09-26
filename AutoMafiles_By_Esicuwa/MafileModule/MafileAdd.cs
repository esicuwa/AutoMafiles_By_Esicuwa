using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AutoMafiles_By_Esicuwa.Tools;
using SteamAuth;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;
using System.Net;
using static AutoMafiles_By_Esicuwa.Tools.Formats_Steam;
namespace AutoMafiles_By_Esicuwa.MafileModule
{

    public class CustomException : Exception
    {
        public int ErrorCode { get; }

        public CustomException(string message, int errorCode) : base(message)
        {
            ErrorCode = errorCode;
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


    internal class MafileAdd
    {
        private static string ConfigFilePath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
        private static readonly object fileLock = new object();
        private static SemaphoreSlim semaphore;
        private static string Imap;
        private static int attempts_config = 3;
        private static int Format = 0;
        static List<string> proxies = new List<string>();
        static List<int> ports = new List<int>();
        static long currentMilliseconds = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        static string directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "result", "MafileAdd", currentMilliseconds.ToString());
        static string resultPath = Path.Combine(directoryPath, "result.txt");
        static string errorPath = Path.Combine(directoryPath, "error.txt");

        static public async Task RunAddMafile()
        {
            List<Task> tasks = new List<Task>();
            Directory.CreateDirectory(directoryPath); 
            File.Create(resultPath).Dispose();
            File.Create(errorPath).Dispose();
            
            for (int i = 9080; i < 10080; i++)
            {
                ports.Add(i);
            }


            string jsonContent = File.ReadAllText(ConfigFilePath);
            JObject config = JObject.Parse(jsonContent);

            Imap = config["Imap"].ToString();
            List<string> DataAccounts = File.ReadAllLines(config["Accounts_Path"].ToString()).ToList();
            proxies = File.ReadAllLines(config["Proxy_Path"].ToString()).ToList();
            semaphore = new SemaphoreSlim(config["Threads"].ToObject<int>());
            attempts_config = config["Attempts"].ToObject<int>();
            Format = config["Format"].ToObject<int>();


            foreach (var Account in DataAccounts)
            {
                tasks.Add(MafAdd(Account));
            }

            await Task.WhenAll(tasks);


            Console.WriteLine("Все процессы завершены.");

            Console.ReadLine();

        }


        static async Task MafAdd(string Account)
        {
            if (string.IsNullOrWhiteSpace(Account))
            {
                Console.WriteLine("Пустая строка аккаунта, пропуск.");
                return;
            }
            List<int> errors_list = new List<int>();
            IAccount? data_account = null;
            string? authCode = null;
            string? proxy_t = null;
            Task? proxyTask = null;
            int local_port_t = 0;
            int attempts = 0;
            int errors_attemps = 3;
            bool isSuccess = false;
            string Login_log = "";
            string passKey = null;

            await semaphore.WaitAsync();

            try
            {

                while (attempts < attempts_config)
                {
                    int errors = 0;
                    string[]? Data_Account = null;

                    var cts = new CancellationTokenSource();

                    try
                    {
                        
                        try
                        {
                            
                            Data_Account = Account.Split(":");
                            Login_log = Data_Account[0];
                            Console.WriteLine($"[{Login_log}] Попытка {attempts + 1} из {attempts_config}");


                            switch (Format)
                            {
                                case 0:
                                    data_account = new Formats_Steam.Standart
                                    {
                                        Login = Data_Account[0],
                                        Password = Data_Account[1],
                                        Email = Data_Account[2],
                                        EmailPassword = Data_Account[3]
                                    };
                                    break;

                                case 1:
                                    data_account = new Formats_Steam.Standart_IMAP_Password
                                    {
                                        Login = Data_Account[0],
                                        Password = Data_Account[1],
                                        Email = Data_Account[2],
                                        EmailPassword = Data_Account[3],
                                        ImapPassword = Data_Account[4]
                                    };
                                    break;

                                case 2:
                                    data_account = new Formats_Steam.Outlook
                                    {
                                        Login = Data_Account[0],
                                        Password = Data_Account[1],
                                        Email = Data_Account[2],
                                        EmailPassword = Data_Account[3],
                                        RefreshToken = Data_Account[4],
                                        ClientId = Data_Account[5]
                                    };
                                    break;

                                case 3:
                                    data_account = new Formats_Steam.Outlook_No_Password
                                    {
                                        Login = Data_Account[0],
                                        Password = Data_Account[1],
                                        Email = Data_Account[2],
                                        RefreshToken = Data_Account[3],
                                        ClientId = Data_Account[4]
                                    };
                                    break;

                                default:
                                    throw new CustomException($"[{Login_log}]❗ Неизвестный формат. Убедитесь, что Format в диапазоне 0-3.", -100);
                            }
                        }
                        catch
                        {
                            throw new CustomException($"[{Login_log}] Неверный формат аккаунта", 1002);
                        }




                        if (proxies.Count == 0)
                        {
                            Console.WriteLine($"[{Login_log}] Нет доступных прокси. Ожидание...");
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

                            var proxies_data = proxy_t.Split(':');

                            opt = new ProxyOptions
                            {
                                ProxyHost = $"{proxies_data[0]}",
                                ProxyPort = Convert.ToInt32(proxies_data[1]),
                                ProxyUser = $"{proxies_data[2]}",
                                ProxyPass = $"{proxies_data[3]}",
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

                            throw new CustomException($"[{Login_log}] Неверный формат прокси", 1001);

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

                        //////////
                        var manager = new CallbackManager(steamClient);
                        var user = steamClient.GetHandler<SteamUser>();
                        string result;

                        manager.Subscribe<SteamClient.ConnectedCallback>(_ => user.LogOnAnonymous());
                        manager.Subscribe<SteamUser.LoggedOnCallback>(cb => result = cb.PublicIP.ToString());
                        //////////
                        
                        steamClient.Connect();

                        while (!steamClient.IsConnected)
                        {
                            await Task.Delay(500);
                            errors++;
                            if (errors == 120)
                            {
                                throw new CustomException($"[{Login_log}] Ошибка подключения к серверам steam", 3005);
                            }
                        }
                        
                        MailCheckerMafile mailCheckerMafile = new MailCheckerMafile(Login_log);

                        if (!(await mailCheckerMafile.LoadLastProcessedUidAsync(data_account, Imap, local_port_t))){

                            throw new CustomException($"[{Login_log}] Ошибка подключения к imap.", 1003);
                        }

                        CredentialsAuthSession authSession;

                        try
                        {
                            authSession = await steamClient.Authentication.BeginAuthSessionViaCredentialsAsync(new AuthSessionDetails
                            {
                                Username = data_account.Login,
                                Password = data_account.Password,
                                IsPersistentSession = false,
                                PlatformType = EAuthTokenPlatformType.k_EAuthTokenPlatformType_MobileApp,
                                ClientOSType = EOSType.Android9,
                                Authenticator = new UserEmailAuthenticator(mailCheckerMafile, data_account, Imap, local_port_t, Login_log)

                            });

                        }

                        catch (Exception ex)
                        {
                            throw new CustomException($"[{Login_log}] Ошибка входа в аккаунт: {ex.Message}", 1004);
                        }

                        AuthPollResult pollResponse;

                        try
                        {
                            pollResponse = await authSession.PollingWaitForResultAsync();
                        }
                        catch (Exception ex)
                        {
                            throw new CustomException($"[{Login_log}] Ошибка при проверка аутентификации: {ex.Message}", 2001);
                        }
                        
                        SessionData sessionData = new SessionData()
                        {
                            SteamID = authSession.SteamID.ConvertToUInt64(),
                            AccessToken = pollResponse.AccessToken,
                            RefreshToken = pollResponse.RefreshToken,
                        };

                        await mailCheckerMafile.LoadLastProcessedUidAsync(data_account, Imap, local_port_t); 

                        Console.WriteLine($"[{Login_log}] Вход в Steam успешен. Начинаю привязку мобильного аутентификатора...");

                        AuthenticatorLinker linker = new AuthenticatorLinker(sessionData,config);
                        AuthenticatorLinker.LinkResult linkResponse = AuthenticatorLinker.LinkResult.GeneralFailure;

                        errors = 0;
                        while (linkResponse != AuthenticatorLinker.LinkResult.AwaitingFinalization && errors < errors_attemps)
                        {
                            try
                            {
                                linkResponse = await linker.AddAuthenticator();
                            }
                            catch (Exception ex)
                            {
                                errors += 1;
                                if (errors >= errors_attemps)
                                {
                                    throw new CustomException($"[{Login_log}] Ошибка при добавлении аутентификатора: {ex.Message}", 2002);
                                }
                                continue;
                            }

                            switch (linkResponse)
                            {
                                case AuthenticatorLinker.LinkResult.MustProvidePhoneNumber:
                                    errors += 1;
                                    if (errors >= errors_attemps)
                                    {
                                        throw new CustomException($"[{Login_log}] Ошибка. Требуется ввод номера телефона", 2003);
                                    }
                                    continue;

                                case AuthenticatorLinker.LinkResult.AuthenticatorPresent:
                                    throw new CustomException($"[{Login_log}] На этом аккаунте уже привязан аутентификатор. Необходимо удалить его для добавления нового.", 2006);

                                case AuthenticatorLinker.LinkResult.FailureAddingPhone:
                                    linker.PhoneNumber = null;
                                    errors += 1;
                                    if (errors >= errors_attemps)
                                    {
                                        throw new CustomException($"[{Login_log}] Не удалось добавить номер телефона.", 2004);
                                    }
                                    continue;

                                case AuthenticatorLinker.LinkResult.MustRemovePhoneNumber:
                                    linker.PhoneNumber = null;
                                    break;

                                case AuthenticatorLinker.LinkResult.MustConfirmEmail:
                                    Console.WriteLine($"[{Login_log}] Пожалуйста, проверьте вашу электронную почту и подтвердите ссылку от Steam.");
                                    break;

                                case AuthenticatorLinker.LinkResult.GeneralFailure:
                                    errors += 1;
                                    if (errors >= errors_attemps)
                                    {
                                        throw new CustomException($"[{Login_log}] Ошибка при добавлении аутентификатора.", 2005);
                                    }
                                    continue;
                            }
                        }

                        Console.WriteLine($"[{Login_log}] Аутентификатор привязан. Ожидаю письмо с кодом для окончания привязки.");

                        var auth_status = await mailCheckerMafile.CheckMailAsync(data_account, Imap, local_port_t);
                        string authcode = null; 
                        if (auth_status.Success)
                        {
                            authcode= auth_status.Message;
                        }

                        AuthenticatorLinker.FinalizeResult finalizeResponse = AuthenticatorLinker.FinalizeResult.GeneralFailure;

                        errors = 0;
                        while (finalizeResponse != AuthenticatorLinker.FinalizeResult.Success && errors < errors_attemps)
                        {
                            finalizeResponse = await linker.FinalizeAddAuthenticator(authcode);

                            switch (finalizeResponse)
                            {
                                case AuthenticatorLinker.FinalizeResult.BadSMSCode:
                                    errors++;
                                    if (errors >= errors_attemps)
                                    {
                                        throw new CustomException($"[{Login_log}] Неверный код привязки.", 3001);
                                    }
                                    continue;

                                case AuthenticatorLinker.FinalizeResult.UnableToGenerateCorrectCodes:
                                    errors++;
                                    if (errors >= errors_attemps)
                                    {
                                        throw new CustomException($"[{Login_log}] Не удалось сгенерировать правильные коды для завершения привязки аутентификатора.", 3002);
                                    }
                                    continue;

                                case AuthenticatorLinker.FinalizeResult.GeneralFailure:
                                    errors++;
                                    if (errors >= errors_attemps)
                                    {
                                        throw new CustomException($"[{Login_log}] Ошибка при завершении привязки аутентификатора.", 3003);
                                    }
                                    continue;
                            }
                        }

                        //Console.WriteLine($"Аутентификатор успешно привязан. Ваш код для отмены привязки: {linker.LinkedAccount.RevocationCode}");

                        Console.WriteLine($"[{Login_log}] Сохранения аккаунта...");

                        try
                        {
                            lock (fileLock)
                            {
                                File.AppendAllText(resultPath,
                                    $"{Account}:{linker.LinkedAccount.RevocationCode}{Environment.NewLine}");
                            }

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($" Ошибка записи в файл: {ex.Message}");
                        }

                        Console.WriteLine($"[{Login_log}] Аккаунт сохранен успешно");

                        Manifest manifest = Manifest.GetManifest();

                        Console.WriteLine($"[{Login_log}] Сохранение аутентификатора...");

                        if (!manifest.SaveAccount(data_account.Login, linker.LinkedAccount, false, passKey))
                        {
                            throw new CustomException($"[{Login_log}] Не удалось сохранить файл мобильного аутентификатора. Аутентификатор не был привязан.", 3004);
                        }

                        Console.WriteLine($"[{Login_log}] Аутентификатор успешно сохранен.");

                        isSuccess = true;
                        break;
                    }
                    catch (CustomException ex)
                    {
                        Console.WriteLine($"{ex.Message}, код: {ex.ErrorCode}");

                        try
                        {
                            errors_list.Add(ex.ErrorCode);
                        }
                        catch { }

                        int code = ex.ErrorCode;
                        bool shouldContinueAttempts = true; 

                        switch (code)
                        {
                            case -100:
                                shouldContinueAttempts = false;
                                break;
                            case 1001:
                                shouldContinueAttempts = false;
                                break;
                            case 1002:
                                shouldContinueAttempts = false;
                                break;
                            case 2006:
                                shouldContinueAttempts = false;
                                break;
                            case 1003:
                                break;
                            case 1004:
                                break;
                            case 2001:
                                break;
                            case 2002:
                                break;
                            case 2003:
                                break;
                            case 2004:
                                break;
                            case 2005:
                                break;
                            case 3001:
                                break;
                            case 3002:
                                break;
                            case 3003:
                                break;
                            case 3004:
                                break;
                            case 3005:
                                break;
                            default:
                                try
                                {
                                    errors_list.Add(4000);
                                }
                                catch { }
                                break;
                        }

                        if (!shouldContinueAttempts)
                        {
                            attempts = attempts_config; 
                        }

                        attempts++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{Login_log}] Неизвестная ошибка: {ex.ToString()}");
                        try
                        {
                            errors_list.Add(4000);
                        }
                        catch { }
                        attempts++;
                    }
                    finally
                    {
                        cts.Cancel();
                        try
                        {
                            await proxyTask; 
                        }
                        catch (Exception ex){}

                       
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
                if (!isSuccess && errors_list.Count > 0)
                {
                    try
                    {
                        lock (fileLock)
                        {
                            File.AppendAllText(errorPath,
                                $"{Account}:({string.Join(", ", errors_list)}){Environment.NewLine}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($" Ошибка записи в файл: {ex.Message}");
                    }
                }

                semaphore.Release();

                string loginForLog = !string.IsNullOrEmpty(Login_log) ? Login_log : "Unknown";
                Console.WriteLine($" [{loginForLog}] Завершён поток.");
            }
        }








        public class UserEmailAuthenticator : IAuthenticator
        {


            public UserEmailAuthenticator(MailCheckerMafile mailCheckerMafile, IAccount data_account, string imap, int local_port_t, string login)
            {
                this.mailCheckerMafile = mailCheckerMafile;
                this.data_account = data_account;
                Imap = imap;
                this.local_port_t = local_port_t;
                Login = login;
            }

            public object data_account { get; set; }
            public string Imap { get; set; }
            public int local_port_t { get; set; }
            public string Login { get; set; }


            public MailCheckerMafile mailCheckerMafile {  get; set; }

            /// <inheritdoc />
            public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
            {

                return Task.FromResult("false");
            }

            /// <inheritdoc />
            public async Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
            {
                if (previousCodeWasIncorrect)
                {
                    return await Task.FromResult("false");
                }

                string? code= null;

               
                Console.Error.Write($"[{Login}] STEAM GUARD! Отправленно письмо с кодом на почту: {email}\n");
                var auth_status = await mailCheckerMafile.CheckMailAsync(data_account, Imap, local_port_t);
                if (auth_status.Success)
                {
                    code = auth_status.Message;
                }
               
                if (code == null)
                {
                    return code ?? string.Empty;

                }
                return await Task.FromResult(code!);
            }

            /// <inheritdoc />
            public Task<bool> AcceptDeviceConfirmationAsync()
            {
                return Task.FromResult(false);
            }
        }



        public class Manifest
        {
            


            public bool Encrypted { get; set; }
            public List<ManifestEntry> Entries { get; set; } = new List<ManifestEntry>();

            public static string GetExecutableDir()
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }

            public static Manifest GetManifest()
            {
                return new Manifest();
            }

      
            public bool SaveAccount(string username1, SteamGuardAccount account, bool encrypt, string passKey = null)
            {
               
                string salt = null;
                string iV = null;
                string jsonAccount = JsonConvert.SerializeObject(account);
                string executableDir = GetExecutableDir();
                string maDir = Path.Combine(directoryPath, "maFiles/");
                string filename = username1 + ".maFile";

                ManifestEntry newEntry = new ManifestEntry()
                {
                    SteamID = account.Session.SteamID,

                    IV = iV,
                    Salt = salt,
                    Filename = filename
                };

                bool foundExistingEntry = false;
                for (int i = 0; i < this.Entries.Count; i++)
                {
                    if (this.Entries[i].SteamID == account.Session.SteamID)
                    {
                        this.Entries[i] = newEntry;
                        foundExistingEntry = true;
                        break;
                    }
                }

                if (!foundExistingEntry)
                {
                    this.Entries.Add(newEntry);
                }

                bool wasEncrypted = this.Encrypted;
                this.Encrypted = encrypt || this.Encrypted;

                try
                {
                    if (!Directory.Exists(maDir))
                    {
                        Directory.CreateDirectory(maDir);
                    }

                    File.WriteAllText(maDir + filename, jsonAccount);
                    Console.WriteLine($"Файл {maDir + filename} успешно создан.");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при сохранении файла: {ex.Message}");
                    return false;
                }
            }
        }

        public class ManifestEntry
        {
            public ulong SteamID { get; set; }
            public string IV { get; set; }
            public string Salt { get; set; }
            public string Filename { get; set; }
        }

    }
    
    
}
