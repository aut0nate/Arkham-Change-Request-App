using ArkhamChangeRequest.Models;

namespace ArkhamChangeRequest.Services
{
    public interface IChangeRequestService
    {
        Task<int> CreateChangeRequestAsync(ChangeRequest changeRequest);
        Task<ChangeRequest?> GetChangeRequestByIdAsync(int id);
        Task<IEnumerable<ChangeRequest>> GetAllChangeRequestsAsync();
        Task<IEnumerable<ChangeRequest>> GetChangeRequestsByEmailAsync(string email);
        Task<bool> UpdateChangeRequestAsync(ChangeRequest changeRequest);
        Task<bool> UpdateChangeRequestStatusAsync(int id, ChangeRequestStatus status, string? modifiedBy = null, string? comments = null);
        Task<bool> UpdateApprovalAsync(int id, string approverName, string approverEmail, string? modifiedBy = null);
        Task<bool> DeleteChangeRequestAsync(int id);
        Task<IEnumerable<ChangeRequestAudit>> GetAuditTrailAsync(int changeRequestId);
        Task<IEnumerable<ChangeRequest>> GetPendingApprovalsAsync();
    }
}
