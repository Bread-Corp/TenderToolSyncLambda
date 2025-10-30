using Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TenderToolSyncLambda.Models
{
    public interface ITender
    {
        Guid TenderID { get; }
        string Title { get; }
        string Status { get; }
        DateTime PublishedDate { get; }
        DateTime ClosingDate { get; }
        DateTime DateAppended { get; }
        string Source { get; }
        List<Tag> Tags { get; }
        string? Description { get; }
        List<SupportingDoc> SupportingDocs { get; }
    }
}
