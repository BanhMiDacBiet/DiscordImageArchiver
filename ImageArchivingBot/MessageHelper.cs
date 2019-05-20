﻿using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using ImageArchivingBot.Models;
using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using DSharpPlus;
using System.Threading;

namespace ImageArchivingBot
{
    class MessageHelper
    {
        public string DownloadDirectory;
        public string TempDirectory;
        private DiscordClient discordClient;

        public MessageHelper(string downloadDirectory, string tempDirectory, DiscordClient dClient)
        {
            DownloadDirectory = downloadDirectory;
            TempDirectory = tempDirectory;
            Trace.WriteLine("Setting Discord instance for message helper.");
            discordClient = dClient;
        }

        private class HelperMessage
        {
            public DiscordMessage discordMessage { get; set; }
        }

        private HttpClient httpClient = new HttpClient();
        private Program mainProgram = null;
        private DbUpdater dbUpdater = null;
        private ModifyDb modifyDb = null;

        private List<HelperMessage> DownloadMessageList = new List<HelperMessage>();
        private List<HelperMessage> CommandMessageList = new List<HelperMessage>();

        private List<HelperMessage> HistoryMessageList = new List<HelperMessage>();
        private List<DiscordChannel> HistoryChannelList = new List<DiscordChannel>();

        private bool DownloadHelperRunning = false;
        private Task DownloadHelperTask;
        private bool CommandHelperRunning = false;
        private Task CommandHelperTask;
        private bool HistoryHelperRunning = false;
        private Task HistoryHelperTask;
        private bool HistoryDownloadHelperRunning = false;
        private Task HistoryDownloadHelperTask;

