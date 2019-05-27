using DSharpPlus;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ImageArchivingBot.SupportLibs
{
    class Helpers
    {
        public DiscordClient discordClient;

        public class HelperMessage
        {
            public DiscordMessage discordMessage { get; set; }
        }

        public Helpers(DiscordClient dClient)
        {
            discordClient = dClient;
        }

        public bool CheckAdminStatus(DiscordMember discordMember)
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

        public async Task<DiscordUser> TryParseDId(ulong discordId, HelperMessage message)
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

        public async Task<DiscordGuild> TryParseDiscordGuildId(ulong discordId, HelperMessage message)
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

        public async Task<DiscordChannel> TryParseDiscordChannelId(ulong discordId, HelperMessage message)
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

        public async Task<List<DiscordChannel>> GetVisibleChannels(DiscordGuild guild)
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

        public List<string> GetRoleNames(DiscordMember member)
        {
            List<string> roleNames = new List<string>();
            IEnumerable<DiscordRole> roles = member.Roles;
            foreach (DiscordRole role in roles)
            {
                roleNames.Add(role.Name);
            }

            return roleNames;
        }
    }
}
