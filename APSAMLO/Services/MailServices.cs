using APSAMLO.Model.Mail;
using APSAMLO.ServicesInterface;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Text;

namespace APSAMLO.Services
{
    public class MailServices : IMailServices
    {
        public static IConfiguration? Configuration { get; set; }
        public async Task<Message> SendMail(MailRequest mailRequest)
        {
            using (var client = new HttpClient())
            {
                Configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

                //Passing service base url  
                client.BaseAddress = new Uri(Configuration["AppSettings:Mail:MailAPI"]);
                client.DefaultRequestHeaders.Clear();

                var content = new StringContent(JsonConvert.SerializeObject(mailRequest), Encoding.UTF8, "application/json");

                //Sending request to find web api REST service resource GetAllEmployees using HttpClient  
                HttpResponseMessage res = await client.PostAsync("api/mail/send", content);

                //Checking the response is successful or not which is sent using HttpClient  
                Message resultMessage;
                if (res.IsSuccessStatusCode)
                {
                    //Storing the response details recieved from web api   
                    var mailResponse = res.Content.ReadAsStringAsync().Result;

                    //Deserializing the response recieved from web api and storing into the Employee list  
                    var mailResponseInfo = JsonConvert.DeserializeObject<MailResponse>(mailResponse);
                    resultMessage = new Message()
                    {
                        Code = Int32.Parse(mailResponseInfo.MessageCode),
                        Detail = mailResponseInfo.MessageDetail
                    };

                }
                else
                {
                    resultMessage = new Message()
                    {
                        Code = -1,
                        Detail = "ส่งไม่สำเร็จ"
                    };
                }
                return resultMessage;
            }
        }
    }
}
