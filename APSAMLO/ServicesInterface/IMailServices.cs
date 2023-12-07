using APSAMLO.Model.Mail;

namespace APSAMLO.ServicesInterface
{
    interface IMailServices
    {
        Task<Message> SendMail(MailRequest mailRequest);
    }
}
