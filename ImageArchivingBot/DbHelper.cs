using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using ImageArchivingBot.Models;
using Microsoft.EntityFrameworkCore;

namespace ImageArchivingBot
{
    public class DbUpdater
    {
        private DiscordClient discordClient;

        public DbUpdater(DiscordClient dClient)
        {
            discordClient = dClient;
        }
        public async Task UpdateDbs()
        {
            await UpdateChannels();
            await UpdateUsers();
        }

#if DEBUG
        public async Task RebuildDbs()
        {
            DiscordDbContext discordDbContext = new DiscordDbContext();

            Trace.WriteLine("Rebuilding database.");
            User[] users = await discordDbContext.Users.ToArrayAsync();
            Channel[] channels = await discordDbContext.Channels.ToArrayAsync();

            discordDbContext.RemoveRange(users);
            discordDbContext.RemoveRange(channels);

            Trace.WriteLine("Saving empty users and channels tables.");
            await discordDbContext.SaveChangesAsync();

            Trace.WriteLine("Populating users and channels tables.");
            await UpdateDbs();
            Trace.WriteLine("Rebuilt database.");
        }

        public async Task<bool> ClearMedia(string downloadDirectory, string tempDirectory)
        {
            DiscordDbContext discordDbContext = new DiscordDbContext();

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Confirm you would like to clear all media from the disk and database.");
            Console.WriteLine("Press y to confirm.");
            Console.ResetColor();
            System.ConsoleKeyInfo confirmationKey = Console.ReadKey();
            if (confirmationKey.Key != ConsoleKey.Y)
            {
                return false;
            }

            Trace.WriteLine("Clearing media files from database.");
            Image[] images = await discordDbContext.Images.ToArrayAsync();
            discordDbContext.RemoveRange(images);

            Trace.WriteLine("Saving empty media table.");
            await discordDbContext.SaveChangesAsync();

            Trace.WriteLine("Deleting all media from disk.");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Deleting folder {downloadDirectory}. Press y to continue or any other key to cancel.");
            Console.ResetColor();

            confirmationKey = Console.ReadKey();
            if (confirmationKey.Key == ConsoleKey.Y)
            {
                Directory.Delete(downloadDirectory);
                Directory.CreateDirectory(downloadDirectory);
                Trace.WriteLine("Deleted and recreated media folder.");
            }

            Trace.WriteLine("\nDeleting all temporary media from disk.");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Deleting folder {tempDirectory}. Press y to continue or any other key to cancel.");
            Console.ResetColor();

            confirmationKey = Console.ReadKey();
            if (confirmationKey.Key == ConsoleKey.Y)
            {
                Directory.Delete(tempDirectory);
                Directory.CreateDirectory(tempDirectory);
                Trace.WriteLine("Deleted and recreated temporary folder.");
            }
            Trace.WriteLine("\nMedia clear completed.");
            return true;
        }
#endif

        private async Task UpdateChannels()
        {
            DiscordDbContext discordDbContext = new DiscordDbContext();
            Trace.WriteLine("Beginning channel database update.");
            DiscordGuild[] GuildArray = discordClient.Guilds.Values.ToArray();
            Trace.WriteLine("Retrieved list of guilds.");
            foreach (DiscordGuild guild in GuildArray)
            {
                Trace.WriteLine($"Grabbing list of channels in guild {guild.Name}.");
                IReadOnlyList<DiscordChannel> channelList = await guild.GetChannelsAsync();
                Trace.WriteLine("Retrieved list of channels.");
                DiscordChannel[] channelArr = channelList.ToArray();
                foreach (DiscordChannel channel in channelArr)
                {
                    Trace.WriteLine("Checking database for existing entry.");
                    Channel dbChannel = await discordDbContext.Channels.Where(cid => cid.Id == channel.Id).SingleOrDefaultAsync();
                    Trace.WriteLine("Database check successful. Building new channel object.");
                    Channel newDbChannel = new Channel
                    {
                        Id = channel.Id,
                        Name = channel.Name,
                        GuildId = channel.GuildId,
                        IsCategory = channel.IsCategory,
                        Topic = channel.Topic,
                        ParentId = channel.ParentId
                    };

                    if (channel.IsCategory == true)
                    {
                        Trace.WriteLine($"Channel {channel.Name} is category, building list of children.");
                        DiscordChannel[] chanChildren = channel.Children.ToArray();
                        foreach (DiscordChannel chanChild in chanChildren)
                        {
                            newDbChannel.ChildIds += chanChild.Id + ",";
                        }
                        Trace.WriteLine("Child list complete.");
                    }

                    if (dbChannel == null)
                    {
                        Trace.WriteLine("Channel does not exist in database. Adding new entry.");
                        discordDbContext.Add(newDbChannel);
                    }
                    else if (dbChannel != newDbChannel)
                    {
                        Trace.WriteLine("Updating existing channel entry.");
                        dbChannel.Name = newDbChannel.Name;
                        dbChannel.Topic = newDbChannel.Topic;
                        dbChannel.ParentId = newDbChannel.ParentId;
                        dbChannel.ChildIds = newDbChannel.ChildIds;
                    }
                }
                Trace.WriteLine("Saving database changes.");
                discordDbContext.SaveChanges();
            }
            Trace.WriteLine("Channel update complete.");
        }

