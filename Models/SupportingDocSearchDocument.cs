namespace TenderToolSyncLambda.Models
{
    /// <summary>
    /// A simple, flat model for nested supporting documents in OpenSearch.
    /// </summary>
    public class SupportingDocSearchDocument
    {
        public string? Name { get; set; }
        public string? URL { get; set; }
    }
}
