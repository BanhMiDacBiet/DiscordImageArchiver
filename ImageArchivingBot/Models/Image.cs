using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ImageArchivingBot.Models
{
    public class Image : Media
    {
        [Key]
        public string IdChecksumConcat { get; set; }

        [Required]
        public int ImageWidth { get; set; }
        [Required]
        public int ImageHeight { get; set; }
    }
}
