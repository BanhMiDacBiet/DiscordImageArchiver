using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ImageArchivingBot.Models
{
    public class Channel
    {
        [Key]
        public ulong Id { get; set; }

        [Required]
        public string Name { get; set; }
        [Required]
        public ulong GuildId { get; set; }
        [Required]
        public bool IsCategory { get; set; }
        [Required]
        public string Topic { get; set; }
        
        public ulong? ParentId { get; set; }
        public string ChildIds { get; set; }
    }
}
