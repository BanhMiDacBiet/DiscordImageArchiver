using DSharpPlus;
using DSharpPlus.Entities;
using ImageArchivingBot.SupportLibs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ImageArchivingBot.SupportLibs.Helpers;

namespace ImageArchivingBot.Commands
{
    class AdminCmds
    {
        private DiscordClient discordClient;
        private Program mainProgram;

        private DbUpdater dbUpdater = null;
        private Helpers helper = null;
        private ModifyDb modifyDb = null;

        string DownloadDirectory;
        string TempDirectory;

        public AdminCmds(DiscordClient dClient, Program progRef, string downloadDirectory, string tempDirectory)
        {
            discordClient = dClient;
            mainProgram = progRef;
            modifyDb = new ModifyDb(downloadDirectory, tempDirectory, discordClient);
            DownloadDirectory = downloadDirectory;
            TempDirectory = tempDirectory;
        }

        public async Task ProcessAdminCommand(HelperMessage message)
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
            if (helper == null)
            {
                helper = new Helpers(discordClient);
            }
            bool admin = helper.CheckAdminStatus(await message.discordMessage.Channel.Guild.GetMemberAsync(message.discordMessage.Author.Id));
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
                    DiscordUser discordUser = await helper.TryParseDId(userId, message);
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
                    DiscordUser discordUser = await helper.TryParseDId(userId, message);
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
                    DiscordUser discordUser = await helper.TryParseDId(userId, message);
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
                    DiscordUser discordUser = await helper.TryParseDId(userId, message);
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
                    DiscordUser discordUser = await helper.TryParseDId(userId, message);
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
    }
}
