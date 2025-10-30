using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TenderToolSyncLambda.Models
{
    public class EskomTender : BaseTender
    {
        [Required]
        public string? TenderNumber { get; set; }

        public string? Reference { get; set; }

        public string? Audience { get; set; }

        public string? OfficeLocation { get; set; }

        public string? Email { get; set; }

        public string? Address { get; set; }

        public string? Province { get; set; }
    }
}