        private async Task UpdateUsers()
        {
            DiscordDbContext discordDbContext = new DiscordDbContext();
            Trace.WriteLine("Beginning user database update.");
            DiscordGuild[] GuildArray = discordClient.Guilds.Values.ToArray();
            Trace.WriteLine("Retrieved list of guilds.");
            foreach (DiscordGuild guild in GuildArray)
            {
                Trace.WriteLine("Grabbing list of users in guild.");
                IReadOnlyList<DiscordMember> guildMembers = await guild.GetAllMembersAsync();
                Trace.WriteLine("Retrieved list of members.");
                foreach (DiscordMember member in guildMembers)
                {
                    Trace.WriteLine("Checking database for existing entry.");
                    User dbUser = await discordDbContext.Users.Where(m => m.GuildId == member.Guild.Id && m.Id == member.Id).SingleOrDefaultAsync();
                    if (dbUser != null && dbUser.OptOut == true)
                    {
                        Trace.WriteLine($"User {member.Username}#{member.Discriminator} requested opt-out, skipping.");
                        continue;
                    }
                    Trace.WriteLine("Database check successful. Building new user object.");
                    User newDbUser = new User()
                    {
                        IdGuildConcat = member.Id.ToString() + member.Guild.Id.ToString(),
                        Id = member.Id,
                        GuildId = member.Guild.Id,
                        Username = member.Username,
                        Discriminator = member.Discriminator,
                        DisplayName = member.DisplayName,
                        Colour = member.Color.Value,
                        AvatarUri = member.AvatarUrl
                    };
                    if (dbUser == null)
                    {
                        Trace.WriteLine("User does not exist in database. Adding new entry.");
                        discordDbContext.Add(newDbUser);
                    }
                    else if (dbUser != newDbUser)
                    {
                        Trace.WriteLine("Updating existing user entry.");
                        dbUser.Username = newDbUser.Username;
                        dbUser.Discriminator = newDbUser.Discriminator;
                        dbUser.DisplayName = newDbUser.DisplayName;
                        dbUser.Colour = newDbUser.Colour;
                        dbUser.AvatarUri = newDbUser.AvatarUri;
                    }
                }
                Trace.WriteLine("Saving database changes.");
                discordDbContext.SaveChanges();
            }
            Trace.WriteLine("User update complete.");
        }
    }

    public class ModifyDb
    {
        public string DownloadDirectory;
        public string TempDirectory;
        public DiscordClient discordClient;

        public ModifyDb(string downloadDirectory, string tempDirectory, DiscordClient dClientIn)
        {
            DownloadDirectory = downloadDirectory;
            TempDirectory = tempDirectory;
            discordClient = dClientIn;
        }

        public async Task DeleteImgsByUser(DiscordUser dUser)
        {
            await _DeleteImgsByUser(dUser);
        }

