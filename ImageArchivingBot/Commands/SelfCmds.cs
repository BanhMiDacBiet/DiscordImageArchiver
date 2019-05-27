using DSharpPlus;
using DSharpPlus.Entities;
using ImageArchivingBot.SupportLibs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static ImageArchivingBot.SupportLibs.Helpers;

namespace ImageArchivingBot.Commands
{
    class SelfCmds
    {
        private DiscordClient discordClient;
        private Program mainProgram;
        private Downloader downloader;

        private DbUpdater dbUpdater = null;
        private Helpers helper = null;

        private List<HelperMessage> HistoryMessageList = new List<HelperMessage>();
        private List<DiscordChannel> HistoryChannelList = new List<DiscordChannel>();

        private bool HistoryHelperRunning = false;
        private Task HistoryHelperTask;
        private bool HistoryDownloadHelperRunning = false;
        private Task HistoryDownloadHelperTask;

        private CancellationToken HistoryCancellationToken;

        public SelfCmds(Downloader dler, Program progRef)
        {
            downloader = dler;
            mainProgram = progRef;
        }


        public async Task ProcessSelfCommand(HelperMessage message)
        {
            // TODO: Diagnostic commands (restart bot, etc)

            if (discordClient == null)
            {
                discordClient = mainProgram.discordClient;
            }
            if (dbUpdater == null)
            {
                Trace.WriteLine("Setting database updater for self command helper.");
                dbUpdater = new DbUpdater(discordClient);
            }
            if (helper == null)
            {
                helper = new Helpers(discordClient);
            }

            bool admin = helper.CheckAdminStatus(await message.discordMessage.Channel.Guild.GetMemberAsync(message.discordMessage.Author.Id));
            if (admin == true)
            {
                if (message.discordMessage.Content == "ia!bot.ping")
                {
                    await message.discordMessage.RespondAsync($"Pong. Socket latency: {discordClient.Ping}ms");
                }
                else if (message.discordMessage.Content.ToLower().StartsWith("ia!bot.chn.info:"))
                {
                    ulong.TryParse(message.discordMessage.Content.Substring(16), out ulong channelId);
                    DiscordChannel discordChannel = await helper.TryParseDiscordChannelId(channelId, message);

                    DiscordDbContext discordDbContext = new DiscordDbContext();

                    DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                    {
                        Title = "Channel Info",
                        Description = $"Channel Name: {discordChannel.Name}\n" +
                                      $"Channel Topic: {discordChannel.Topic}\n" +
                                      $"Channel Type: {discordChannel.Type}\n" +
                                      $"Parent Guild: {discordChannel.Guild.Name}\n" +
                                      $"\n" +
                                      $"**Database Info**:\n" +
                                      $"Saved Images: {discordDbContext.Images.Where(i => i.ChannelId == discordChannel.Id).Count()}",
                        Author = new DiscordEmbedBuilder.EmbedAuthor(),
                        Color = DiscordColor.CornflowerBlue,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                    dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                    await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                }
                else if (message.discordMessage.Content.ToLower().StartsWith("ia!bot.hst.scrape:"))
                {
                    ulong.TryParse(message.discordMessage.Content.Substring(18), out ulong channelId);
                    DiscordChannel channel = await helper.TryParseDiscordChannelId(channelId, message);

                    List<DiscordChannel> visibleChannels = await helper.GetVisibleChannels(channel.Guild);
                    if (!visibleChannels.Contains(channel))
                    {
                        DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Image Archiver Bot",
                            Description = $"Error: Channel #{channel.Name} is not visible to the bot.",
                            Author = new DiscordEmbedBuilder.EmbedAuthor(),
                            Color = DiscordColor.Red,
                            Timestamp = DateTimeOffset.UtcNow
                        };
                        dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                        dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                    }
                    else if (channel.Type != ChannelType.Text)
                    {
                        DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Image Archiver Bot",
                            Description = $"Error: Channel #{channel.Name} is not a text channel.",
                            Author = new DiscordEmbedBuilder.EmbedAuthor(),
                            Color = DiscordColor.Red,
                            Timestamp = DateTimeOffset.UtcNow
                        };
                        dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                        dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                    }
                    else
                    {
                        HistoryChannelList.Add(channel);

                        DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Channel History Scraper Queue",
                            Description = $"Added channel #{channel.Name} to queue. Use `ia!bot.hst.scraper.info` for status.",
                            Author = new DiscordEmbedBuilder.EmbedAuthor(),
                            Color = DiscordColor.Green,
                            Timestamp = DateTimeOffset.UtcNow
                        };
                        dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                        dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());

                        if (HistoryHelperRunning == false || HistoryHelperTask.IsCompleted == true)
                        {
                            Trace.WriteLine("Getting new cancellation token.");
                            HistoryCancellationToken = new CancellationToken();
                            Trace.WriteLine("Starting history helper thread...");
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                            HistoryHelperRunning = true;
                            HistoryHelperTask = Task.Run(() => HistoryHelper(message.discordMessage));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        }
                    }
                }
                else if (message.discordMessage.Content.ToLower() == "ia!bot.hst.scraper.clear")
                {
                    Trace.WriteLine("Clearing history scraper channel list.");
                    HistoryChannelList.Clear();
                    DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                    {
                        Title = "Channel History Scraper Queue",
                        Description = $"Cleared the channel scrape queue.",
                        Author = new DiscordEmbedBuilder.EmbedAuthor(),
                        Color = DiscordColor.Green,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                }
                else if (message.discordMessage.Content.ToLower() == "ia!bot.hst.scraper.info")
                {
                    List<string> activeScrapers = new List<string>();
                    string scraperSuccess;
                    bool scraperStatus = false;

                    string scraperDownloadSuccess;
                    bool scraperDownloading = false;

                    activeScrapers.Add("**[ID]: Channel Name (Guild Name)**");
                    for (int i = 0; i < HistoryChannelList.Count; i++)
                    {
                        activeScrapers.Add($"[{i}]: {HistoryChannelList[i].Name} ({HistoryChannelList[i].Guild.Name})\n");
                    }
                    if (activeScrapers.Count == 1)
                    {
                        activeScrapers.Add("No channels in queue.");
                    }

                    if (HistoryHelperTask != null)
                    {
                        scraperStatus = !HistoryHelperTask.IsCompleted;
                        scraperSuccess = HistoryHelperTask.IsCompletedSuccessfully.ToString();
                    }
                    else
                    {
                        scraperSuccess = "No history scraper has run yet.";
                    }

                    if (HistoryDownloadHelperTask != null)
                    {
                        scraperDownloading = !HistoryDownloadHelperTask.IsCompleted;
                        scraperDownloadSuccess = HistoryDownloadHelperTask.IsCompletedSuccessfully.ToString();
                    }
                    else
                    {
                        scraperDownloadSuccess = "No history scraper download has run yet.";
                    }

                    DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                    {
                        Title = "Channel History Scraper Queue",
                        Description = $"{string.Join("\n", activeScrapers)}\n" +
                                      $"Currently scraping: {scraperStatus}\n" +
                                      $"Scraper exited successfully: {scraperSuccess}\n" +
                                      $"\n" +
                                      $"**Download Queue Status:**\n" +
                                      $"Current items waiting in download queue: {HistoryMessageList.Count}\n" +
                                      $"Download helper running: {scraperDownloading}\n" +
                                      $"Download helper successfully completed: {scraperDownloadSuccess}",
                        Author = new DiscordEmbedBuilder.EmbedAuthor(),
                        Color = DiscordColor.CornflowerBlue,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                    dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                    await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                }
                else if (message.discordMessage.Content.ToLower() == "ia!bot.hst.scraper.stop")
                {
                    if (HistoryHelperTask != null)
                    {
                        CancellationTokenSource cts = new CancellationTokenSource();
                        HistoryCancellationToken = cts.Token;
                        cts.Cancel();

                        DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Channel History Scraper Queue",
                            Description = $"Sent cancellation token to history scraper.",
                            Author = new DiscordEmbedBuilder.EmbedAuthor(),
                            Color = DiscordColor.CornflowerBlue,
                            Timestamp = DateTimeOffset.UtcNow
                        };
                        dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                        dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());

                        await Task.Run(async () =>
                        {
                            while (HistoryHelperTask.IsCompleted != true) { await Task.Delay(100); }
                            return;
                        });

                        cts.Dispose();

                        dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Channel History Scraper Queue",
                            Description = $"History scraper stopped. The queue may not be empty.",
                            Author = new DiscordEmbedBuilder.EmbedAuthor(),
                            Color = DiscordColor.Green,
                            Timestamp = DateTimeOffset.UtcNow
                        };
                        dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                        dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                    }
                    else
                    {
                        DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Channel History Scraper Queue",
                            Description = $"No history scraper is running.",
                            Author = new DiscordEmbedBuilder.EmbedAuthor(),
                            Color = DiscordColor.Red,
                            Timestamp = DateTimeOffset.UtcNow
                        };
                        dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                        dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                    }
                }
                else if (message.discordMessage.Content.ToLower() == "ia!bot.listguilds")
                {
                    // Send an embed with the list of guilds and names. Get further details on a guild with a different command.
                    IEnumerable<DiscordGuild> guilds = discordClient.Guilds.Values;
                    List<string> guildNamesIds = new List<string>();
                    foreach (DiscordGuild guild in guilds)
                    {
                        guildNamesIds.Add($"{guild.Name} ({guild.Id})");
                    }

                    DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                    {
                        Title = "Guilds",
                        Description = $"*Guild Name (ID)*\n{string.Join("\n", guildNamesIds)}",
                        Author = new DiscordEmbedBuilder.EmbedAuthor(),
                        Color = DiscordColor.CornflowerBlue,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                    dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                    await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                }
                else if (message.discordMessage.Content.ToLower().StartsWith("ia!bot.guildinfo:"))
                {
                    ulong.TryParse(message.discordMessage.Content.Substring(17), out ulong guildId);
                    DiscordGuild discordGuild = await helper.TryParseDiscordGuildId(guildId, message);

                    List<DiscordChannel> visibleChannels = await helper.GetVisibleChannels(discordGuild);

                    DiscordMember botMember = discordGuild.CurrentMember;
                    DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                    {
                        Title = "Guild Information",
                        Description = $"Guild Name: {discordGuild.Name}\n" +
                                      $"Guild ID: {discordGuild.Id}\n" +
                                      $"Members: {discordGuild.MemberCount}\n" +
                                      $"\n" +
                                      $"**Bot Information:**\n" +
                                      $"Display Name: {botMember.DisplayName}\n" +
                                      $"Member Since: {botMember.JoinedAt.DateTime.ToString()}\n" +
                                      $"Roles: {string.Join(", ", helper.GetRoleNames(botMember))}" +
                                      $"\n" +
                                      $"**Channel Information:**\n" +
                                      $"*Visible Channels:*\n {string.Join("\n", visibleChannels)}",
                        Author = new DiscordEmbedBuilder.EmbedAuthor(),
                        Color = DiscordColor.CornflowerBlue,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                    dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                    await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                }
                else if (message.discordMessage.Content.ToLower() == "ia!bot.dqueue.status")
                {
                    if (downloader.DownloadHelperTask != null)
                    {
                        DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Download Queue Status",
                            Description = $"Current items waiting in download queue: {downloader.DownloadMessageList.Count}\n" +
                                          $"Download helper running: {!downloader.DownloadHelperTask.IsCompleted}\n" +
                                          $"Download helper successfully completed: {downloader.DownloadHelperTask.IsCompletedSuccessfully}",
                            Author = new DiscordEmbedBuilder.EmbedAuthor(),
                            Color = DiscordColor.CornflowerBlue,
                            Timestamp = DateTimeOffset.UtcNow
                        };
                        dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                        dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                    }
                    else
                    {
                        DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Download Queue Status",
                            Description = $"Current items waiting in download queue: {downloader.DownloadMessageList.Count}\n" +
                                          $"Download helper running: False\n" +
                                          $"Download helper successfully completed: No download helper has run yet.",
                            Author = new DiscordEmbedBuilder.EmbedAuthor(),
                            Color = DiscordColor.CornflowerBlue,
                            Timestamp = DateTimeOffset.UtcNow
                        };
                        dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                        dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                    }
                }
                else if (message.discordMessage.Content.ToLower() == "ia!bot.dqueue.clear")
                {
                    downloader.DownloadMessageList.Clear();

                    DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                    {
                        Title = "Download Queue Cleared",
                        Description = $"Download queue successfully cleared.",
                        Author = new DiscordEmbedBuilder.EmbedAuthor(),
                        Color = DiscordColor.Green,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                    dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                    await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                }
                else if (message.discordMessage.Content.ToLower() == "ia!bot.dqueue.process")
                {
                    if (downloader.DownloadHelperRunning == false || downloader.DownloadHelperTask.IsCompleted == true)
                    {
                        // Runs download tasks on background thread.
                        Trace.WriteLine("Starting download helper thread...");
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        downloader.DownloadHelperRunning = true;
                        downloader.DownloadHelperTask = Task.Run(downloader.DownloadHelper);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                        DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Image Archiver Bot",
                            Description = $"Successfully started download helper.",
                            Author = new DiscordEmbedBuilder.EmbedAuthor(),
                            Color = DiscordColor.Green,
                            Timestamp = DateTimeOffset.UtcNow
                        };
                        dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                        dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                    }
                    else
                    {
                        DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Image Archiver Bot",
                            Description = $"Error: Download helper is already running.",
                            Author = new DiscordEmbedBuilder.EmbedAuthor(),
                            Color = DiscordColor.Red,
                            Timestamp = DateTimeOffset.UtcNow
                        };
                        dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                        dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                    }
                }
                else if (message.discordMessage.Content == "ia!bot.cmds")
                {
                    DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                    {
                        Title = "Image Archiver Bot Info and Control Commands",
                        Description = "`ia!bot.ping`:\n" +
                                      "Gets the socket latency of the bot.\n" +
                                      "\n" +
                                      "`ia!bot.chn.info:[channel ID]`:\n" +
                                      "Gets information for the specified channel.\n" +
                                      "\n" +
                                      "`ia!bot.hst.scrape:[channel ID]`:\n" +
                                      "Scrapes all messages previously sent in the specified channel. May take a long time.\n" +
                                      "\n" +
                                      "`ia!bot.hst.scraper.info`:\n" +
                                      "Checks the status of the historical message scraper.\n" +
                                      "\n" +
                                      "`ia!bot.hst.scraper.stop`:\n" +
                                      "Tries to stop the historical message scraper.\n" +
                                      "\n" +
                                      "`ia!bot.hst.scraper.clear`:\n" +
                                      "Clears the channel history scrape list.\n" +
                                      "\n" +
                                      "`ia!bot.listguilds`:\n" +
                                      "Gets a list of guilds the bot is in.\n" +
                                      "\n" +
                                      "`ia!bot.guildinfo:[guild ID]`:\n" +
                                      "Get information about a specific guild the bot is in.\n" +
                                      "\n" +
                                      "`ia!bot.dqueue.status`:\n" +
                                      "Checks the download queue's status.\n" +
                                      "\n" +
                                      "`ia!bot.dqueue.clear`:\n" +
                                      "Clears the bot's download queue.\n" +
                                      "\n" +
                                      "`ia!bot.dqueue.process`:\n" +
                                      "Spawns a new download queue helper if one doesn't exist.",
                        Color = DiscordColor.CornflowerBlue,
                        Author = new DiscordEmbedBuilder.EmbedAuthor(),
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                    dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                    await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                }
                else
                {
                    DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                    {
                        Title = "Image Archiver Bot",
                        Description = $"Your command was invalid. Run `ia!bot.cmds` for help.",
                        Author = new DiscordEmbedBuilder.EmbedAuthor(),
                        Color = DiscordColor.Red,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                    dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                    await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                }
            }
            else
            {
                DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                {
                    Title = "Image Archiver Bot",
                    Description = $"Error: That command requires administrator permissions.",
                    Author = new DiscordEmbedBuilder.EmbedAuthor(),
                    Color = DiscordColor.Red,
                    Timestamp = DateTimeOffset.UtcNow
                };
                dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
            }
        }

        // Start of history section.
        // Given a channel, get the most recent 100 messages, loop through them to check attachments, and send the relevant ones to a separate List<HelperMessage>
        // with its own DownloadHelper(). After that, get the 100 messages before the first one in the recent 100 and repeat until there aren't any left.

        private async Task HistoryHelper(DiscordMessage message)
        {
            Trace.WriteLine("History helper started.");
            do
            {
                if (HistoryCancellationToken.IsCancellationRequested != false)
                {
                    Trace.WriteLine("Helper cancellation request received.");
                    HistoryHelperRunning = false;
                    return;
                }
                Trace.WriteLine($"{HistoryChannelList.Count} channel(s) in download queue.");
                DiscordChannel discordChannel = HistoryChannelList.First();
                await GetHistoricalMessages(discordChannel);
                HistoryChannelList.RemoveAt(0);
            } while (HistoryChannelList.Count > 0);
            Trace.WriteLine("History helper exited.");

            DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
            {
                Title = "Channel History Scraper Queue",
                Description = $"Finished processing messages. Downloads may still be pending.",
                Author = new DiscordEmbedBuilder.EmbedAuthor(),
                Color = DiscordColor.CornflowerBlue,
                Timestamp = DateTimeOffset.UtcNow
            };
            dEmbedBuilder.Author.Name = message.Author.Username + "#" + message.Author.Discriminator;
            dEmbedBuilder.Author.IconUrl = message.Author.AvatarUrl;
            await message.RespondAsync("", false, dEmbedBuilder.Build());

            HistoryHelperRunning = false;
        }

        private async Task GetHistoricalMessages(DiscordChannel channel)
        {
            // First 100 messages
            IReadOnlyList<DiscordMessage> messages = await channel.GetMessagesAsync(100, channel.LastMessageId);

            while (messages.Count > 0)
            {
                foreach (DiscordMessage message in messages)
                {
                    if (HistoryCancellationToken.IsCancellationRequested != false)
                    {
                        Trace.WriteLine("Helper cancellation request received.");
                        return;
                    }
                    await CheckHistoricalMessage(message);
                }
                Trace.WriteLine("Getting more historical messages.");
                messages = await channel.GetMessagesAsync(100, messages.Last().Id);
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task CheckHistoricalMessage(DiscordMessage message)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            if (HistoryCancellationToken.IsCancellationRequested != false)
            {
                Trace.WriteLine("Helper cancellation request received.");
                return;
            }
            DiscordDbContext discordDbContext = new DiscordDbContext();
            if (message.Attachments.Count != 0)
            {
                bool optOut;
                try
                {
                    optOut = discordDbContext.Users.Where(u => u.IdGuildConcat == message.Author.Id.ToString() + message.Channel.GuildId.ToString()).SingleOrDefault().OptOut;
                }
                catch (NullReferenceException)
                {
                    optOut = true;
                    Trace.WriteLine("Warning: User not in database sent attachment.");
                }

                if (optOut != false)
                {
                    Trace.WriteLine("Cancelled processing attachments due to user opt-out.");
                    return;
                }
                Trace.WriteLine("Message has attachments.");
                HelperMessage helperMessage = new HelperMessage
                {
                    discordMessage = message,
                };
                HistoryMessageList.Add(helperMessage);

                if (HistoryDownloadHelperRunning == false || HistoryDownloadHelperTask.IsCompleted == true)
                {
                    // Runs download tasks on background thread.
                    Trace.WriteLine("Starting history download helper thread...");
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    HistoryDownloadHelperRunning = true;
                    HistoryDownloadHelperTask = Task.Run(HistoryDownloadHelper);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
            }
        }

        private async Task HistoryDownloadHelper()
        {
            Trace.WriteLine("History download helper started.");
            do
            {
                if (HistoryCancellationToken.IsCancellationRequested != false)
                {
                    Trace.WriteLine("Helper cancellation request received.");
                    return;
                }
                Trace.WriteLine($"{HistoryMessageList.Count} message(s) in download queue.");
                HelperMessage currentMessage = HistoryMessageList.First();
                await downloader.DownloadAttachments(currentMessage);
                HistoryMessageList.RemoveAt(0);
            } while (HistoryMessageList.Count > 0);
            Trace.WriteLine("History download helper exited.");
            HistoryDownloadHelperRunning = false;
        }
    }
}
