using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TenderToolSyncLambda.Models
{
    public class SupportingDoc
    {
        [Key]
        public Guid SupportingDocID { get; set; }
        public string? Name { get; set; }
        public string? URL { get; set; }

        //FK
        [ForeignKey(nameof(Tender))]
        public Guid TenderID { get; set; }

        [JsonIgnore]
        public BaseTender Tender { get; set; }
    }
}
