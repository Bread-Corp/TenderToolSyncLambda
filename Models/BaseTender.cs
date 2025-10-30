using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TenderToolSyncLambda.Models
{
    public abstract class BaseTender : ITender
    {
        [Key]
        [Required]
        public Guid TenderID { get; set; } //Initialised on create.

        [Required]
        public string Title { get; set; }

        [Required]
        public string Status { get; set; } //Set on create.

        [Required]
        public DateTime PublishedDate { get; set; }

        [Required]
        public DateTime ClosingDate { get; set; }

        [Required]
        public DateTime DateAppended { get; set; }

        [Required]
        [JsonProperty("Source")]
        public string Source { get; set; }

        public List<Tag> Tags { get; set; } = new();

        public string? Description { get; set; }

        public string? AISummary { get; set; } //Another field for AI summary

        public List<SupportingDoc> SupportingDocs { get; set; } = new();
    }
}
