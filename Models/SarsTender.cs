using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TenderToolSyncLambda.Models
{
    public class SarsTender : BaseTender
    {
        [Required]
        public string? TenderNumber { get; set; }

        public string? BriefingSession { get; set; }
    }
}
