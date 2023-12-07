using APSAMLO.Model.Mail;

namespace APSAMLO.Services
{
    class TemplateService : ServicesInterface.ITemplateService
    {

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Program));
        public string GetTemplate(string template, Message messageMail)
        {
            string body = string.Empty;
            string htmlTemplate = string.Empty;

            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MailTemplates", template);

            try
            {
                using (StreamReader reader = new StreamReader(templatePath))
                {
                    body = reader.ReadToEnd();
                    htmlTemplate = body.Replace("{Status}", messageMail.Status);
                }
            }
            catch (Exception er)
            {
                log.Error($"Error reading template: {er.Message}");
            }

            return htmlTemplate;
        }
    }
}