        private async Task _DeleteImgsByUser(DiscordUser dUser)
        {
            DiscordDbContext discordDbContext = new DiscordDbContext();

            Image[] uniqueImages = await discordDbContext.Images.FromSql("SELECT * FROM Images WHERE FileChecksum NOT IN (SELECT FileChecksum FROM (SELECT FileChecksum, SenderId FROM Images GROUP BY FileChecksum, SenderId) GROUP BY FileChecksum HAVING COUNT(FileChecksum) > 1)").Where(i => i.SenderId == dUser.Id).ToArrayAsync();
            Trace.WriteLine("Deleting unique images.");
            foreach (Image image in uniqueImages)
            {
                try
                {
                    File.Delete(DownloadDirectory + Path.DirectorySeparatorChar + image.LocalFile);
                }
                catch (FileNotFoundException) { }
                discordDbContext.Remove(image);
            }

            Image[] sharedImages = await discordDbContext.Images.Where(i => i.SenderId == dUser.Id).ToArrayAsync();
            Trace.WriteLine("Deleting shared images.");
            discordDbContext.RemoveRange(sharedImages);
            Trace.WriteLine("Saving database after deletions.");
            await discordDbContext.SaveChangesAsync();
        }

        public async Task DeleteUser(DiscordUser dUser)
        {
            await _DeleteUser(dUser);
        }

        private async Task _DeleteUser(DiscordUser dUser)
        {
            DiscordDbContext discordDbContext = new DiscordDbContext();

            Trace.WriteLine($"Deleting user {dUser.Username}#{dUser.Discriminator}, ID {dUser.Id}");
            await DeleteImgsByUser(dUser);
            User[] dbUsers = await discordDbContext.Users.Where(u => u.Id == dUser.Id).ToArrayAsync();
            foreach (User user in dbUsers)
            {
                discordDbContext.Remove(user);
            }
            Trace.WriteLine($"Saving database changes for user ID {dUser.Id}");
            await discordDbContext.SaveChangesAsync();
        }

        public async Task ProcessOptOut(DiscordUser dUser)
        {
            await _ProcessOptOut(dUser);
        }

        private async Task _ProcessOptOut(DiscordUser dUser)
        {
            DiscordDbContext discordDbContext = new DiscordDbContext();

            Trace.WriteLine($"Starting opt-out process for {dUser.Username}#{dUser.Discriminator}");
            User[] dbUsers = await discordDbContext.Users.Where(u => u.Id == dUser.Id).ToArrayAsync();
            if (dbUsers.Count() == 0)
            {
                DbUpdater dbUpdater = new DbUpdater(discordClient);
                await dbUpdater.UpdateDbs();
                await _ProcessOptOut(dUser);
                return;
            }
            foreach (User user in dbUsers)
            {
                if (user.OptOut == true)
                {
                    continue;
                }
                else
                {
                    user.OptOut = true;

                    user.Username = null;
                    user.Discriminator = null;
                    user.DisplayName = null;
                    user.Colour = null;
                    user.AvatarUri = null;
                }
            }
            Trace.WriteLine($"Saving opt-out setting for {dUser.Username}#{dUser.Discriminator}");
            await discordDbContext.SaveChangesAsync();
        }

        public async Task CancelOptOut(DiscordMember dMember)
        {
            await _CancelOptOut(dMember);
        }

        private async Task _CancelOptOut(DiscordMember dMember)
        {
            DiscordDbContext discordDbContext = new DiscordDbContext();

            Trace.WriteLine($"Starting opt-in process for {dMember.Username}#{dMember.Discriminator}");
            User[] dbUsers = await discordDbContext.Users.Where(u => u.Id == dMember.Id).ToArrayAsync();
            if (dbUsers.Count() == 0)
            {
                DbUpdater dbUpdater = new DbUpdater(discordClient);
                await dbUpdater.UpdateDbs();
                await _CancelOptOut(dMember);
                return;
            }
            foreach (User user in dbUsers)
            {
                if (user.OptOut == false)
                {
                    continue;
                }
                else
                {
                    Trace.WriteLine("Updating existing user entry.");
                    user.OptOut = false;
                    user.Username = dMember.Username;
                    user.Discriminator = dMember.Discriminator;
                    user.DisplayName = dMember.DisplayName;
                    user.Colour = dMember.Color.Value;
                    user.AvatarUri = dMember.AvatarUrl;
                }
            }
            Trace.WriteLine($"Saving opt-out setting for {dMember.Username}#{dMember.Discriminator}");
            await discordDbContext.SaveChangesAsync();
        }
    }

    public class DiscordDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Channel> Channels { get; set; }
        public DbSet<Image> Images { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=discord.sqlite");
        }
    }
}
