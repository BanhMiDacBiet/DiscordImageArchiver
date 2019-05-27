using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using ImageArchivingBot.Models;
using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using System.Threading;
using ImageArchivingBot.SupportLibs;
using ImageArchivingBot.Commands;
using static ImageArchivingBot.SupportLibs.Helpers;

namespace ImageArchivingBot
{
    class MessageHelper
    {
        public string DownloadDirectory;
        public string TempDirectory;
        private DiscordClient discordClient;
        private Helpers helper;

        public MessageHelper(string downloadDirectory, string tempDirectory, DiscordClient dClient)
        {
            DownloadDirectory = downloadDirectory;
            TempDirectory = tempDirectory;
            Trace.WriteLine("Setting Discord instance for message helper.");
            discordClient = dClient;
        }

        private Program mainProgram = null;
        private AdminCmds adminCmd = null;
        private SelfCmds selfCmd = null;
        private Downloader downloader = null;
        private ModifyDb modifyDb = null;

        private List<HelperMessage> CommandMessageList = new List<HelperMessage>();

        private bool CommandHelperRunning = false;
        private Task CommandHelperTask;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<bool> IngestMessage(MessageCreateEventArgs e, Program progRef)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            if (discordClient == null)
            {
                Trace.WriteLine("Setting Discord instance for message helper.");
                discordClient = progRef.discordClient;
            }
            if (helper == null)
            {
                helper = new Helpers(discordClient);
            }
            if (adminCmd == null)
            {
                adminCmd = new AdminCmds(discordClient, mainProgram, DownloadDirectory, TempDirectory);
            }
            if (downloader == null)
            {
                downloader = new Downloader(discordClient);
                downloader.DownloadDirectory = DownloadDirectory;
                downloader.TempDirectory = TempDirectory;
            }
            if (selfCmd == null)
            {
                selfCmd = new SelfCmds(downloader, mainProgram);
            }
            DiscordDbContext discordDbContext = new DiscordDbContext();
            if (e.Message.Attachments.Count != 0)
            {
                bool optOut;
                try
                {
                    optOut = discordDbContext.Users.Where(u => u.IdGuildConcat == e.Message.Author.Id.ToString() + e.Message.Channel.GuildId.ToString()).SingleOrDefault().OptOut;
                }
                catch (NullReferenceException)
                {
                    optOut = true;
                    Trace.WriteLine("Warning: User not in database sent attachment.");
                }

                if (optOut != false)
                {
                    Trace.WriteLine("Cancelled processing attachments due to user opt-out.");
                    return true;
                }
                Trace.WriteLine("Message has attachments.");
                HelperMessage message = new HelperMessage
                {
                    discordMessage = e.Message,
                };
                downloader.DownloadMessageList.Add(message);
                if (downloader.DownloadHelperRunning == false || downloader.DownloadHelperTask.IsCompleted == true)
                {
                    // Runs download tasks on background thread.
                    Trace.WriteLine("Starting download helper thread...");
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    downloader.DownloadHelperRunning = true;
                    downloader.DownloadHelperTask = Task.Run(downloader.DownloadHelper);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
            }
            if (e.Message.Content == "ia!clearcq")
            {
                CommandMessageList.Clear();
            }
            if (e.Message.Content.StartsWith("ia!"))
            {
                HelperMessage message = new HelperMessage
                {
                    discordMessage = e.Message,
                };
                CommandMessageList.Add(message);
                if (CommandHelperRunning == false || CommandHelperTask.IsCompleted == true)
                {
                    Trace.WriteLine("Starting command helper thread...");
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    CommandHelperRunning = true;
                    CommandHelperTask =  Task.Run(CommandHelper);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
            }
            return true;
        }

        public void SetRuntimeVars(Program progRef)
        {
            if (mainProgram == null)
            {
                Trace.WriteLine("Setting main program reference for message helper.");
                mainProgram = progRef;
            }
            if (modifyDb == null)
            {
                Trace.WriteLine("Setting database helper reference.");
                if (discordClient == null)
                {
                    discordClient = progRef.discordClient;
                }
                modifyDb = new ModifyDb(DownloadDirectory, TempDirectory, discordClient);
            }
        }

        private async Task CommandHelper()
        {
            Trace.WriteLine("Command helper started.");
            do
            {
                Trace.WriteLine($"{CommandMessageList.Count} message(s) in command queue.");
                HelperMessage currentMessage = CommandMessageList.First();
                CommandMessageList.RemoveAt(0);
                await ProcessCommand(currentMessage);
            } while (CommandMessageList.Count > 0);
            Trace.WriteLine("Command helper exited.");
            CommandHelperRunning = false;
        }

        private async Task ProcessCommand(HelperMessage message)
        {
            // TODO: Add delete image by checksum
            // TODO: Add list images by user (maybe for web interface?)
            // TODO: Move to CommandsNext (didn't expect the commands to get this extensive)
            if (modifyDb.discordClient == null)
            {
                modifyDb.discordClient = mainProgram.discordClient;
            }
            if (message.discordMessage.Content.ToLower().StartsWith("ia!adm."))
            {
                await adminCmd.ProcessAdminCommand(message);
            }
            else if (message.discordMessage.Content.ToLower().StartsWith("ia!bot."))
            {
                await selfCmd.ProcessSelfCommand(message);
            }
            else
            {
                DiscordUser discordUser = null;
                DiscordEmbedBuilder dEmbedBuilder;
                switch (message.discordMessage.Content.ToLower())
                {
                    case "ia!delimgs":
                        discordUser = message.discordMessage.Author;
                        if (discordUser != null)
                        {
                            dEmbedBuilder = new DiscordEmbedBuilder
                            {
                                Title = "Image Archiver Bot",
                                Description = $"Deleting images from {discordUser.Username}#{discordUser.Discriminator}",
                                Author = new DiscordEmbedBuilder.EmbedAuthor(),
                                Color = DiscordColor.CornflowerBlue,
                                Timestamp = DateTimeOffset.UtcNow
                            };
                            dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                            dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                            await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());

                            await modifyDb.DeleteImgsByUser(discordUser);

                            dEmbedBuilder.Description = $"Deleted all images from {discordUser.Username}#{discordUser.Discriminator}.";
                            dEmbedBuilder.Color = DiscordColor.Green;
                            await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                        }
                        break;

                    case "ia!optout":
                        discordUser = message.discordMessage.Author;
                        if (discordUser != null)
                        {
                            dEmbedBuilder = new DiscordEmbedBuilder
                            {
                                Title = "Image Archiver Bot",
                                Description = $"Processing opt-out for {discordUser.Username}#{discordUser.Discriminator}",
                                Author = new DiscordEmbedBuilder.EmbedAuthor(),
                                Color = DiscordColor.CornflowerBlue,
                                Timestamp = DateTimeOffset.UtcNow
                            };
                            dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                            dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                            await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());

                            await modifyDb.ProcessOptOut(discordUser);

                            dEmbedBuilder.Description = $"Opt-out processed for {discordUser.Username}#{discordUser.Discriminator}. Run `ia!delimgs` to clear your saved pictures.";
                            dEmbedBuilder.Color = DiscordColor.Green;
                            await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                        }
                        break;

                    case "ia!optin":
                        discordUser = message.discordMessage.Author;
                        if (discordUser != null)
                        {
                            dEmbedBuilder = new DiscordEmbedBuilder
                            {
                                Title = "Image Archiver Bot",
                                Description = $"Cancelling opt-out for {discordUser.Username}#{discordUser.Discriminator}",
                                Author = new DiscordEmbedBuilder.EmbedAuthor(),
                                Color = DiscordColor.CornflowerBlue,
                                Timestamp = DateTimeOffset.UtcNow
                            };
                            dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                            dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                            await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());

                            await modifyDb.CancelOptOut(await message.discordMessage.Channel.Guild.GetMemberAsync(message.discordMessage.Author.Id));

                            dEmbedBuilder.Color = DiscordColor.Green;
                            dEmbedBuilder.Description = $"Opt-out cancelled for {discordUser.Username}#{discordUser.Discriminator}.";
                            await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                        }
                        break;

                    case "ia!optstatus":
                        discordUser = message.discordMessage.Author;
                        if (discordUser != null)
                        {
                            DiscordDbContext discordDbContext = new DiscordDbContext();
                            bool getOptOut;
                            try
                            {
                                getOptOut = discordDbContext.Users.Where(u => u.IdGuildConcat == discordUser.Id.ToString() + message.discordMessage.Channel.GuildId.ToString()).SingleOrDefault().OptOut;
                            }
                            catch (NullReferenceException)
                            {
                                getOptOut = true;
                            }
                            dEmbedBuilder = new DiscordEmbedBuilder
                            {
                                Title = "Image Archiver Bot",
                                Description = $"Image archive opt-out status: {getOptOut}",
                                Author = new DiscordEmbedBuilder.EmbedAuthor(),
                                Color = DiscordColor.CornflowerBlue,
                                Timestamp = DateTimeOffset.UtcNow
                            };
                            dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                            dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                            await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                        }
                        break;

                    case "ia!cmds":
                        dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Image Archiver Bot Commands",
                            Description = "`ia!delimgs`: \n" +
                                          "Deletes all of your images from the bot.\n" +
                                          "\n" +
                                          "`ia!optout`: \n" +
                                          "Opts out from the bot's data collection and image archiving.\n" +
                                          "\n" +
                                          "`ia!optin`: \n" +
                                          "Cancels your opt-out status (if active).\n" +
                                          "\n" +
                                          "`ia!optstatus`: \n" +
                                          "Checks your opt-out status.",
                            Author = new DiscordEmbedBuilder.EmbedAuthor(),
                            Color = DiscordColor.CornflowerBlue,
                            Timestamp = DateTimeOffset.UtcNow
                        };
                        dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                        dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                        break;

                    default:
                        dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Image Archiver Bot",
                            Description = $"Your command was invalid. Run `ia!cmds` for help.",
                            Author = new DiscordEmbedBuilder.EmbedAuthor(),
                            Color = DiscordColor.Red,
                            Timestamp = DateTimeOffset.UtcNow
                        };
                        dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                        dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                        break;
                }
            }
        }
    }
}
