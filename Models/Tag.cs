using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TenderToolSyncLambda.Models
{
    public class Tag
    {
        [Key]
        public Guid TagID { get; set; }

        [Required]
        public string TagName { get; set; }

        public List<BaseTender> Tenders { get; set; } = new();
    }
}