        private CancellationToken HistoryCancellationToken;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<bool> IngestMessage(MessageCreateEventArgs e, Program progRef)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            if (discordClient == null)
            {
                Trace.WriteLine("Setting Discord instance for message helper.");
                discordClient = progRef.discordClient;
            }
            DiscordDbContext discordDbContext = new DiscordDbContext();
            if (e.Message.Attachments.Count != 0)
            {
                bool optOut;
                try
                {
/*#if DEBUG
                    Trace.WriteLine("Sleeping 1000ms to simulate slow db query.");
                    System.Threading.Thread.Sleep(1000);
#endif*/
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
                DownloadMessageList.Add(message);
                if (DownloadHelperRunning == false || DownloadHelperTask.IsCompleted == true)
                {
                    // Runs download tasks on background thread.
                    Trace.WriteLine("Starting download helper thread...");
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    DownloadHelperRunning = true;
                    DownloadHelperTask = Task.Run(DownloadHelper);
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
                await ProcessAdminCommand(message);
            }
            else if (message.discordMessage.Content.ToLower().StartsWith("ia!bot."))
            {
                await ProcessSelfCommand(message);
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

        private async Task ProcessAdminCommand(HelperMessage message)
        {
            if (dbUpdater == null)
            {
                Trace.WriteLine("Setting database updater for message helper.");
                if (discordClient == null)
                {
                    discordClient = mainProgram.discordClient;
                }
                dbUpdater = new DbUpdater(discordClient);
            }
            bool admin = CheckAdminStatus(await message.discordMessage.Channel.Guild.GetMemberAsync(message.discordMessage.Author.Id));
            if (admin == true)
            {
                if (message.discordMessage.Content == "ia!adm.db.ref")
                {
                    DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                    {
                        Title = "Image Archiver Bot",
                        Description = $"Refreshing database, please wait...",
                        Author = new DiscordEmbedBuilder.EmbedAuthor(),
                        Color = DiscordColor.CornflowerBlue,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                    dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                    await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());

                    await dbUpdater.UpdateDbs();

                    dEmbedBuilder.Description = $"Database refreshed.";
                    dEmbedBuilder.Color = DiscordColor.Green;
                    await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                }
#if DEBUG
                else if (message.discordMessage.Content == "ia!adm.db.rebuild")
                {
                    DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                    {
                        Title = "Image Archiver Bot",
                        Description = $"Rebuilding database, please wait...",
                        Author = new DiscordEmbedBuilder.EmbedAuthor(),
                        Color = DiscordColor.CornflowerBlue,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                    dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                    await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());

                    await dbUpdater.RebuildDbs();

                    dEmbedBuilder.Description = $"Database rebuilt.";
                    dEmbedBuilder.Color = DiscordColor.Green;
                    await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                }
                else if (message.discordMessage.Content == "ia!adm.md.delall")
                {

                    DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                    {
                        Title = "Image Archiving Bot",
                        Description = "Media clear triggered. This operation requires interaction with the console window.",
                        Color = DiscordColor.Red,
                        Author = new DiscordEmbedBuilder.EmbedAuthor(),
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                    dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                    await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());

                    bool clearStatus = await dbUpdater.ClearMedia(DownloadDirectory, TempDirectory);

                    if (clearStatus == true)
                    {
                        dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Image Archiving Bot",
                            Description = "Media clear completed.",
                            Color = DiscordColor.Green,
                            Author = new DiscordEmbedBuilder.EmbedAuthor(),
                            Timestamp = DateTimeOffset.UtcNow
                        };
                        dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                        dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                    }
                    else
                    {
                        dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Image Archiving Bot",
                            Description = "Media clear cancelled.",
                            Color = DiscordColor.CornflowerBlue,
                            Author = new DiscordEmbedBuilder.EmbedAuthor(),
                            Timestamp = DateTimeOffset.UtcNow
                        };
                        dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                        dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                    }
                }
#endif
                else if (message.discordMessage.Content.ToLower().StartsWith("ia!adm.img.deluid:"))
                {
                    ulong.TryParse(message.discordMessage.Content.Substring(18), out ulong userId);
                    DiscordUser discordUser = await TryParseDId(userId, message);
                    if (discordUser != null)
                    {
                        DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Image Archiver Bot",
                            Description = $"Deleting images from {discordUser.Username}#{discordUser.Discriminator}, ID {discordUser.Id}",
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
                }
                else if (message.discordMessage.Content.ToLower().StartsWith("ia!adm.usr.deluid:"))
                {
                    ulong.TryParse(message.discordMessage.Content.Substring(18), out ulong userId);
                    DiscordUser discordUser = await TryParseDId(userId, message);
                    if (discordUser != null)
                    {
                        DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Image Archiver Bot",
                            Description = $"Deleting user {discordUser.Username}#{discordUser.Discriminator}, ID {discordUser.Id}",
                            Author = new DiscordEmbedBuilder.EmbedAuthor(),
                            Color = DiscordColor.CornflowerBlue,
                            Timestamp = DateTimeOffset.UtcNow
                        };
                        dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                        dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());

                        await modifyDb.DeleteUser(discordUser);

                        dEmbedBuilder.Description = $"Deleted user {discordUser.Username}#{discordUser.Discriminator}.";
                        dEmbedBuilder.Color = DiscordColor.Green;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                    }
                }
                else if (message.discordMessage.Content.ToLower().StartsWith("ia!adm.usr.optoutuid:"))
                {
                    ulong.TryParse(message.discordMessage.Content.Substring(21), out ulong userId);
                    DiscordUser discordUser = await TryParseDId(userId, message);
                    if (discordUser != null)
                    {
                        DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Image Archiver Bot",
                            Description = $"Opting out user {discordUser.Username}#{discordUser.Discriminator}, ID {discordUser.Id}",
                            Author = new DiscordEmbedBuilder.EmbedAuthor(),
                            Color = DiscordColor.CornflowerBlue,
                            Timestamp = DateTimeOffset.UtcNow
                        };
                        dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                        dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());

                        await modifyDb.ProcessOptOut(discordUser);

                        dEmbedBuilder.Description = $"Opted out user ID {discordUser.Id}.";
                        dEmbedBuilder.Color = DiscordColor.Green;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                    }
                }
                else if (message.discordMessage.Content.ToLower().StartsWith("ia!adm.usr.optinuid:"))
                {
                    ulong.TryParse(message.discordMessage.Content.Substring(20), out ulong userId);
                    DiscordUser discordUser = await TryParseDId(userId, message);
                    if (discordUser != null)
                    {
                        DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Image Archiver Bot",
                            Description = $"Opting in user ID {discordUser.Id}.",
                            Author = new DiscordEmbedBuilder.EmbedAuthor(),
                            Color = DiscordColor.CornflowerBlue,
                            Timestamp = DateTimeOffset.UtcNow
                        };
                        dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                        dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());

