using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ImageArchivingBot.Models
{
    public class User
    {
        [Key]
        public string IdGuildConcat { get; set; }

        [Required]
        public ulong GuildId { get; set; }
        [Required]
        public ulong Id { get; set; }

        [Required]
        public bool OptOut { get; set; }
        public string Username { get; set; }
        public string Discriminator { get; set; }
        public string DisplayName { get; set; }
        public int? Colour { get; set; }
        public string AvatarUri { get; set; }
    }
}
