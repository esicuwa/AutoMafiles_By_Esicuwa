using AutoMafiles_By_Esicuwa.MafileModule;
using Newtonsoft.Json.Linq;
using SteamKit2;

namespace AutoMafiles_By_Esicuwa
{
    internal class Program
    {
        private static string ConfigFilePath = GetExecutableDir() + "/config.json";

        static async Task Main(string[] args)
        {
            string jsonContent = File.ReadAllText(ConfigFilePath);
            JObject config = JObject.Parse(jsonContent);
            int Type = config["Type"].ToObject<int>();

            if (Type == 0)
            {
                await MafileAdd.RunAddMafile();
            }
            else if (Type == 1) 
            { 
                await MafileRemove.MafilesRemoves();
            }
        }


        public static string GetExecutableDir()
        {
            return Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
        }
    }
}
