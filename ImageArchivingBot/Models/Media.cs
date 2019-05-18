using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ImageArchivingBot.Models
{
    public class Media : Message
    {
        [Required]
        public string Url { get; set; }

        [Required]
        public string FileName { get; set; }
        [Required]
        public int FileSize { get; set; }
        [Required]
        public string FileChecksum { get; set; }

        [Required]
        public string LocalFile { get; set; }
    }
}
