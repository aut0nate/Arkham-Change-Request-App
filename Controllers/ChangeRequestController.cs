using Microsoft.AspNetCore.Mvc;
using ArkhamChangeRequest.Models;
using ArkhamChangeRequest.Services;
using Microsoft.AspNetCore.Authorization;

namespace ArkhamChangeRequest.Controllers
{
    public class ChangeRequestController : Controller
    {
        private readonly IChangeRequestService _changeRequestService;
        private readonly IUserService _userService;
        private readonly ILogger<ChangeRequestController> _logger;

        public ChangeRequestController(
            IChangeRequestService changeRequestService,
            IUserService userService,
            ILogger<ChangeRequestController> logger)
        {
            _changeRequestService = changeRequestService;
            _userService = userService;
            _logger = logger;
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

            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var changeRequest = await _changeRequestService.GetChangeRequestByIdAsync(id);
            if (changeRequest == null)
            {
                return NotFound();
            }

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

            return View(model);
        }

        [HttpGet]
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
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string approverName, string approverEmail)
        {
            if (string.IsNullOrWhiteSpace(approverName) || string.IsNullOrWhiteSpace(approverEmail))
            {
                ModelState.AddModelError("", "Approver name and email are required.");
                
                var changeRequest = await _changeRequestService.GetChangeRequestByIdAsync(id);
                ViewBag.ChangeRequest = changeRequest;
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
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> UpdateStatus(int id)
        {
            var changeRequest = await _changeRequestService.GetChangeRequestByIdAsync(id);
            if (changeRequest == null)
            {
                return NotFound();
            }

            ViewBag.ChangeRequest = changeRequest;
            ViewBag.AvailableStatuses = GetAvailableStatuses(changeRequest.Status);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, ChangeRequestStatus newStatus, string modifiedBy, string? comments)
        {
            if (string.IsNullOrWhiteSpace(modifiedBy))
            {
                ModelState.AddModelError("", "Modified by field is required.");
                
                var changeRequest = await _changeRequestService.GetChangeRequestByIdAsync(id);
                ViewBag.ChangeRequest = changeRequest;
                ViewBag.AvailableStatuses = GetAvailableStatuses(changeRequest?.Status ?? ChangeRequestStatus.New);
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
            ViewBag.AvailableStatuses = GetAvailableStatuses(requestForView?.Status ?? ChangeRequestStatus.New);
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

        private List<ChangeRequestStatus> GetAvailableStatuses(ChangeRequestStatus currentStatus)
        {
            var availableStatuses = new List<ChangeRequestStatus>();

            switch (currentStatus)
            {
                case ChangeRequestStatus.New:
                    availableStatuses.AddRange(new[] { ChangeRequestStatus.Approved, ChangeRequestStatus.Cancelled });
                    break;
                case ChangeRequestStatus.Approved:
                    availableStatuses.AddRange(new[] { ChangeRequestStatus.Complete, ChangeRequestStatus.Cancelled });
                    break;
                case ChangeRequestStatus.Complete:
                case ChangeRequestStatus.Cancelled:
                    // Terminal states - no transitions allowed
                    break;
            }

            return availableStatuses;
        }
    }
}
