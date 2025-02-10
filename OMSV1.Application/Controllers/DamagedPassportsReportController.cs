using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using OMSV1.Domain.Entities.DamagedPassport;
using OMSV1.Domain.Interfaces;
using OMSV1.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace OMSV1.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DamagedPassportsReportController : ControllerBase
    {
        private readonly ILogger<DamagedPassportsReportController> _logger;
        private readonly IDamagedPassportRepository _damagedPassportRepository;
        private readonly IDamagedPassportService _damagedPassportService;
        private readonly IEmailReportRepository _emailReportRepository;
        private readonly IEmailService _emailService;

        public DamagedPassportsReportController(
            ILogger<DamagedPassportsReportController> logger,
            IDamagedPassportRepository damagedPassportRepository,
            IDamagedPassportService damagedPassportService,
            IEmailReportRepository emailReportRepository,
            IEmailService emailService)
        {
            _logger = logger;
            _damagedPassportRepository = damagedPassportRepository;
            _damagedPassportService = damagedPassportService;
            _emailReportRepository = emailReportRepository;
            _emailService = emailService;
        }

        [HttpPost("zip")]
        [Authorize(Policy = "RequireSuperAdminRole")]

        public async Task<IActionResult> GenerateAndSendDailyDamagedPassportsZipArchiveReport([FromBody] DamagedPassportsReportRequest request)
        {
            try
            {
                _logger.LogInformation("Starting in-memory daily damaged passports archive generation");

                // Use the report date provided in the POST body (date-only).
                var reportDate = request.ReportDate.Date;

                // Retrieve passports created on the specified date.
                // (Ensure your repository uses a date range query such as:
                //   DateCreated >= reportDate && DateCreated < reportDate.AddDays(1))
                var damagedPassports = await _damagedPassportRepository.GetDamagedPassportsByDateAsync(reportDate);

                if (damagedPassports == null || !damagedPassports.Any())
                {
                    _logger.LogWarning($"No damaged passports found for {reportDate:yyyy-MM-dd}");
                    return NotFound($"No damaged passports found for {reportDate:yyyy-MM-dd}");
                }

                // Create a MemoryStream to hold the ZIP archive.
                using var archiveStream = new MemoryStream();

                // Create a ZIP archive within the MemoryStream.
                using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    // Group passports by their DamageType name.
                    var groups = damagedPassports.GroupBy(dp => dp.DamagedType.Name);

                    foreach (var group in groups)
                    {
                        // Use the damage type name as the folder name inside the ZIP.
                        string folderName = group.Key;

                        foreach (var passport in group)
                        {
                            // Retrieve the file path for the attachment using your custom logic.
                            string filePath = GetAttachmentFilePath(passport);

                            if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
                            {
                                string fileName = Path.GetFileName(filePath);

                                // Build an entry name that places the file within a folder (damage type name).
                                string entryName = $"{folderName}/{fileName}";

                                // Create an entry in the ZIP archive.
                                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

                                // Open the entry stream and copy the file content.
                                using var entryStream = entry.Open();
                                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                                await fileStream.CopyToAsync(entryStream);
                            }
                            else
                            {
                                _logger.LogWarning($"No file found for passport ID {passport.Id} using pattern: DamagedPassport_{passport.Id}_*.jpg");
                            }
                        }
                    }
                }

                // Reset the MemoryStream position to the beginning.
                archiveStream.Position = 0;

                // Convert the MemoryStream to a byte array.
                byte[] zipBytes = archiveStream.ToArray();

                // Prepare a simple email body.
                string emailBody = "Please find attached the daily damaged passports archive.";

                // Retrieve recipients for the "Daily Passports Archives" report type.
                var recipients = await _emailReportRepository.GetEmailsByReportTypeAsync("Daily Passports Archives");

                if (recipients != null && recipients.Any())
                {
                    // For each recipient, send an email with the ZIP archive attached.
                    foreach (var recipient in recipients)
                    {
                        await _emailService.SendEmailWithAttachmentAsync(
                            from: "omc@scopesky.iq",
                            to: recipient,
                            subject: $"Daily Damaged Passports Archive - {reportDate:yyyyMMdd}",
                            body: emailBody,
                            attachmentBytes: zipBytes,
                            attachmentName: $"DamagedPassports_{reportDate:yyyyMMdd}.zip"
                        );

                        _logger.LogInformation($"Daily damaged passports archive email sent successfully to {recipient}.");
                    }
                }
                else
                {
                    _logger.LogWarning("No recipients found for report type 'Daily Passports Archives'");
                }

                // Return a success message.
                return Ok("Damaged passports archive generated and emails sent successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating daily damaged passports archive report");
                return StatusCode(500, "Internal server error");
            }
        }

        // Helper method for determining the attachment file path.
private string GetAttachmentFilePath(Domain.Entities.DamagedPassport.DamagedPassport passport)
{
    // Define the base folder where the images are stored.
    string baseFolder = @"\\172.16.108.26\samba";

    // Get directories that start with "damagedpassport" (case insensitive on many file systems).
    string[] directories = Directory.GetDirectories(baseFolder, "damagedpassport*", SearchOption.TopDirectoryOnly);

    // Loop through each matching directory.
    foreach (string directory in directories)
    {
        // Enumerate all files in the current directory.
       var file = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
    .FirstOrDefault(f => Path.GetFileName(f)
        .IndexOf(passport.Id.ToString(), StringComparison.OrdinalIgnoreCase) >= 0);


        if (!string.IsNullOrEmpty(file))
        {
            return file; // Return the first match found.
        }
    }

    // Log a warning that no file was found.
    _logger?.LogWarning($"No file found for passport ID {passport.Id}");
    return string.Empty;
}



    }
}