                        await modifyDb.CancelOptOut(await message.discordMessage.Channel.Guild.GetMemberAsync(discordUser.Id));

                        dEmbedBuilder.Description = $"Opted in user {discordUser.Username}#{discordUser.Discriminator}, ID {discordUser.Id}.";
                        dEmbedBuilder.Color = DiscordColor.Green;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                    }
                }
                else if (message.discordMessage.Content.ToLower().StartsWith("ia!adm.usr.optstatus"))
                {
                    ulong.TryParse(message.discordMessage.Content.Substring(21), out ulong userId);
                    DiscordUser discordUser = await TryParseDId(userId, message);
                    if (discordUser != null)
                    {
                        DiscordDbContext discordDbContext = new DiscordDbContext();
                        bool getOptOut;
                        string reason = "";
                        try
                        {
                            getOptOut = discordDbContext.Users.Where(u => u.IdGuildConcat == discordUser.Id.ToString() + message.discordMessage.Channel.GuildId.ToString()).SingleOrDefault().OptOut;
                        }
                        catch (NullReferenceException)
                        {
                            getOptOut = true;
                            reason = "(Not in database)";
                        }
                        DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Image Archiver Bot",
                            Description = $"Opt-out status for {discordUser.Username}#{discordUser.Discriminator}: {getOptOut} {reason}",
                            Author = new DiscordEmbedBuilder.EmbedAuthor(),
                            Color = DiscordColor.CornflowerBlue,
                            Timestamp = DateTimeOffset.UtcNow
                        };
                        dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                        dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                        await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                    }
                }
                else if (message.discordMessage.Content.ToLower() == "ia!adm.cmds")
                {
                    DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                    {
                        Title = "Image Archiver Bot Administrative Commands",
                        Description = "`ia!adm.db.ref`: \n" +
                        "Refreshes the bot's database of users and channels.\n" +
                        "\n" +
#if DEBUG
                        "`ia!adm.db.rebuild`:\n" +
                        "Rebuilds the bot's database from scratch. Debugging only.\n" +
                        "\n" +
                        "`ia!adm.md.delall`:\n" +
                        "Deletes all media files from disk and database. Debugging only.\n" +
                        "\n" +
#endif
                        "`ia!adm.img.deluid:[user ID]`:\n" +
                        "Delete all images from the specified user ID.\n" +
                         "\n" +
                        "`ia!adm.usr.deluid:[user ID]`:\n" +
                        "Deletes the user from the database and clears all of their saved images. Will *not* opt-out (all future images will still be processed).\n" +
                         "\n" +
                         "`ia!adm.usr.optoutuid:[user ID]`:\n" +
                         "Forcibly opts out the specified user. Will not clear their saved images.\n" +
                        "\n" +
                        "`ia!adm.usr.optinuid:[user ID]`:\n" +
                        "Forcibly opts in the specified user.\n" +
                        "\n" +
                        "`ia!adm.usr.optstatus:[user ID]`:\n" +
                        "Checks opt-out status for the specified user.",
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
                        Description = $"Your command was invalid. Run `ia!adm.cmds` for help.",
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

        private async Task ProcessSelfCommand(HelperMessage message)
        {
            // TODO: Diagnostic commands (restart bot, etc)
            if (dbUpdater == null)
            {
                Trace.WriteLine("Setting database updater for message helper.");
                if (discordClient == null)
                {
                    discordClient = mainProgram.discordClient;
                }
                dbUpdater = new DbUpdater(discordClient);
            }
            bool admin = CheckAdminStatus(await message.discordMessage.Channel.Guild.GetMemberAsync(message.discordMessage.Author.Id));
            if (admin == true)
            {
                if (message.discordMessage.Content == "ia!bot.ping")
                {
                    await message.discordMessage.RespondAsync($"Pong. Socket latency: {discordClient.Ping}ms");
                }
                else if (message.discordMessage.Content.ToLower().StartsWith("ia!bot.chn.info:"))
                {
                    ulong.TryParse(message.discordMessage.Content.Substring(16), out ulong channelId);
                    DiscordChannel discordChannel = await TryParseDiscordChannelId(channelId, message);

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
                    DiscordChannel channel = await TryParseDiscordChannelId(channelId, message);

                    List<DiscordChannel> visibleChannels = await GetVisibleChannels(channel.Guild);
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
                    }

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

                    activeScrapers.Add("**[ID]: Channel Name (Guild Name)**");
                    for (int i = 0; i < HistoryChannelList.Count; i++)
                    {
                        activeScrapers.Add($"[{i}]: {HistoryChannelList[i].Name} ({HistoryChannelList[i].Guild.Name})");
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

                    DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                    {
                        Title = "Channel History Scraper Queue",
                        Description = $"{string.Join("\n", activeScrapers)}\n" +
                                      $"Currently scraping: {scraperStatus}\n" +
                                      $"Scraper exited successfully: {scraperSuccess}",
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
                    DiscordGuild discordGuild = await TryParseDiscordGuildId(guildId, message);

                    List<DiscordChannel> visibleChannels = await GetVisibleChannels(discordGuild);

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
                                      $"Roles: {string.Join(", ", GetRoleNames(botMember))}" +
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
                    if (DownloadHelperTask != null)
                    {
                        DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                        {
                            Title = "Download Queue Status",
                            Description = $"Current items waiting in download queue: {DownloadMessageList.Count}\n" +
                                          $"Download helper running: {!DownloadHelperTask.IsCompleted}\n" +
                                          $"Download helper successfully completed: {DownloadHelperTask.IsCompletedSuccessfully}",
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
                            Description = $"Current items waiting in download queue: {DownloadMessageList.Count}\n" +
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
                    DownloadMessageList.Clear();

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
                    if (DownloadHelperRunning == false || DownloadHelperTask.IsCompleted == true)
                    {
                        // Runs download tasks on background thread.
                        Trace.WriteLine("Starting download helper thread...");
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        DownloadHelperRunning = true;
                        DownloadHelperTask = Task.Run(DownloadHelper);
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

        private bool CheckAdminStatus(DiscordMember discordMember)
        {
#if DEBUG
            if (discordMember.Id == 245108919020552192)
            {
                Console.WriteLine("Overriding administrator check for testing purposes.");
                return true;
            }
#endif

            if (discordMember.IsOwner == true)
            {
                return true;
            }
            IEnumerable<DiscordRole> discordRoles = discordMember.Roles;
            foreach (DiscordRole role in discordRoles)
            {
                if (role.Permissions.HasPermission(Permissions.Administrator))
                {
                    return true;
                }
            }
            return false;
        }

        private async Task<DiscordUser> TryParseDId(ulong discordId, HelperMessage message)
        {
            // Try to parse a Discord user from a given ulong.
            DiscordUser discordUser = null;
            if (discordId != 0)
            {
                try
                {
                    discordUser = await discordClient.GetUserAsync(discordId);
                    return discordUser;
                }
                catch
                {
                    DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                    {
                        Title = "Image Archiver Bot",
                        Description = $"Error: Could not get user object from ID, please check the ID is valid and try again.",
                        Author = new DiscordEmbedBuilder.EmbedAuthor(),
                        Color = DiscordColor.Red,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                    dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                    await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                    return null;
                }
            }
            else
            {
                DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                {
                    Title = "Image Archiver Bot",
                    Description = $"Error: Could not parse user ID as ulong, please check the ID is valid and try again.",
                    Author = new DiscordEmbedBuilder.EmbedAuthor(),
                    Color = DiscordColor.Red,
                    Timestamp = DateTimeOffset.UtcNow
                };
                dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                return null;
            }
        }

        private async Task<DiscordGuild> TryParseDiscordGuildId(ulong discordId, HelperMessage message)
        {
            // Try to parse a Discord guild from a given ulong.
            DiscordGuild discordGuild = null;
            if (discordId != 0)
            {
                try
                {
                    discordGuild = await discordClient.GetGuildAsync(discordId);
                    return discordGuild;

                }
                catch
                {
                    DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                    {
                        Title = "Image Archiver Bot",
                        Description = $"Error: Could not get guild object from ID, please check the ID is valid and try again.",
                        Author = new DiscordEmbedBuilder.EmbedAuthor(),
                        Color = DiscordColor.Red,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                    dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                    await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                    return null;
                }
            }
            else
            {
                DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                {
                    Title = "Image Archiver Bot",
                    Description = $"Error: Could not parse guild ID as ulong, please check the ID is valid and try again.",
                    Author = new DiscordEmbedBuilder.EmbedAuthor(),
                    Color = DiscordColor.Red,
                    Timestamp = DateTimeOffset.UtcNow
                };
                dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build()); return null;
            }
        }

        private async Task<DiscordChannel> TryParseDiscordChannelId(ulong discordId, HelperMessage message)
        {
            // Try to parse a Discord guild from a given ulong.
            DiscordChannel discordChannel = null;
            if (discordId != 0)
            {
                try
                {
                    discordChannel = await discordClient.GetChannelAsync(discordId);
                    return discordChannel;
                }
                catch
                {
                    DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                    {
                        Title = "Image Archiver Bot",
                        Description = $"Error: Could not get channel object from ID, please check the ID is valid and try again.",
                        Author = new DiscordEmbedBuilder.EmbedAuthor(),
                        Color = DiscordColor.Red,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                    dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                    await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build());
                    return null;
                }
            }
            else
            {
                DiscordEmbedBuilder dEmbedBuilder = new DiscordEmbedBuilder
                {
                    Title = "Image Archiver Bot",
                    Description = $"Error: Could not parse channel ID as ulong, please check the ID is valid and try again.",
                    Author = new DiscordEmbedBuilder.EmbedAuthor(),
                    Color = DiscordColor.Red,
                    Timestamp = DateTimeOffset.UtcNow
                };
                dEmbedBuilder.Author.Name = message.discordMessage.Author.Username + "#" + message.discordMessage.Author.Discriminator;
                dEmbedBuilder.Author.IconUrl = message.discordMessage.Author.AvatarUrl;
                await message.discordMessage.RespondAsync("", false, dEmbedBuilder.Build()); return null;
            }
        }

        private async Task<List<DiscordChannel>> GetVisibleChannels(DiscordGuild guild)
        {
            List<DiscordChannel> visibleChannels = new List<DiscordChannel>();
            IReadOnlyList<DiscordChannel> channels = await guild.GetChannelsAsync();
            foreach (DiscordChannel channel in channels)
            {
                if (channel.Guild.CurrentMember.PermissionsIn(channel).HasPermission(Permissions.AccessChannels))
                {
                    visibleChannels.Add(channel);
                }
            }
            return visibleChannels;
        }

        private List<string> GetRoleNames(DiscordMember member)
        {
            List<string> roleNames = new List<string>();
            IEnumerable<DiscordRole> roles = member.Roles;
            foreach (DiscordRole role in roles)
            {
                roleNames.Add(role.Name);
            }

            return roleNames;
        }

        // Start of download section.
        private async Task DownloadHelper()
        {
            Trace.WriteLine("Download helper started.");
            do
            {
                Trace.WriteLine($"{DownloadMessageList.Count} message(s) in download queue.");
                HelperMessage currentMessage = DownloadMessageList.First();
                await DownloadAttachments(currentMessage);
                DownloadMessageList.RemoveAt(0);
            } while (DownloadMessageList.Count > 0);
            Trace.WriteLine("Download helper exited.");
            DownloadHelperRunning = false;
        }

        private async Task<bool> DownloadAttachments(HelperMessage message)
        {
            IReadOnlyList<DiscordAttachment> discordAttachments = message.discordMessage.Attachments;
            foreach (DiscordAttachment attachment in discordAttachments)
            {
                Trace.WriteLine("Checking if attachment is image.");
                if (attachment.Height != 0 || attachment.Width != 0)
                {
                    Trace.WriteLine("Attachment is image.");
                    Trace.WriteLine("Starting attachment download process.");
                    Image imageRef = await DownloadAttachment(attachment);
                    Trace.WriteLine("Download process complete. Processing attachment metadata.");
                    await ProcessAttachment(message, attachment, imageRef);
                }
            }
            return true;
        }

        private async Task<Image> DownloadAttachment(DiscordAttachment attachment)
        {
            byte[] FileSha256;
            string Sha256HashHex;
            string newFileName;
            Trace.WriteLine("Downloading attachment to stream.");
            using (Stream attachmentFile = await httpClient.GetStreamAsync(attachment.Url).ConfigureAwait(false))
            using (MemoryStream memoryStream = new MemoryStream())
            {
                await attachmentFile.CopyToAsync(memoryStream);
                using (SHA256 sha256 = SHA256.Create())
                {
                    Trace.WriteLine("Hashing file from memory stream.");
                    memoryStream.Position = 0;
                    FileSha256 = sha256.ComputeHash(memoryStream);
                }

                Sha256HashHex = StackOverflow.ByteArrayToHexViaLookup32(FileSha256);

                newFileName = Sha256HashHex + "." + attachment.FileName.Split('.').Last<string>();

                try
                {
                    if (File.Exists(DownloadDirectory + newFileName))
                    {
                        discordClient.DebugLogger.LogMessage(LogLevel.Info, "Download Helper", $"File already exists: {newFileName}", DateTime.Now);
                    }
                    else
                    {
                        Trace.WriteLine($"Creating new file at {DownloadDirectory + Path.DirectorySeparatorChar + newFileName}");
                        using (FileStream file = File.Create(DownloadDirectory + Path.DirectorySeparatorChar + newFileName))
                        {
                            memoryStream.WriteTo(file);
                        }
                    }
                }
                catch (Exception e)
                {
                    discordClient.DebugLogger.LogMessage(LogLevel.Error, "Download Helper", $"Exception occured: {e.GetType()}: {e.Message}", DateTime.Now);
                    return null;
                }
            }

            Trace.WriteLine("Scaffolding new image object...");
            Image image = new Image
            {
                FileChecksum = Sha256HashHex,
                LocalFile = newFileName
            };

            return image;
        }

        private async Task<bool> ProcessAttachment(HelperMessage message, DiscordAttachment attachment, Image image)
        {
            DiscordDbContext discordDbContext = new DiscordDbContext();

            Trace.WriteLine("Beginning to process image metadata.");
            image.ImageWidth = attachment.Width;
            image.ImageHeight = attachment.Height;
            image.FileName = attachment.FileName;
            image.FileSize = attachment.FileSize;
            image.Url = attachment.Url;

            image.Id = message.discordMessage.Id;
            image.IdChecksumConcat = message.discordMessage.Id + image.FileChecksum;

            image.SenderId = message.discordMessage.Author.Id;
            image.SenderUsername = message.discordMessage.Author.Username;
            image.SenderDiscriminator = message.discordMessage.Author.Discriminator;

            image.ChannelId = message.discordMessage.ChannelId;
            image.ChannelName = message.discordMessage.Channel.Name;

            image.MessageContent = message.discordMessage.Content;
            image.Timestamp = message.discordMessage.Timestamp.ToUnixTimeSeconds();

            // Should only match if scraping historical messages.
            if (discordDbContext.Images.Where(i => i.IdChecksumConcat == image.IdChecksumConcat).FirstOrDefault() == null)
            {
                discordDbContext.Add(image);

                try
                {
                    await discordDbContext.SaveChangesAsync();
                    Trace.WriteLine("Image metadata saved in database.");
                }
                catch (Exception e)
                {
                    discordClient.DebugLogger.LogMessage(LogLevel.Error, "Download Helper", $"Exception occured: {e.GetType()}: {e.Message}", DateTime.Now);
                    return false;
                }
                return true;
            }
            else
            {
                Trace.WriteLine("Duplicate entry exists in database.");
                return false;
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
                await DownloadAttachments(currentMessage);
                HistoryMessageList.RemoveAt(0);
            } while (HistoryMessageList.Count > 0);
            Trace.WriteLine("History download helper exited.");
            HistoryDownloadHelperRunning = false;
        }
    }
}
