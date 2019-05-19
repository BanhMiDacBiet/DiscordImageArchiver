using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using Newtonsoft.Json;

namespace ImageArchivingBot
{
    class Program
    {
        public DiscordClient discordClient { get; set; }
        private MessageHelper messageHelper = null;

        public static async Task Main(string[] args)
        {
#if DEBUG
            TextWriterTraceListener consoleWriter = new TextWriterTraceListener(System.Console.Out);
            Trace.Listeners.Add(consoleWriter);
#endif
            var prog = new Program();
            await prog.RunBotAsync();
        }

        public async Task RunBotAsync()
        {
            var json = "";
            using (var fs = File.OpenRead("config.json"))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();

            var cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);
            var cfg = new DiscordConfiguration
            {
                Token = cfgjson.Token,
                TokenType = TokenType.Bot,

                AutoReconnect = true,
                LogLevel = LogLevel.Debug,
                UseInternalLogHandler = true
            };

            PrepareWorkingDirectory(cfgjson);

            this.discordClient = new DiscordClient(cfg);

            this.discordClient.Ready += this.Client_Ready;
            this.discordClient.GuildAvailable += this.Client_GuildAvailable;
            this.discordClient.ClientErrored += this.Client_ClientError;

            this.discordClient.MessageCreated += this.ReadMessage;

            await this.discordClient.ConnectAsync();

            await Task.Delay(-1);
        }

        private Task Client_Ready(ReadyEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "Image Download Bot", "Client is ready to process events.", DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Client_GuildAvailable(GuildCreateEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "Image Download Bot", $"Guild available: {e.Guild.Name}", DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Client_ClientError(ClientErrorEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Error, "Image Download Bot", $"Exception occured: {e.Exception.GetType()}: {e.Exception.Message}", DateTime.Now);
            return Task.CompletedTask;
        }

        // TODO: Add event handler for message edits (EditHelper?)

        private async Task ReadMessage(MessageCreateEventArgs e)
        {
            Trace.WriteLine("Message received.");
            bool success = await messageHelper.IngestMessage(e, this);
            if (success != true)
            {
                throw new IOException("Could not read message.");
            }
        }

        private bool PrepareWorkingDirectory(ConfigJson json)
        {
            string CurrentDir = Directory.GetCurrentDirectory();
            string DownloadDir = CurrentDir + Path.DirectorySeparatorChar + json.DownloadLocation;
            string TempDir = CurrentDir + Path.DirectorySeparatorChar + json.TempLocation;
            if (!Directory.Exists(DownloadDir))
            {
                try
                {
                    Directory.CreateDirectory(DownloadDir);
                    
                }
                catch (Exception e)
                {
                    discordClient.DebugLogger.LogMessage(LogLevel.Error, "Image Download Bot", $"Exception occurred: {e.GetType()}: {e.Message}", DateTime.Now);
                    return false;
                }
            }
            if (!Directory.Exists(TempDir))
            {
                try
                {
                    Directory.CreateDirectory(TempDir);
                }
                catch (Exception e)
                {
                    discordClient.DebugLogger.LogMessage(LogLevel.Error, "Image Download Bot", $"Exception occurred: {e.GetType()}: {e.Message}", DateTime.Now);
                    return false;
                }
            }
            Console.WriteLine($"Download directory: {DownloadDir}");
            Console.WriteLine($"Temporary directory: {TempDir}");
            messageHelper = new MessageHelper(DownloadDir, TempDir, discordClient);
            messageHelper.SetRuntimeVars(this);
            return true;
        }

        public struct ConfigJson
        {
            [JsonProperty("token")]
            public string Token { get; private set; }

            [JsonProperty("prefix")]
            public string CommandPrefix { get; private set; }

            [JsonProperty("download_location")]
            public string DownloadLocation { get; private set; }

            [JsonProperty("temp_location")]
            public string TempLocation { get; private set; }
        }
    }
}
