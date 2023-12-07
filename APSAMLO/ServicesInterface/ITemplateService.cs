using APSAMLO.Model.Mail;

namespace APSAMLO.ServicesInterface
{
    interface ITemplateService
    {
        string GetTemplate(string template, Message messageMail);
    }
}
