﻿using Kaiyuanshe.OpenHackathon.Server.Models.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Kaiyuanshe.OpenHackathon.Server.Models
{
    /// <summary>
    /// a class indicates a picture
    /// </summary>
    public class PictureInfo
    {
        /// <summary>
        /// name of the picture.
        /// </summary>
        /// <example>vehicle 1</example>
        [MaxLength(128)]
        public string Name { get; set; }

        /// <summary>
        /// rich-text description of the picture. Can be used as alt text.
        /// </summary>
        /// <example>the rear view of the vehicle</example>
        [MaxLength(512)]
        public string Description { get; set; }

        /// <summary>
        /// Uri of the picture
        /// </summary>
        /// <example>https://example.com/a.png</example>
        [Required]
        [MaxLength(256)]
        [AbsoluteUri]
        public string Uri { get; set; }
    }
}
