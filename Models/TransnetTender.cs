using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TenderToolSyncLambda.Models
{
    public class TransnetTender : BaseTender
    {
        [Required]
        public string? TenderNumber { get; set; }

        public string? Category { get; set; }

        public string? Region { get; set; }

        public string? ContactPerson { get; set; }

        public string? Email { get; set; }

        public string? Institution { get; set; }

        public string? TenderType { get; set; }
    }
}
