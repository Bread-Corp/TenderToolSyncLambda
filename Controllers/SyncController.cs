using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenSearch.Client;
using TenderToolSyncLambda.Data;
using TenderToolSyncLambda.Models;

namespace TenderToolSyncLambda.Controllers
{
    [ApiController]
    [Route("sync")]
    public class SyncController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IOpenSearchClient _openSearchClient;
        private readonly ILogger<SyncController> _logger;
        private const string OpenSearchIndexName = "tenders";

        public SyncController(
            ApplicationDbContext dbContext,
            IOpenSearchClient openSearchClient,
            ILogger<SyncController> logger)
        {
            _dbContext = dbContext;
            _openSearchClient = openSearchClient;
            _logger = logger;
        }

        /// <summary>
        /// A re-runnable endpoint to sync all data from RDS to OpenSearch.
        /// It performs an "Upsert" (create or update) for all tenders.
        /// </summary>
        [HttpPost("start")]
        public async Task<IActionResult> StartSync()
        {
            _logger.LogInformation("--- SYNC PROCESS STARTED ---");

            try
            {

                // ### STEP 1: READ ALL DATA FROM RDS ###
                _logger.LogInformation("Reading all tenders and related data from RDS database...");

                var allTenders = await _dbContext.Tenders
                    .Include(t => t.Tags)
                    .Include(t => t.SupportingDocs)
                    .AsNoTracking()
                    .ToListAsync();

                var eTenders = await _dbContext.eTenders.AsNoTracking().ToDictionaryAsync(t => t.TenderID);
                var eskomTenders = await _dbContext.EskomTenders.AsNoTracking().ToDictionaryAsync(t => t.TenderID);
                var sanralTenders = await _dbContext.SanralTenders.AsNoTracking().ToDictionaryAsync(t => t.TenderID);
                var sarsTenders = await _dbContext.SarsTenders.AsNoTracking().ToDictionaryAsync(t => t.TenderID);
                var transnetTenders = await _dbContext.TransnetTenders.AsNoTracking().ToDictionaryAsync(t => t.TenderID);

                _logger.LogInformation("Successfully read {Count} base tenders and all child tables.", allTenders.Count);

                // ### STEP 2: TRANSFORM DATA (FLATTEN) ###
                _logger.LogInformation("Transforming data for OpenSearch...");
                var searchDocuments = new List<TenderSearchDocument>();

                foreach (var tender in allTenders)
                {
                    // 1. Map all BaseTender fields
                    var doc = new TenderSearchDocument
                    {
                        TenderID = tender.TenderID,
                        Title = tender.Title,
                        Status = tender.Status,
                        PublishedDate = tender.PublishedDate,
                        ClosingDate = tender.ClosingDate,
                        DateAppended = tender.DateAppended,
                        Source = tender.Source,
                        Description = tender.Description,
                        AISummary = tender.AISummary,
                        // 2. Map included Tags and Docs
                        Tags = tender.Tags.Select(tag => tag.TagName).ToList(),
                        SupportingDocs = tender.SupportingDocs.Select(sd => new SupportingDocSearchDocument
                        {
                            Name = sd.Name,
                            URL = sd.URL
                        }).ToList()
                    };

                    // 3. Map child-specific fields using our dictionaries
                    switch (tender.Source)
                    {
                        case "eTender" when eTenders.TryGetValue(tender.TenderID, out var et):
                            doc.TenderNumber = et.TenderNumber;
                            doc.Audience = et.Audience;
                            doc.Email = et.Email;
                            doc.OfficeLocation = et.OfficeLocation;
                            doc.Address = et.Address;
                            doc.Province = et.Province;
                            break;
                        case "Eskom" when eskomTenders.TryGetValue(tender.TenderID, out var ek):
                            doc.TenderNumber = ek.TenderNumber;
                            doc.Reference = ek.Reference;
                            doc.Audience = ek.Audience;
                            doc.OfficeLocation = ek.OfficeLocation;
                            doc.Email = ek.Email;
                            doc.Address = ek.Address;
                            doc.Province = ek.Province;
                            break;
                        case "SANRAL" when sanralTenders.TryGetValue(tender.TenderID, out var sn):
                            doc.TenderNumber = sn.TenderNumber;
                            doc.Category = sn.Category;
                            doc.Location = sn.Location;
                            doc.Email = sn.Email;
                            doc.FullTextNotice = sn.FullTextNotice;
                            break;
                        case "SARS" when sarsTenders.TryGetValue(tender.TenderID, out var sa):
                            doc.TenderNumber = sa.TenderNumber;
                            doc.BriefingSession = sa.BriefingSession;
                            break;
                        case "Transnet" when transnetTenders.TryGetValue(tender.TenderID, out var tr):
                            doc.TenderNumber = tr.TenderNumber;
                            doc.Category = tr.Category;
                            doc.Region = tr.Region;
                            doc.ContactPerson = tr.ContactPerson;
                            doc.Email = tr.Email;
                            doc.Institution = tr.Institution;
                            doc.TenderType = tr.TenderType;
                            break;
                    }
                    searchDocuments.Add(doc);
                }
                _logger.LogInformation("Data transformation complete. {Count} documents prepared.", searchDocuments.Count);

                // ### STEP 3: BULK UPLOAD TO OPENSEARCH ###
                _logger.LogInformation("Starting bulk upsert to OpenSearch index '{IndexName}'...", OpenSearchIndexName);

                var batches = searchDocuments.Select((doc, index) => new { doc, index })
                                             .GroupBy(x => x.index / 1000)
                                             .Select(g => g.Select(x => x.doc).ToList());

                int batchNum = 1;
                foreach (var batch in batches)
                {
                    _logger.LogInformation("Uploading batch {BatchNum} of {TotalBatches} ({Count} documents)...",
                        batchNum, batches.Count(), batch.Count);

                    var bulkResponse = await _openSearchClient.BulkAsync(b => b
                        .Index(OpenSearchIndexName)
                        .IndexMany(batch, (descriptor, doc) => descriptor
                            .Id(doc.TenderID.ToString()) // This ID makes it an "Upsert"
                        )
                    );

                    if (bulkResponse.Errors)
                    {
                        _logger.LogError("Bulk upload FAILED for batch {BatchNum}.", batchNum);
                        foreach (var itemWithError in bulkResponse.ItemsWithErrors)
                        {
                            _logger.LogError("Failed to index document {Id}: {Error}", itemWithError.Id, itemWithError.Error);
                        }
                        return StatusCode(500, "Bulk upload failed on one or more items. Check logs.");
                    }
                    _logger.LogInformation("Batch {BatchNum} uploaded successfully.", batchNum++);
                }

                _logger.LogInformation("--- SYNC PROCESS SUCCEEDED ---");
                _logger.LogInformation("Successfully created/updated {Count} documents.", searchDocuments.Count);

                return Ok($"Sync successful. {searchDocuments.Count} documents created/updated.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A fatal error occurred during the sync process.");
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }
}
