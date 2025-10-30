using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TenderToolSyncLambda.Models
{
    public class SanralTender : BaseTender
    {
        [Required]
        public string? TenderNumber { get; set; }

        public string? Category { get; set; }

        public string? Location { get; set; }

        public string? Email { get; set; }

        public string? FullTextNotice { get; set; }
    }
}
