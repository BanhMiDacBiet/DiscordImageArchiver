using DSharpPlus;
using DSharpPlus.Entities;
using ImageArchivingBot.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static ImageArchivingBot.SupportLibs.Helpers;

namespace ImageArchivingBot.SupportLibs
{
    class Downloader
    {
        private HttpClient httpClient = new HttpClient();
        private DiscordClient discordClient;

        public List<HelperMessage> DownloadMessageList = new List<HelperMessage>();

        public bool DownloadHelperRunning = false;
        public Task DownloadHelperTask;

        public string DownloadDirectory { get; set; }
        public string TempDirectory { get; set; }

        public Downloader(DiscordClient dClient)
        {
            discordClient = dClient;
        }

        // Start of download section.
        public async Task DownloadHelper()
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

        public async Task<bool> DownloadAttachments(HelperMessage message)
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
                    if (imageRef == null)
                    {
                        continue;
                    }
                    Trace.WriteLine("Download process complete. Processing attachment metadata.");
                    await ProcessAttachment(message, attachment, imageRef);
                }
            }
            return true;
        }

        private async Task<Image> DownloadAttachment(DiscordAttachment attachment)
        {
            DiscordDbContext discordDbContext = new DiscordDbContext();

            // Should only match when downloading historical messages.
            if (discordDbContext.Images.Where(i => i.Url == attachment.Url).FirstOrDefault() != null)
            {
                Trace.WriteLine("Attachment is already in database. Skipping.");
                return null;
            }

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
    }
}
