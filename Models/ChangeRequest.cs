using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArkhamChangeRequest.Models
{
    public class ChangeRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Requestor Name")]
        [StringLength(100)]
        public string RequestorName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
            ErrorMessage = "Please enter a valid email address (e.g., user@company.com)")]
        [Display(Name = "Requestor Email")]
        [StringLength(200)]
        public string RequestorEmail { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Change Title")]
        [StringLength(200)]
        public string ChangeTitle { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Change Description")]
        [StringLength(2000)]
        public string ChangeDescription { get; set; } = string.Empty;

        [Display(Name = "Authorization/Service Affected")]
        [StringLength(1000)]
        public string AuthorizationServiceAffected { get; set; } = string.Empty;

        [Display(Name = "Proposed Implementation Date")]
        [DataType(DataType.DateTime)]
        public DateTime? ProposedImplementationDate { get; set; }

        [Required]
        [Display(Name = "Change Type")]
        public ChangeType ChangeType { get; set; }

        [Required]
        [Display(Name = "Priority")]
        public Priority Priority { get; set; }

        [Required]
        [Display(Name = "Risk Assessment")]
        [StringLength(2000)]
        public string RiskAssessment { get; set; } = string.Empty;

        [Display(Name = "Backout Plan")]
        [StringLength(2000)]
        public string BackoutPlan { get; set; } = string.Empty;

        [NotMapped]
        [Display(Name = "Attachments")]
        public List<IFormFile>? Attachments { get; set; }

        // Navigation property for attachments
        public virtual ICollection<ChangeRequestAttachment> AttachmentFiles { get; set; } = new List<ChangeRequestAttachment>();

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        public ChangeRequestStatus Status { get; set; } = ChangeRequestStatus.New;

        // Approver Information
        [Display(Name = "Approver Name")]
        [StringLength(100)]
        public string? ApproverName { get; set; }

        [Display(Name = "Approver Email")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(200)]
        public string? ApproverEmail { get; set; }

        [Display(Name = "Approval Date")]
        [DataType(DataType.DateTime)]
        public DateTime? ApprovalDate { get; set; }

        // Audit Trail
        public DateTime? LastModifiedDate { get; set; }

        [StringLength(100)]
        public string? LastModifiedBy { get; set; }

        // Navigation property for audit trail
        public virtual ICollection<ChangeRequestAudit> AuditTrail { get; set; } = new List<ChangeRequestAudit>();
    }

    public class ChangeRequestAttachment
    {
        [Key]
        public int Id { get; set; }

        public int ChangeRequestId { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string BlobUrl { get; set; } = string.Empty;

        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

        // Foreign key
        [ForeignKey("ChangeRequestId")]
        public virtual ChangeRequest ChangeRequest { get; set; } = null!;
    }

    public class ChangeRequestAudit
    {
        [Key]
        public int Id { get; set; }

        public int ChangeRequestId { get; set; }

        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty;

        [StringLength(20)]
        public string? OldStatus { get; set; }

        [StringLength(20)]
        public string? NewStatus { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

        [StringLength(500)]
        public string? Comments { get; set; }

        // Foreign key
        [ForeignKey("ChangeRequestId")]
        public virtual ChangeRequest ChangeRequest { get; set; } = null!;
    }

    public enum ChangeRequestStatus
    {
        New,
        Approved,
        OnHold,
        Complete,
        Abandoned,
        Cancelled
    }

    public enum ChangeType
    {
        Normal,
        Emergency,
        Standard,
        Major
    }

    public enum Priority
    {
        Low,
        Medium,
        High,
        Critical
    }
}
