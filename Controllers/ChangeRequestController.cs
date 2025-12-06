using Microsoft.AspNetCore.Mvc;
using ArkhamChangeRequest.Models;
using ArkhamChangeRequest.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace ArkhamChangeRequest.Controllers
{
    public class ChangeRequestController : Controller
    {
        private readonly IChangeRequestService _changeRequestService;
        private readonly IUserService _userService;
        private readonly ILogger<ChangeRequestController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IAuthorizationService _authorizationService;
        private readonly IBlobStorageService _blobStorageService;

        public ChangeRequestController(
            IChangeRequestService changeRequestService,
            IUserService userService,
            ILogger<ChangeRequestController> logger,
            IConfiguration configuration,
            IAuthorizationService authorizationService,
            IBlobStorageService blobStorageService)
        {
            _changeRequestService = changeRequestService;
            _userService = userService;
            _logger = logger;
            _configuration = configuration;
            _authorizationService = authorizationService;
            _blobStorageService = blobStorageService;
        }

        public async Task<IActionResult> Index()
        {
            var changeRequests = await _changeRequestService.GetAllChangeRequestsAsync();
            return View(changeRequests);
        }

        public IActionResult Create()
        {
            // Pre-fill the form with the authenticated user's info
            var model = new ChangeRequest
            {
                RequestorName = _userService.GetUserName(),
                RequestorEmail = _userService.GetUserEmail()
            };
            
            ViewBag.ServiceOptions = GetServiceOptions();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ChangeRequest model)
        {
            // Always ensure requestor info is from authenticated user
            if (_userService.IsAuthenticated())
            {
                model.RequestorName = _userService.GetUserName();
                model.RequestorEmail = _userService.GetUserEmail();
            }
            
            if (ModelState.IsValid)
            {
                try
                {
                    var changeRequestId = await _changeRequestService.CreateChangeRequestAsync(model);
                    
                    TempData["SuccessMessage"] = $"Change request #{changeRequestId} submitted successfully!";
                    return RedirectToAction("Success", new { id = changeRequestId });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating change request");
                    ModelState.AddModelError("", "An error occurred while submitting your request. Please try again.");
                }
            }

            ViewBag.ServiceOptions = GetServiceOptions();
            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var changeRequest = await _changeRequestService.GetChangeRequestByIdAsync(id);
            if (changeRequest == null)
            {
                return NotFound();
            }

            var authorization = await _authorizationService.AuthorizeAsync(User, null, "ChangeApproversOnly");
            var currentUserEmail = _userService.GetUserEmail();
            var isOwner = !string.IsNullOrWhiteSpace(currentUserEmail) &&
                          string.Equals(currentUserEmail, changeRequest.RequestorEmail, StringComparison.OrdinalIgnoreCase);

            ViewBag.IsApprover = authorization.Succeeded;
            ViewBag.IsRequestOwner = isOwner;
            ViewBag.OwnerCanUpdateStatus = isOwner &&
                (changeRequest.Status == ChangeRequestStatus.Approved ||
                 changeRequest.Status == ChangeRequestStatus.OnHold);

            return View(changeRequest);
        }

        public IActionResult Success(int? id)
        {
            ViewBag.ChangeRequestId = id;
            return View();
        }

        public async Task<IActionResult> MyRequests()
        {
            // Get the user's email to filter their requests
            var userEmail = _userService.GetUserEmail();
            var changeRequests = await _changeRequestService.GetChangeRequestsByEmailAsync(userEmail);
            return View(changeRequests);
        }

    [Authorize(Policy = "ChangeApproversOnly")]
    public async Task<IActionResult> Approvals()
    {
            var allRequests = await _changeRequestService.GetAllChangeRequestsAsync();
            return View(allRequests);
    }

        public async Task<IActionResult> Edit(int id)
        {
            var changeRequest = await _changeRequestService.GetChangeRequestByIdAsync(id);
            if (changeRequest == null)
            {
                return NotFound();
            }

            // Only allow editing of requests with "New" status
            if (changeRequest.Status != ChangeRequestStatus.New)
            {
                TempData["ErrorMessage"] = "Only requests with 'New' status can be edited.";
                return RedirectToAction("MyRequests");
            }

            ViewBag.ServiceOptions = GetServiceOptions();
            return View(changeRequest);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ChangeRequest model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Get the existing request to preserve certain fields
                    var existingRequest = await _changeRequestService.GetChangeRequestByIdAsync(id);
                    if (existingRequest == null)
                    {
                        return NotFound();
                    }

                    // Only allow editing of requests with "New" status
                    if (existingRequest.Status != ChangeRequestStatus.New)
                    {
                        TempData["ErrorMessage"] = "Only requests with 'New' status can be edited.";
                        return RedirectToAction("MyRequests");
                    }

                    // Preserve original creation date and status
                    model.CreatedDate = existingRequest.CreatedDate;
                    model.Status = existingRequest.Status;

                    // Update the request
                    var result = await _changeRequestService.UpdateChangeRequestAsync(model);
                    if (result)
                    {
                        TempData["SuccessMessage"] = $"Change request #{model.Id} updated successfully!";
                        return RedirectToAction("Details", new { id = model.Id });
                    }
                    else
                    {
                        ModelState.AddModelError("", "An error occurred while updating the request. Please try again.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating change request {Id}", id);
                    ModelState.AddModelError("", "An error occurred while updating your request. Please try again.");
                }
            }

            ViewBag.ServiceOptions = GetServiceOptions();
            return View(model);
        }

        [HttpGet]
        [Authorize(Policy = "ChangeApproversOnly")]
        public async Task<IActionResult> Approve(int id)
        {
            var changeRequest = await _changeRequestService.GetChangeRequestByIdAsync(id);
            if (changeRequest == null)
            {
                return NotFound();
            }

            if (changeRequest.Status != ChangeRequestStatus.New)
            {
                TempData["ErrorMessage"] = "Only 'New' requests can be approved.";
                return RedirectToAction("Details", new { id });
            }

            ViewBag.ChangeRequest = changeRequest;
            ViewBag.ApproverName = _userService.GetUserName();
            ViewBag.ApproverEmail = _userService.GetUserEmail();
            return View();
        }

        [HttpPost]
        [Authorize(Policy = "ChangeApproversOnly")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string approverName, string approverEmail)
        {
            if (_userService.IsAuthenticated())
            {
                var resolvedName = _userService.GetUserName();
                var resolvedEmail = _userService.GetUserEmail();

                if (!string.IsNullOrWhiteSpace(resolvedName))
                {
                    approverName = resolvedName;
                }

                if (!string.IsNullOrWhiteSpace(resolvedEmail))
                {
                    approverEmail = resolvedEmail;
                }
            }

            if (string.IsNullOrWhiteSpace(approverName) || string.IsNullOrWhiteSpace(approverEmail))
            {
                ModelState.AddModelError("", "Approver name and email are required.");
                
                var changeRequest = await _changeRequestService.GetChangeRequestByIdAsync(id);
                ViewBag.ChangeRequest = changeRequest;
                ViewBag.ApproverName = _userService.GetUserName();
                ViewBag.ApproverEmail = _userService.GetUserEmail();
                return View();
            }

            try
            {
                var result = await _changeRequestService.UpdateApprovalAsync(id, approverName, approverEmail, approverEmail);
                if (result)
                {
                    TempData["SuccessMessage"] = $"Change request #{id} has been approved successfully!";
                    return RedirectToAction("Details", new { id });
                }
                else
                {
                    ModelState.AddModelError("", "Change request not found or cannot be approved.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving change request {Id}", id);
                ModelState.AddModelError("", "An error occurred while approving the request. Please try again.");
            }

            var requestForView = await _changeRequestService.GetChangeRequestByIdAsync(id);
            ViewBag.ChangeRequest = requestForView;
            ViewBag.ApproverName = _userService.GetUserName();
            ViewBag.ApproverEmail = _userService.GetUserEmail();
            return View();
        }

        [HttpPost]
        [Authorize(Policy = "ChangeApproversOnly")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string rejectionReason)
        {
            var changeRequest = await _changeRequestService.GetChangeRequestByIdAsync(id);
            if (changeRequest == null)
            {
                return NotFound();
            }

            if (changeRequest.Status != ChangeRequestStatus.New)
            {
                TempData["ErrorMessage"] = "Only change requests awaiting approval can be rejected.";
                return RedirectToAction("Details", new { id });
            }

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["ErrorMessage"] = "Please provide a rejection reason.";
                return RedirectToAction("Approve", new { id });
            }

            var approverName = _userService.GetUserName();
            var approverEmail = _userService.GetUserEmail();
            var modifiedBy = !string.IsNullOrWhiteSpace(approverName)
                ? approverName
                : approverEmail ?? "Approver";

            var success = await _changeRequestService.UpdateChangeRequestStatusAsync(
                id,
                ChangeRequestStatus.Cancelled,
                modifiedBy,
                rejectionReason.Trim());

            if (success)
            {
                TempData["SuccessMessage"] = $"Change request #{id} was rejected.";
            }
            else
            {
                TempData["ErrorMessage"] = "We couldn't reject this change request. Please try again.";
            }

            return RedirectToAction("Details", new { id });
        }

        [HttpGet]
        public async Task<IActionResult> UpdateStatus(int id)
        {
            var changeRequest = await _changeRequestService.GetChangeRequestByIdAsync(id);
            if (changeRequest == null)
            {
                return NotFound();
            }

            if (changeRequest.Status == ChangeRequestStatus.New)
            {
                TempData["ErrorMessage"] = "Use the Review & Approve workflow to approve or reject new requests.";
                return RedirectToAction("Details", new { id });
            }

            if (changeRequest.Status != ChangeRequestStatus.Approved &&
                changeRequest.Status != ChangeRequestStatus.OnHold)
            {
                TempData["ErrorMessage"] = "Only approved or on-hold requests can be updated from this page.";
                return RedirectToAction("Details", new { id });
            }

            var isApprover = (await _authorizationService.AuthorizeAsync(User, null, "ChangeApproversOnly")).Succeeded;
            var currentUserEmail = _userService.GetUserEmail();
            var isOwner = !string.IsNullOrWhiteSpace(currentUserEmail) &&
                          string.Equals(currentUserEmail, changeRequest.RequestorEmail, StringComparison.OrdinalIgnoreCase);

            ViewBag.ChangeRequest = changeRequest;
            ViewBag.AvailableStatuses = GetAvailableStatuses(changeRequest.Status, isApprover, isOwner);
            ViewBag.ModifiedBy = _userService.GetUserName();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, ChangeRequestStatus newStatus, string modifiedBy, string? comments)
        {
            var changeRequest = await _changeRequestService.GetChangeRequestByIdAsync(id);
            if (changeRequest == null)
            {
                return NotFound();
            }

            if (changeRequest.Status != ChangeRequestStatus.Approved &&
                changeRequest.Status != ChangeRequestStatus.OnHold)
            {
                TempData["ErrorMessage"] = "This change request cannot be updated from its current state.";
                return RedirectToAction("Details", new { id });
            }

            var isApprover = (await _authorizationService.AuthorizeAsync(User, null, "ChangeApproversOnly")).Succeeded;
            var currentUserEmail = _userService.GetUserEmail();
            var isOwner = !string.IsNullOrWhiteSpace(currentUserEmail) &&
                          string.Equals(currentUserEmail, changeRequest.RequestorEmail, StringComparison.OrdinalIgnoreCase);
            var availableStatuses = GetAvailableStatuses(changeRequest.Status, isApprover, isOwner);

            if (_userService.IsAuthenticated())
            {
                var resolvedName = _userService.GetUserName();
                if (!string.IsNullOrWhiteSpace(resolvedName))
                {
                    modifiedBy = resolvedName;
                }
            }

            if (string.IsNullOrWhiteSpace(modifiedBy))
            {
                ModelState.AddModelError("", "Modified by field is required.");
                
                ViewBag.ChangeRequest = changeRequest;
                ViewBag.AvailableStatuses = availableStatuses;
                ViewBag.ModifiedBy = _userService.GetUserName();
                return View();
            }

            if (!availableStatuses.Contains(newStatus))
            {
                ModelState.AddModelError("", "You are not permitted to move this request to the selected status.");
                ViewBag.ChangeRequest = changeRequest;
                ViewBag.AvailableStatuses = availableStatuses;
                ViewBag.ModifiedBy = _userService.GetUserName();
                return View();
            }

            try
            {
                var result = await _changeRequestService.UpdateChangeRequestStatusAsync(id, newStatus, modifiedBy, comments);
                if (result)
                {
                    TempData["SuccessMessage"] = $"Change request #{id} status updated to {newStatus} successfully!";
                    return RedirectToAction("Details", new { id });
                }
                else
                {
                    ModelState.AddModelError("", "Change request not found or status cannot be updated.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for change request {Id}", id);
                ModelState.AddModelError("", "An error occurred while updating the status. Please try again.");
            }

            var requestForView = await _changeRequestService.GetChangeRequestByIdAsync(id);
            ViewBag.ChangeRequest = requestForView;
            var refreshApprover = (await _authorizationService.AuthorizeAsync(User, null, "ChangeApproversOnly")).Succeeded;
            var refreshOwner = !string.IsNullOrWhiteSpace(currentUserEmail) &&
                               requestForView != null &&
                               string.Equals(currentUserEmail, requestForView.RequestorEmail, StringComparison.OrdinalIgnoreCase);
            ViewBag.AvailableStatuses = GetAvailableStatuses(requestForView?.Status ?? ChangeRequestStatus.New, refreshApprover, refreshOwner);
            ViewBag.ModifiedBy = _userService.GetUserName();
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> AuditTrail(int id)
        {
            var changeRequest = await _changeRequestService.GetChangeRequestByIdAsync(id);
            if (changeRequest == null)
            {
                return NotFound();
            }

            var auditTrail = await _changeRequestService.GetAuditTrailAsync(id);
            
            ViewBag.ChangeRequest = changeRequest;
            return View(auditTrail);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadAttachment(int requestId, int attachmentId)
        {
            var changeRequest = await _changeRequestService.GetChangeRequestByIdAsync(requestId);
            if (changeRequest == null)
            {
                return NotFound();
            }

            var attachment = changeRequest.AttachmentFiles.FirstOrDefault(a => a.Id == attachmentId);
            if (attachment == null)
            {
                return NotFound();
            }

            var isApprover = (await _authorizationService.AuthorizeAsync(User, null, "ChangeApproversOnly")).Succeeded;
            var currentUserEmail = _userService.GetUserEmail();
            var isOwner = !string.IsNullOrWhiteSpace(currentUserEmail) &&
                          string.Equals(currentUserEmail, changeRequest.RequestorEmail, StringComparison.OrdinalIgnoreCase);

            if (!isOwner && !isApprover)
            {
                return Forbid();
            }

            var fileStream = await _blobStorageService.DownloadFileAsync(attachment.BlobUrl);
            if (fileStream == null)
            {
                return NotFound();
            }

            var contentType = string.IsNullOrWhiteSpace(attachment.ContentType)
                ? "application/octet-stream"
                : attachment.ContentType;

            return File(fileStream, contentType, attachment.FileName);
        }

        private List<ChangeRequestStatus> GetAvailableStatuses(
            ChangeRequestStatus currentStatus,
            bool isApprover,
            bool isRequestOwner)
        {
            var availableStatuses = new List<ChangeRequestStatus>();

            switch (currentStatus)
            {
                case ChangeRequestStatus.New:
                    if (isApprover)
                    {
                        availableStatuses.Add(ChangeRequestStatus.Approved);
                        availableStatuses.Add(ChangeRequestStatus.Cancelled);
                    }
                    break;
                case ChangeRequestStatus.Approved:
                    if (isApprover || isRequestOwner)
                    {
                        availableStatuses.Add(ChangeRequestStatus.Complete);
                        availableStatuses.Add(ChangeRequestStatus.OnHold);
                        availableStatuses.Add(ChangeRequestStatus.Abandoned);
                    }
                    break;
                case ChangeRequestStatus.OnHold:
                    if (isApprover || isRequestOwner)
                    {
                        availableStatuses.Add(ChangeRequestStatus.Complete);
                        availableStatuses.Add(ChangeRequestStatus.Abandoned);
                    }
                    break;
                case ChangeRequestStatus.Complete:
                case ChangeRequestStatus.Abandoned:
                case ChangeRequestStatus.Cancelled:
                    // Terminal states - no transitions allowed
                    break;
            }

            return availableStatuses;
        }

        private List<SelectListItem> GetServiceOptions()
        {
            var services = _configuration.GetSection("Services:Catalog").Get<List<string>>();

            if (services == null || services.Count == 0)
            {
                services = new List<string>
                {
                    "Arkham Automate",
                    "Arkham AI App Builder",
                    "Arkham Consulting",
                    "Arkham RPA",
                    "Arkham Edge Computing",
                    "Arkham Fraud Detect"
                };
            }

            return services
                .Select(s => new SelectListItem { Value = s, Text = s })
                .ToList();
        }
    }
}
