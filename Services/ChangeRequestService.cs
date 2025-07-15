using Microsoft.EntityFrameworkCore;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using ArkhamChangeRequest.Data;
using ArkhamChangeRequest.Models;

namespace ArkhamChangeRequest.Services
{
    public class ChangeRequestService : IChangeRequestService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlobStorageService _blobStorageService;
        private readonly ILogger<ChangeRequestService> _logger;
        private readonly TelemetryClient _telemetryClient;

        public ChangeRequestService(
            ApplicationDbContext context, 
            IBlobStorageService blobStorageService,
            ILogger<ChangeRequestService> logger,
            TelemetryClient telemetryClient)
        {
            _context = context;
            _blobStorageService = blobStorageService;
            _logger = logger;
            _telemetryClient = telemetryClient;
        }

        public async Task<int> CreateChangeRequestAsync(ChangeRequest changeRequest)
        {
            using var operation = _telemetryClient.StartOperation<DependencyTelemetry>("CreateChangeRequest");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Track custom metrics
                _telemetryClient.TrackEvent("ChangeRequestCreationStarted", new Dictionary<string, string>
                {
                    {"RequestorEmail", changeRequest.RequestorEmail},
                    {"ChangeType", changeRequest.ChangeType.ToString()},
                    {"Priority", changeRequest.Priority.ToString()}
                });

                // Add the change request to the database
                _context.ChangeRequests.Add(changeRequest);
                await _context.SaveChangesAsync();

                // Create initial audit trail entry
                await CreateAuditEntryAsync(changeRequest.Id, "Created", null, ChangeRequestStatus.New.ToString(), 
                    changeRequest.RequestorEmail, "Initial change request submission");

                var attachmentCount = 0;
                var totalFileSize = 0L;

                // Handle file uploads if any
                if (changeRequest.Attachments != null && changeRequest.Attachments.Count > 0)
                {
                    var attachments = new List<ChangeRequestAttachment>();

                    foreach (var file in changeRequest.Attachments)
                    {
                        if (file.Length > 0)
                        {
                            // Upload file to blob storage
                            var blobUrl = await _blobStorageService.UploadFileAsync(file);
                            
                            // Create attachment record
                            var attachment = new ChangeRequestAttachment
                            {
                                ChangeRequestId = changeRequest.Id,
                                FileName = file.FileName,
                                BlobUrl = blobUrl,
                                ContentType = file.ContentType ?? "application/octet-stream",
                                FileSize = file.Length,
                                UploadedDate = DateTime.UtcNow
                            };

                            attachments.Add(attachment);
                            attachmentCount++;
                            totalFileSize += file.Length;
                        }
                    }

                    if (attachments.Count > 0)
                    {
                        _context.ChangeRequestAttachments.AddRange(attachments);
                        await _context.SaveChangesAsync();
                    }
                }

                await transaction.CommitAsync();
                stopwatch.Stop();

                // Track success metrics
                _telemetryClient.TrackEvent("ChangeRequestCreated", new Dictionary<string, string>
                {
                    {"ChangeRequestId", changeRequest.Id.ToString()},
                    {"RequestorEmail", changeRequest.RequestorEmail},
                    {"ChangeType", changeRequest.ChangeType.ToString()},
                    {"Priority", changeRequest.Priority.ToString()},
                    {"AttachmentCount", attachmentCount.ToString()}
                }, new Dictionary<string, double>
                {
                    {"ProcessingTimeMs", stopwatch.ElapsedMilliseconds},
                    {"TotalFileSizeMB", totalFileSize / 1024.0 / 1024.0}
                });

                _logger.LogInformation($"Change request {changeRequest.Id} created successfully with {attachmentCount} attachments in {stopwatch.ElapsedMilliseconds}ms");
                
                return changeRequest.Id;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                stopwatch.Stop();

                // Track failure metrics
                _telemetryClient.TrackException(ex, new Dictionary<string, string>
                {
                    {"Operation", "CreateChangeRequest"},
                    {"RequestorEmail", changeRequest.RequestorEmail},
                    {"ChangeType", changeRequest.ChangeType.ToString()},
                    {"Priority", changeRequest.Priority.ToString()}
                }, new Dictionary<string, double>
                {
                    {"ProcessingTimeMs", stopwatch.ElapsedMilliseconds}
                });

                _logger.LogError(ex, $"Error creating change request for {changeRequest.RequestorEmail}");
                throw;
            }
        }

        public async Task<ChangeRequest?> GetChangeRequestByIdAsync(int id)
        {
            using var operation = _telemetryClient.StartOperation<DependencyTelemetry>("GetChangeRequestById");
            try
            {
                var result = await _context.ChangeRequests
                    .Include(cr => cr.AttachmentFiles)
                    .Include(cr => cr.AuditTrail)
                    .FirstOrDefaultAsync(cr => cr.Id == id);

                _telemetryClient.TrackEvent("ChangeRequestRetrieved", new Dictionary<string, string>
                {
                    {"ChangeRequestId", id.ToString()},
                    {"Found", (result != null).ToString()}
                });

                return result;
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex, new Dictionary<string, string>
                {
                    {"Operation", "GetChangeRequestById"},
                    {"ChangeRequestId", id.ToString()}
                });
                throw;
            }
        }

        public async Task<IEnumerable<ChangeRequest>> GetAllChangeRequestsAsync()
        {
            using var operation = _telemetryClient.StartOperation<DependencyTelemetry>("GetAllChangeRequests");
            try
            {
                var results = await _context.ChangeRequests
                    .Include(cr => cr.AttachmentFiles)
                    .OrderByDescending(cr => cr.CreatedDate)
                    .ToListAsync();

                _telemetryClient.TrackEvent("AllChangeRequestsRetrieved", new Dictionary<string, string>
                {
                    {"Count", results.Count.ToString()}
                });

                return results;
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex, new Dictionary<string, string>
                {
                    {"Operation", "GetAllChangeRequests"}
                });
                throw;
            }
        }

        public async Task<bool> UpdateChangeRequestStatusAsync(int id, ChangeRequestStatus status, string? modifiedBy = null, string? comments = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var changeRequest = await _context.ChangeRequests.FindAsync(id);
                if (changeRequest == null) return false;

                var oldStatus = changeRequest.Status;
                changeRequest.Status = status;
                changeRequest.LastModifiedDate = DateTime.UtcNow;
                changeRequest.LastModifiedBy = modifiedBy;

                await _context.SaveChangesAsync();

                // Create audit trail entry
                await CreateAuditEntryAsync(id, "Status Changed", oldStatus.ToString(), status.ToString(), modifiedBy, comments);
                
                await transaction.CommitAsync();

                _telemetryClient.TrackEvent("ChangeRequestStatusUpdated", new Dictionary<string, string>
                {
                    {"ChangeRequestId", id.ToString()},
                    {"OldStatus", oldStatus.ToString()},
                    {"NewStatus", status.ToString()},
                    {"ModifiedBy", modifiedBy ?? "Unknown"}
                });

                _logger.LogInformation($"Change request {id} status updated from {oldStatus} to {status} by {modifiedBy ?? "Unknown"}");
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                
                _telemetryClient.TrackException(ex, new Dictionary<string, string>
                {
                    {"Operation", "UpdateChangeRequestStatus"},
                    {"ChangeRequestId", id.ToString()},
                    {"Status", status.ToString()}
                });

                _logger.LogError(ex, $"Error updating status for change request {id}");
                return false;
            }
        }

        public async Task<bool> UpdateApprovalAsync(int id, string approverName, string approverEmail, string? modifiedBy = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var changeRequest = await _context.ChangeRequests.FindAsync(id);
                if (changeRequest == null) return false;

                var oldStatus = changeRequest.Status;
                changeRequest.ApproverName = approverName;
                changeRequest.ApproverEmail = approverEmail;
                changeRequest.ApprovalDate = DateTime.UtcNow;
                changeRequest.Status = ChangeRequestStatus.Approved;
                changeRequest.LastModifiedDate = DateTime.UtcNow;
                changeRequest.LastModifiedBy = modifiedBy ?? approverEmail;

                await _context.SaveChangesAsync();

                // Create audit trail entry
                await CreateAuditEntryAsync(id, "Approved", oldStatus.ToString(), ChangeRequestStatus.Approved.ToString(), 
                    modifiedBy ?? approverEmail, $"Approved by {approverName} ({approverEmail})");

                await transaction.CommitAsync();

                _telemetryClient.TrackEvent("ChangeRequestApproved", new Dictionary<string, string>
                {
                    {"ChangeRequestId", id.ToString()},
                    {"ApproverName", approverName},
                    {"ApproverEmail", approverEmail},
                    {"PreviousStatus", oldStatus.ToString()}
                });

                _logger.LogInformation($"Change request {id} approved by {approverName} ({approverEmail})");
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                
                _telemetryClient.TrackException(ex, new Dictionary<string, string>
                {
                    {"Operation", "UpdateApproval"},
                    {"ChangeRequestId", id.ToString()},
                    {"ApproverEmail", approverEmail}
                });

                _logger.LogError(ex, $"Error approving change request {id}");
                return false;
            }
        }

        public async Task<IEnumerable<ChangeRequestAudit>> GetAuditTrailAsync(int changeRequestId)
        {
            try
            {
                var auditTrail = await _context.ChangeRequestAudits
                    .Where(a => a.ChangeRequestId == changeRequestId)
                    .OrderByDescending(a => a.ModifiedDate)
                    .ToListAsync();

                _telemetryClient.TrackEvent("AuditTrailRetrieved", new Dictionary<string, string>
                {
                    {"ChangeRequestId", changeRequestId.ToString()},
                    {"AuditEntryCount", auditTrail.Count.ToString()}
                });

                return auditTrail;
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex, new Dictionary<string, string>
                {
                    {"Operation", "GetAuditTrail"},
                    {"ChangeRequestId", changeRequestId.ToString()}
                });
                throw;
            }
        }

        private async Task CreateAuditEntryAsync(int changeRequestId, string action, string? oldStatus, string? newStatus, string? modifiedBy, string? comments)
        {
            var auditEntry = new ChangeRequestAudit
            {
                ChangeRequestId = changeRequestId,
                Action = action,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                ModifiedBy = modifiedBy,
                ModifiedDate = DateTime.UtcNow,
                Comments = comments
            };

            _context.ChangeRequestAudits.Add(auditEntry);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteChangeRequestAsync(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var changeRequest = await _context.ChangeRequests
                    .Include(cr => cr.AttachmentFiles)
                    .FirstOrDefaultAsync(cr => cr.Id == id);

                if (changeRequest == null) return false;

                var attachmentCount = changeRequest.AttachmentFiles.Count;

                // Delete associated blobs
                foreach (var attachment in changeRequest.AttachmentFiles)
                {
                    await _blobStorageService.DeleteFileAsync(attachment.BlobUrl);
                }

                // Delete the change request (cascade will handle attachments)
                _context.ChangeRequests.Remove(changeRequest);
                await _context.SaveChangesAsync();
                
                await transaction.CommitAsync();

                _telemetryClient.TrackEvent("ChangeRequestDeleted", new Dictionary<string, string>
                {
                    {"ChangeRequestId", id.ToString()},
                    {"AttachmentsDeleted", attachmentCount.ToString()}
                });

                _logger.LogInformation($"Change request {id} deleted successfully with {attachmentCount} attachments");
                
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _telemetryClient.TrackException(ex, new Dictionary<string, string>
                {
                    {"Operation", "DeleteChangeRequest"},
                    {"ChangeRequestId", id.ToString()}
                });

                _logger.LogError(ex, $"Error deleting change request {id}");
                return false;
            }
        }

        public async Task<bool> UpdateChangeRequestAsync(ChangeRequest changeRequest)
        {
            using var operation = _telemetryClient.StartOperation<DependencyTelemetry>("UpdateChangeRequest");
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var existingRequest = await _context.ChangeRequests
                    .Include(cr => cr.AttachmentFiles)
                    .FirstOrDefaultAsync(cr => cr.Id == changeRequest.Id);

                if (existingRequest == null)
                {
                    _logger.LogWarning($"Change request with ID {changeRequest.Id} not found for update");
                    return false;
                }

                // Update the properties that are allowed to be edited
                existingRequest.RequestorName = changeRequest.RequestorName;
                existingRequest.RequestorEmail = changeRequest.RequestorEmail;
                existingRequest.ChangeTitle = changeRequest.ChangeTitle;
                existingRequest.ChangeDescription = changeRequest.ChangeDescription;
                existingRequest.AuthorizationServiceAffected = changeRequest.AuthorizationServiceAffected;
                existingRequest.ProposedImplementationDate = changeRequest.ProposedImplementationDate;
                existingRequest.ChangeType = changeRequest.ChangeType;
                existingRequest.Priority = changeRequest.Priority;
                existingRequest.RiskAssessment = changeRequest.RiskAssessment;
                existingRequest.BackoutPlan = changeRequest.BackoutPlan;

                // Handle new attachments if any
                if (changeRequest.Attachments?.Any() == true)
                {
                    foreach (var formFile in changeRequest.Attachments)
                    {
                        if (formFile.Length > 0)
                        {
                            var blobUrl = await _blobStorageService.UploadFileAsync(formFile);
                            
                            var attachment = new ChangeRequestAttachment
                            {
                                ChangeRequestId = existingRequest.Id,
                                FileName = formFile.FileName,
                                BlobUrl = blobUrl,
                                FileSize = formFile.Length,
                                ContentType = formFile.ContentType,
                                UploadedDate = DateTime.UtcNow
                            };

                            existingRequest.AttachmentFiles.Add(attachment);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Create audit trail entry for update
                await CreateAuditEntryAsync(changeRequest.Id, "Updated", null, null, changeRequest.LastModifiedBy, "Change request details updated");

                _telemetryClient.TrackEvent("ChangeRequestUpdated", new Dictionary<string, string>
                {
                    {"ChangeRequestId", changeRequest.Id.ToString()},
                    {"ChangeType", changeRequest.ChangeType.ToString()},
                    {"Priority", changeRequest.Priority.ToString()},
                    {"NewAttachments", (changeRequest.Attachments?.Count ?? 0).ToString()}
                });

                _logger.LogInformation($"Change request {changeRequest.Id} updated successfully");
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _telemetryClient.TrackException(ex, new Dictionary<string, string>
                {
                    {"Operation", "UpdateChangeRequest"},
                    {"ChangeRequestId", changeRequest.Id.ToString()}
                });

                _logger.LogError(ex, $"Error updating change request {changeRequest.Id}");
                return false;
            }
        }

        public async Task<IEnumerable<ChangeRequest>> GetChangeRequestsByEmailAsync(string email)
        {
            using var operation = _telemetryClient.StartOperation<DependencyTelemetry>("GetChangeRequestsByEmail");
            try
            {
                var results = await _context.ChangeRequests
                    .Include(cr => cr.AttachmentFiles)
                    .Where(cr => cr.RequestorEmail.ToLower() == email.ToLower())
                    .OrderByDescending(cr => cr.CreatedDate)
                    .ToListAsync();

                _telemetryClient.TrackEvent("ChangeRequestsByEmailRetrieved", new Dictionary<string, string>
                {
                    {"Email", email},
                    {"Count", results.Count.ToString()}
                });

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving change requests for email {Email}", email);
                _telemetryClient.TrackException(ex);
                operation.Telemetry.Success = false;
                throw;
            }
        }
    }
}
