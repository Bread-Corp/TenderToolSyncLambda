namespace TenderToolSyncLambda.Models
{
    /// <summary>
    /// Represents the "flattened" document that will be stored in OpenSearch.
    /// It combines data from BaseTender, all child tables, tags, and docs.
    /// Null fields will be ignored by OpenSearch.
    /// </summary>
    public class TenderSearchDocument
    {
        // --- From BaseTender ---
        public Guid TenderID { get; set; }
        public string? Title { get; set; }
        public string? Status { get; set; }
        public DateTime PublishedDate { get; set; }
        public DateTime ClosingDate { get; set; }
        public DateTime DateAppended { get; set; }
        public string Source { get; set; }
        public string? Description { get; set; }
        public string? AISummary { get; set; }

        // --- From Tag (Flattened) ---
        public List<string> Tags { get; set; } = new List<string>();

        // --- From SupportingDoc (Flattened) ---
        public List<SupportingDocSearchDocument> SupportingDocs { get; set; } = new List<SupportingDocSearchDocument>();

        // --- Common Child Fields (Eskom, eTender, Sanral, Sars, Transnet) ---
        public string? TenderNumber { get; set; }
        public string? Category { get; set; } // Sanral, Transnet
        public string? Email { get; set; } // Eskom, eTender, Sanral, Transnet

        // --- Eskom / eTender Fields ---
        public string? Audience { get; set; }
        public string? OfficeLocation { get; set; }
        public string? Address { get; set; }
        public string? Province { get; set; }

        // --- Eskom Only ---
        public string? Reference { get; set; }

        // --- Sanral Only ---
        public string? Location { get; set; }
        public string? FullTextNotice { get; set; }

        // --- Sars Only ---
        public string? BriefingSession { get; set; }

        // --- Transnet Only ---
        public string? Region { get; set; } // Transnet has its own 'Region'
        public string? ContactPerson { get; set; }
        public string? Institution { get; set; }
        public string? TenderType { get; set; }
    }   
}
