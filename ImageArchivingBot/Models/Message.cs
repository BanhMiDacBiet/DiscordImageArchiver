using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ImageArchivingBot.Models
{
    public class Message
    {
        [Required]
        public ulong Id { get; set; }
        [Required]
        public long Timestamp { get; set; }

        [Required]
        public ulong SenderId { get; set; }
        [Required]
        public string SenderUsername { get; set; }
        [Required]
        public string SenderDiscriminator { get; set; }

        [Required]
        public ulong ChannelId { get; set; }
        [Required]
        public string ChannelName { get; set; }

        [Required]
        public string MessageContent { get; set; }

        public string EditTimestamps { get; set; }
        public string EditContent { get; set; }
    }
}
