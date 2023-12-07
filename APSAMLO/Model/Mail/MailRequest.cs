
namespace APSAMLO.Model.Mail
{
    public class MailRequest
    {
        public string? Subject { get; set; }
        public string? Body { get; set; }
        public string? From { get; set; }
        public string? DisplayName { get; set; }
        public List<string>? ToList { get; set; }
        public List<string>? CcList { get; set; }
        public List<string>? BccList { get; set; }
        public string? ReplyTo { get; set; }
        public List<AttachedFile>? AttachedFileList { get; set; }
        public bool IsHtml { get; set; }
        public bool SendToCustomer { get; set; }
    }
}
