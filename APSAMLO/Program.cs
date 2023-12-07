using APSAMLO.Model;
using APSAMLO.Model.Mail;
using APSAMLO.Services;
using APSAMLO.ServicesInterface;
using Cinchoo.PGP;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config")]

namespace APSAMLO
{
    public class Program
    {
        private static IConfiguration? configurationInterface { get; set; }
        private static readonly IMailServices _mailServices = new MailServices();
        private static readonly ITemplateService _templateService = new TemplateService();
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Program));
        private static Message? messageMail { get; set; }
        private static List<AttachedFile> attachedFileList = new List<AttachedFile>();

        static async Task Main()
        {
            configurationInterface = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

            Configuration.Instance.APIKey = configurationInterface["AppSettings:x-api-key"];
            Configuration.Instance.CertificatePassword = configurationInterface["AppSettings:certificatepassword"];
            Configuration.Instance.CertificatePath = configurationInterface["AppSettings:certificate"];
            Configuration.Instance.ProcessPath = configurationInterface["AppSettings:processpath"];
            Configuration.Instance.KeyFile = configurationInterface["AppSettings:keyfile"];
            Configuration.Instance.PassPhrase = configurationInterface["AppSettings:passphrase"];
            Configuration.Instance.PassWord = configurationInterface["AppSettings:password"];
            Configuration.Instance.UserName = configurationInterface["AppSettings:username"];
            Configuration.Instance.ZipFilePath = configurationInterface["AppSettings:zipfilepath"];
            Configuration.Instance.BackupPath = configurationInterface["AppSettings:backuppath"];

            await CheckDataVersion();
            SendMail(messageMail);
        }
        public static void DecryptFile(string inputFilePath, string outputFilePath, string privateKeyFilePath, string passPhrase)
        {
            // Method intentionally left empty.
        }
        public static async Task<Message?> CheckDataVersion()
        {
            var apiSection = configurationInterface.GetSection("AppSettings:Version");
            Dictionary<string, string> getVersionSection = new Dictionary<string, string>();
            List<string> listLoadData = new List<string>();
            // List to store name API check
            List<string> checkList = new List<string>();

            getVersionSection = apiSection.GetChildren().ToDictionary(item => item.Key, item => item.Value);

            if (!Directory.Exists(Configuration.Instance.ProcessPath))
            {
                Directory.CreateDirectory(Configuration.Instance.ProcessPath);
            }

            if (!Directory.Exists(Configuration.Instance.ZipFilePath))
            {
                Directory.CreateDirectory(Configuration.Instance.ZipFilePath);
            }

            if (!Directory.Exists(Configuration.Instance.BackupPath))
            {
                Directory.CreateDirectory(Configuration.Instance.BackupPath);
            }

            string[] filesInzip = Directory.GetFiles(Configuration.Instance.ZipFilePath);
            string[] filesInbackup = Directory.GetFiles(Configuration.Instance.BackupPath);
            if (filesInzip.Length == 0 && filesInbackup.Length == 0)
            {
                var tasks = new Task[]
                {
                GetDataDPRK(),
                GetDataFreeze05(),
                GetDataHR02(),
                GetDataHR08(),
                GetDataIran(),
                GetDataQaida(),
                GetDataTaliban()
                };

                await Task.WhenAll(tasks);
            }

            // Load SSL certificate
            var certificate = new X509Certificate2(Configuration.Instance.CertificatePath, Configuration.Instance.CertificatePassword);

            // Create HttpClientHandler with the certificate
            var handler = new HttpClientHandler();
            handler.ClientCertificates.Add(certificate);

            checkList.AddRange(getVersionSection.Select(version => $"{version.Key},{version.Value}"));

            try
            {
                // Create an HttpClient with the handler
                using (var httpClient = new HttpClient(handler))
                {
                    string base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Configuration.Instance.UserName}:{Configuration.Instance.PassWord}"));
                    string versionFilePath = Path.Combine(Configuration.Instance.ProcessPath, "Version.txt");
                    string[]? checkInFile = null;
                    var getDataAPI = string.Empty;
                    List<string> WriteInFile = new List<string>();
                    List<string> updateFile = new List<string>();
                    List<string> compareVersion = new List<string>();

                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
                    httpClient.DefaultRequestHeaders.Add("x-api-key", Configuration.Instance.APIKey);

                    //Check file created
                    if (!File.Exists(versionFilePath))
                    {
                        File.WriteAllText(versionFilePath, string.Empty);
                        foreach (string getUrlAPI in checkList)
                        {
                            string apiUrl = getUrlAPI.Split(',')[1];
                            using (var response = await httpClient.PostAsync(apiUrl, null))
                            {
                                if (response.IsSuccessStatusCode)
                                {
                                    getDataAPI = await response.Content.ReadAsStringAsync();
                                    WriteInFile.Add(getDataAPI);
                                }
                            }
                        }
                        File.WriteAllLines(versionFilePath, WriteInFile);

                        var tasks = new Task[]
                        {
                        GetDataDPRK(),
                        GetDataFreeze05(),
                        GetDataHR02(),
                        GetDataHR08(),
                        GetDataIran(),
                        GetDataQaida(),
                        GetDataTaliban()
                        };

                        await Task.WhenAll(tasks);
                    }
                    else
                    {
                        checkInFile = File.ReadAllLines(versionFilePath);
                        List<string> updateList = new List<string>();

                        foreach (string getUrlAPI in checkList)
                        {
                            string apiUrl = getUrlAPI.Split(',')[1];

                            using (var response = await httpClient.PostAsync(apiUrl, null))
                            {
                                if (response.IsSuccessStatusCode)
                                {
                                    getDataAPI = await response.Content.ReadAsStringAsync();
                                    var apiData = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(getDataAPI);

                                    if (apiData.Count == 0) continue;

                                    string listName;
                                    if (!apiData[0].TryGetValue("LIST_NAME", out listName)) continue;

                                    string newVersion;
                                    if (!apiData[0].TryGetValue("VERSION_NUMBER", out newVersion)) continue;

                                    string newDate;
                                    if (!apiData[0].TryGetValue("CREATE_DATE", out newDate)) continue;

                                    foreach (string checkText in checkInFile)
                                    {
                                        var fileData = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(checkText);

                                        if (fileData.Count == 0 || !checkText.Contains(listName)) continue;

                                        string oldVersion;
                                        if (!fileData[0].TryGetValue("VERSION_NUMBER", out oldVersion)) continue;

                                        string oldDate;
                                        if (!fileData[0].TryGetValue("CREATE_DATE", out oldDate)) continue;

                                        if (oldVersion != newVersion || oldDate != newDate)
                                        {
                                            updateFile.Add(listName);
                                        }
                                    }

                                    WriteInFile.Add(getDataAPI);
                                    compareVersion.Add(getDataAPI);
                                }
                            }
                        }

                        foreach (var readInLine in checkInFile)
                        {
                            var splitLine = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(readInLine);

                            // If there's no data in splitLine, skip the iteration.
                            if (splitLine.Count == 0) continue;

                            string nameCheckApi = splitLine[0]["LIST_NAME"];
                            bool foundMatch = false;

                            foreach (var lineInCompareVersion in compareVersion)
                            {
                                if (lineInCompareVersion.Contains(nameCheckApi) && readInLine != lineInCompareVersion)
                                {
                                    updateList.Add(lineInCompareVersion);
                                    listLoadData.Add(lineInCompareVersion);
                                    foundMatch = true;
                                    break;
                                }
                            }

                            if (!foundMatch)
                            {
                                updateList.Add(readInLine);
                            }
                        }
                        File.WriteAllLines(versionFilePath, updateList);
                    }

                    if (listLoadData.Count > 0)
                    {
                        // Map names to associated functions
                        var FunctionMap = new Dictionary<string, Func<Task>>
                        {
                            ["FREEZE 15 UN 1718"] = GetDataDPRK,
                            ["FREEZE-05"] = GetDataFreeze05,
                            ["HR-02"] = GetDataHR02,
                            ["HR-08"] = GetDataHR08,
                            ["FREEZE 15 UN 2231"] = GetDataIran,
                            ["FREEZE 04 UN 1267"] = GetDataQaida,
                            ["FREEZE 04 UN 1988"] = GetDataTaliban
                        };

                        string nameCheck = string.Empty;
                        string versionCheck = string.Empty;

                        foreach (var lineInListLoadData in listLoadData)
                        {
                            var splitListLoadData = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(lineInListLoadData);

                            if (splitListLoadData.Count == 0) continue;

                            nameCheck = splitListLoadData[0]["LIST_NAME"];
                            versionCheck = splitListLoadData[0]["VERSION_NUMBER"];

                            if (FunctionMap.TryGetValue(nameCheck, out var function))
                            {
                                await function();
                            }
                            else
                            {
                                log.Info("Not found name of API \n");
                            }
                        }
                    }
                }

                if (messageMail == null)
                {
                    messageMail = new Message
                    {
                        Status = "have not been updated"
                    };
                    return messageMail;
                }
            }
            catch (Exception ex)
            {
                log.Info(ex.Message + "\n");
            }
            return null;
        }

        private static void SendMail(Message? messageMail)
        {
            var request = new MailRequest
            {
                Subject = configurationInterface["AppSettings:Mail:Subject"],
                From = configurationInterface["AppSettings:Mail:SYSTEM_FROM_EMAIL"],
                ToList = new List<string>(configurationInterface["AppSettings:Mail:SYSTEM_FROM_EMAIL_TO_CUSTOMER"].Split(',')),
                CcList = new List<string>(configurationInterface["AppSettings:Mail:CC_EMAIL"].Split(',')),
                AttachedFileList = attachedFileList,
                Body = _templateService.GetTemplate("SendZipFile.cshtml", messageMail)
            };

            var mailResult = _mailServices.SendMail(request).GetAwaiter().GetResult();
            log.Info($"Email has been sent {mailResult.Detail}");
        }
        private static async Task GetDataDPRK()
        {
            string apiFreeze05Path = configurationInterface["AppSettings:Data:apidprkpath"];

            // Load SSL certificate
            var certificate = new X509Certificate2(Configuration.Instance.CertificatePath, Configuration.Instance.CertificatePassword);
            string OutputFilePath = string.Empty;

            // Create HttpClientHandler with the certificate
            var handler = new HttpClientHandler
            {
                ClientCertificates = { certificate }
            };

            // Create an HttpClient with the handler
            using (var httpClient = new HttpClient(handler))
            {
                string base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Configuration.Instance.UserName}:{Configuration.Instance.PassWord}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
                // Add x-api-key header
                httpClient.DefaultRequestHeaders.Add("x-api-key", Configuration.Instance.APIKey);

                // Perform a POST request
                using (var response = await httpClient.PostAsync(apiFreeze05Path, null))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        string getData = await response.Content.ReadAsStringAsync();
                        Data? data = JsonConvert.DeserializeObject<Data>(getData);
                        string? outputFileName = DateTime.Now.ToString("yyyy-MM-dd_HH.mm_") + data?.FileName;
                        byte[] binaryData;

                        try
                        {
                            binaryData = Convert.FromBase64String(data.Result ?? string.Empty);
                        }
                        catch
                        {
                            log.Error("Failed to convert base64 data.");
                            return;
                        }

                        string PGPData = Encoding.UTF8.GetString(binaryData);
                        string PGPfilePath = Path.Combine(Configuration.Instance.ProcessPath, outputFileName);
                        OutputFilePath = Path.Combine(Configuration.Instance.ZipFilePath, outputFileName.Replace(".pgp", ""));

                        File.WriteAllText(PGPfilePath, PGPData);

                        using (ChoPGPEncryptDecrypt pgp = new ChoPGPEncryptDecrypt())
                        {
                            pgp.DecryptFile(PGPfilePath, OutputFilePath, Configuration.Instance.KeyFile, Configuration.Instance.PassPhrase);
                            log.Info(OutputFilePath + " Success \n");
                            File.Delete(PGPfilePath);
                        }
                    }
                }
            }
            // Get file name from file path
            string fileName = Path.GetFileName(OutputFilePath);

            // Read file content and convert to base64 string
            string base64Content = Convert.ToBase64String(File.ReadAllBytes(OutputFilePath));

            // Create new AttachedFile and add it to the list
            attachedFileList.Add(new AttachedFile
            {
                FileName = fileName,
                Base64ByteArrayContent = base64Content
            });

            messageMail = new Message
            {
                Status = "have been updated"
            };

            if (File.Exists(OutputFilePath))
            {
                File.Copy(OutputFilePath, Configuration.Instance.BackupPath + @"\" + fileName, true);
                File.Delete(OutputFilePath);
            }
        }
        private static async Task GetDataFreeze05()
        {
            string apiFreeze05Path = configurationInterface["AppSettings:Data:apifreeze05path"];
            // Load SSL configurationInterface
            var certificate = new X509Certificate2(Configuration.Instance.CertificatePath, Configuration.Instance.CertificatePassword);
            string OutputFilePath = string.Empty;

            // Create HttpClientHandler with the certificate
            var handler = new HttpClientHandler
            {
                ClientCertificates = { certificate }
            };

            // Create an HttpClient with the handler
            using (var httpClient = new HttpClient(handler))
            {
                string base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Configuration.Instance.UserName}:{Configuration.Instance.PassWord}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
                // Add x-api-key header
                httpClient.DefaultRequestHeaders.Add("x-api-key", Configuration.Instance.APIKey);

                // Perform a POST request
                using (var response = await httpClient.PostAsync(apiFreeze05Path, null))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        string getData = await response.Content.ReadAsStringAsync();
                        Data? data = JsonConvert.DeserializeObject<Data>(getData);
                        string? outputFileName = DateTime.Now.ToString("yyyy-MM-dd_HH.mm_") + data?.FileName;
                        byte[] binaryData;

                        try
                        {
                            binaryData = Convert.FromBase64String(data.Result ?? string.Empty);
                        }
                        catch
                        {
                            log.Error("Failed to convert base64 data.");
                            return;
                        }

                        string PGPData = Encoding.UTF8.GetString(binaryData);
                        string PGPfilePath = Path.Combine(Configuration.Instance.ProcessPath, outputFileName);
                        OutputFilePath = Path.Combine(Configuration.Instance.ZipFilePath, outputFileName.Replace(".pgp", ""));

                        File.WriteAllText(PGPfilePath, PGPData);

                        using (ChoPGPEncryptDecrypt pgp = new ChoPGPEncryptDecrypt())
                        {
                            pgp.DecryptFile(PGPfilePath, OutputFilePath, Configuration.Instance.KeyFile, Configuration.Instance.PassPhrase);
                            log.Info(OutputFilePath + " Success \n");
                            File.Delete(PGPfilePath);
                        }
                    }
                }
            }
            // Get file name from file path
            string fileName = Path.GetFileName(OutputFilePath);

            // Read file content and convert to base64 string
            string base64Content = Convert.ToBase64String(File.ReadAllBytes(OutputFilePath));

            // Create new AttachedFile and add it to the list
            attachedFileList.Add(new AttachedFile
            {
                FileName = fileName,
                Base64ByteArrayContent = base64Content
            });

            messageMail = new Message
            {
                Status = "have been updated"
            };

            if (File.Exists(OutputFilePath))
            {
                File.Copy(OutputFilePath, Configuration.Instance.BackupPath + @"\" + fileName, true);
                File.Delete(OutputFilePath);
            }
        }
        private static async Task GetDataHR02()
        {
            string apiHr02Path = configurationInterface["AppSettings:Data:apihr02path"];
            // Load SSL certificate
            var certificate = new X509Certificate2(Configuration.Instance.CertificatePath, Configuration.Instance.CertificatePassword);
            string OutputFilePath = string.Empty;

            // Create HttpClientHandler with the certificate
            var handler = new HttpClientHandler
            {
                ClientCertificates = { certificate }
            };

            // Create an HttpClient with the handler
            using (var httpClient = new HttpClient(handler))
            {
                string base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Configuration.Instance.UserName}:{Configuration.Instance.PassWord}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
                // Add x-api-key header
                httpClient.DefaultRequestHeaders.Add("x-api-key", Configuration.Instance.APIKey);

                // Perform a POST request
                using (var response = await httpClient.PostAsync(apiHr02Path, null))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        string getData = await response.Content.ReadAsStringAsync();
                        Data? data = JsonConvert.DeserializeObject<Data>(getData);
                        string? outputFileName = DateTime.Now.ToString("yyyy-MM-dd_HH.mm_") + data?.FileName;
                        byte[] binaryData;

                        try
                        {
                            binaryData = Convert.FromBase64String(data.Result ?? string.Empty);
                        }
                        catch
                        {
                            log.Error("Failed to convert base64 data.");
                            return;
                        }

                        string PGPData = Encoding.UTF8.GetString(binaryData);
                        string PGPfilePath = Path.Combine(Configuration.Instance.ProcessPath, outputFileName);
                        OutputFilePath = Path.Combine(Configuration.Instance.ZipFilePath, outputFileName.Replace(".pgp", ""));

                        File.WriteAllText(PGPfilePath, PGPData);

                        using (ChoPGPEncryptDecrypt pgp = new ChoPGPEncryptDecrypt())
                        {
                            pgp.DecryptFile(PGPfilePath, OutputFilePath, Configuration.Instance.KeyFile, Configuration.Instance.PassPhrase);
                            log.Info(OutputFilePath + " Success \n");
                            File.Delete(PGPfilePath);
                        }
                    }
                    else
                    {
                        log.Info(response.StatusCode.ToString() + "\n");
                    }
                }
            }
            // Get file name from file path
            string fileName = Path.GetFileName(OutputFilePath);

            // Read file content and convert to base64 string
            string base64Content = Convert.ToBase64String(File.ReadAllBytes(OutputFilePath));

            // Create new AttachedFile and add it to the list
            attachedFileList.Add(new AttachedFile
            {
                FileName = fileName,
                Base64ByteArrayContent = base64Content
            });

            messageMail = new Message
            {
                Status = "have been updated"
            };

            if (File.Exists(OutputFilePath))
            {
                File.Copy(OutputFilePath, Configuration.Instance.BackupPath + @"\" + fileName, true);
                File.Delete(OutputFilePath);
            }
        }
        private static async Task GetDataHR08()
        {
            string apiFreeze05Path = configurationInterface["AppSettings:Data:apihr08path"];
            // Load SSL certificate
            var certificate = new X509Certificate2(Configuration.Instance.CertificatePath, Configuration.Instance.CertificatePassword);
            string OutputFilePath = string.Empty;

            // Create HttpClientHandler with the certificate
            var handler = new HttpClientHandler
            {
                ClientCertificates = { certificate }
            };

            // Create an HttpClient with the handler
            using (var httpClient = new HttpClient(handler))
            {
                string base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Configuration.Instance.UserName}:{Configuration.Instance.PassWord}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
                // Add x-api-key header
                httpClient.DefaultRequestHeaders.Add("x-api-key", Configuration.Instance.APIKey);

                // Perform a POST request
                using (var response = await httpClient.PostAsync(apiFreeze05Path, null))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        string getData = await response.Content.ReadAsStringAsync();
                        Data? data = JsonConvert.DeserializeObject<Data>(getData);
                        string? outputFileName = DateTime.Now.ToString("yyyy-MM-dd_HH.mm_") + data?.FileName;
                        byte[] binaryData;

                        try
                        {
                            binaryData = Convert.FromBase64String(data.Result ?? string.Empty);
                        }
                        catch
                        {
                            log.Error("Failed to convert base64 data.");
                            return;
                        }

                        string PGPData = Encoding.UTF8.GetString(binaryData);
                        string PGPfilePath = Path.Combine(Configuration.Instance.ProcessPath, outputFileName);
                        OutputFilePath = Path.Combine(Configuration.Instance.ZipFilePath, outputFileName.Replace(".pgp", ""));

                        File.WriteAllText(PGPfilePath, PGPData);

                        using (ChoPGPEncryptDecrypt pgp = new ChoPGPEncryptDecrypt())
                        {
                            pgp.DecryptFile(PGPfilePath, OutputFilePath, Configuration.Instance.KeyFile, Configuration.Instance.PassPhrase);
                            log.Info(OutputFilePath + " Success \n");
                            File.Delete(PGPfilePath);
                        }
                    }
                }
            }
            // Get file name from file path
            string fileName = Path.GetFileName(OutputFilePath);

            // Read file content and convert to base64 string
            string base64Content = Convert.ToBase64String(File.ReadAllBytes(OutputFilePath));

            // Create new AttachedFile and add it to the list
            attachedFileList.Add(new AttachedFile
            {
                FileName = fileName,
                Base64ByteArrayContent = base64Content
            });

            messageMail = new Message
            {
                Status = "have been updated"
            };

            if (File.Exists(OutputFilePath))
            {
                File.Copy(OutputFilePath, Configuration.Instance.BackupPath + @"\" + fileName, true);
                File.Delete(OutputFilePath);
            }
        }
        private static async Task GetDataIran()
        {
            string apiFreeze05Path = configurationInterface["AppSettings:Data:apiiranpath"];
            // Load SSL certificate
            var certificate = new X509Certificate2(Configuration.Instance.CertificatePath, Configuration.Instance.CertificatePassword);
            string OutputFilePath = string.Empty;

            // Create HttpClientHandler with the certificate
            var handler = new HttpClientHandler
            {
                ClientCertificates = { certificate }
            };

            // Create an HttpClient with the handler
            using (var httpClient = new HttpClient(handler))
            {
                string base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Configuration.Instance.UserName}:{Configuration.Instance.PassWord}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
                // Add x-api-key header
                httpClient.DefaultRequestHeaders.Add("x-api-key", Configuration.Instance.APIKey);

                // Perform a POST request
                using (var response = await httpClient.PostAsync(apiFreeze05Path, null))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        string getData = await response.Content.ReadAsStringAsync();
                        Data? data = JsonConvert.DeserializeObject<Data>(getData);
                        string? outputFileName = DateTime.Now.ToString("yyyy-MM-dd_HH.mm_") + data?.FileName;
                        byte[] binaryData;

                        try
                        {
                            binaryData = Convert.FromBase64String(data.Result ?? string.Empty);
                        }
                        catch
                        {
                            log.Error("Failed to convert base64 data.");
                            return;
                        }

                        string PGPData = Encoding.UTF8.GetString(binaryData);
                        string PGPfilePath = Path.Combine(Configuration.Instance.ProcessPath, outputFileName);
                        OutputFilePath = Path.Combine(Configuration.Instance.ZipFilePath, outputFileName.Replace(".pgp", ""));

                        File.WriteAllText(PGPfilePath, PGPData);

                        using (ChoPGPEncryptDecrypt pgp = new ChoPGPEncryptDecrypt())
                        {
                            pgp.DecryptFile(PGPfilePath, OutputFilePath, Configuration.Instance.KeyFile, Configuration.Instance.PassPhrase);
                            log.Info(OutputFilePath + " Success \n");
                            File.Delete(PGPfilePath);
                        }
                    }
                }
            }
            // Get file name from file path
            string fileName = Path.GetFileName(OutputFilePath);

            // Read file content and convert to base64 string
            string base64Content = Convert.ToBase64String(File.ReadAllBytes(OutputFilePath));

            // Create new AttachedFile and add it to the list
            attachedFileList.Add(new AttachedFile
            {
                FileName = fileName,
                Base64ByteArrayContent = base64Content
            });

            messageMail = new Message
            {
                Status = "have been updated"
            };

            if (File.Exists(OutputFilePath))
            {
                File.Copy(OutputFilePath, Configuration.Instance.BackupPath + @"\" + fileName, true);
                File.Delete(OutputFilePath);
            }
        }
        private static async Task GetDataQaida()
        {
            string apiFreeze05Path = configurationInterface["AppSettings:Data:apiqaidapath"];
            // Load SSL certificate
            var certificate = new X509Certificate2(Configuration.Instance.CertificatePath, Configuration.Instance.CertificatePassword);
            string OutputFilePath = string.Empty;

            // Create HttpClientHandler with the certificate
            var handler = new HttpClientHandler
            {
                ClientCertificates = { certificate }
            };

            // Create an HttpClient with the handler
            using (var httpClient = new HttpClient(handler))
            {
                string base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Configuration.Instance.UserName}:{Configuration.Instance.PassWord}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
                // Add x-api-key header
                httpClient.DefaultRequestHeaders.Add("x-api-key", Configuration.Instance.APIKey);

                // Perform a POST request
                using (var response = await httpClient.PostAsync(apiFreeze05Path, null))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        string getData = await response.Content.ReadAsStringAsync();
                        Data? data = JsonConvert.DeserializeObject<Data>(getData);
                        string? outputFileName = DateTime.Now.ToString("yyyy-MM-dd_HH.mm_") + data?.FileName;
                        byte[] binaryData;

                        try
                        {
                            binaryData = Convert.FromBase64String(data.Result ?? string.Empty);
                        }
                        catch
                        {
                            log.Error("Failed to convert base64 data.");
                            return;
                        }

                        string PGPData = Encoding.UTF8.GetString(binaryData);
                        string PGPfilePath = Path.Combine(Configuration.Instance.ProcessPath, outputFileName);
                        OutputFilePath = Path.Combine(Configuration.Instance.ZipFilePath, outputFileName.Replace(".pgp", ""));

                        File.WriteAllText(PGPfilePath, PGPData);

                        using (ChoPGPEncryptDecrypt pgp = new ChoPGPEncryptDecrypt())
                        {
                            pgp.DecryptFile(PGPfilePath, OutputFilePath, Configuration.Instance.KeyFile, Configuration.Instance.PassPhrase);
                            log.Info(OutputFilePath + " Success \n");
                            File.Delete(PGPfilePath);
                        }
                    }
                }
            }
            // Get file name from file path
            string fileName = Path.GetFileName(OutputFilePath);

            // Read file content and convert to base64 string
            string base64Content = Convert.ToBase64String(File.ReadAllBytes(OutputFilePath));

            // Create new AttachedFile and add it to the list
            attachedFileList.Add(new AttachedFile
            {
                FileName = fileName,
                Base64ByteArrayContent = base64Content
            });

            messageMail = new Message
            {
                Status = "have been updated"
            };

            if (File.Exists(OutputFilePath))
            {
                File.Copy(OutputFilePath, Configuration.Instance.BackupPath + @"\" + fileName, true);
                File.Delete(OutputFilePath);
            }
        }
        private static async Task GetDataTaliban()
        {
            string apiFreeze05Path = configurationInterface["AppSettings:Data:apitalibanpath"];
            // Load SSL certificate
            var certificate = new X509Certificate2(Configuration.Instance.CertificatePath, Configuration.Instance.CertificatePassword);
            string OutputFilePath = string.Empty;

            // Create HttpClientHandler with the certificate
            var handler = new HttpClientHandler
            {
                ClientCertificates = { certificate }
            };

            // Create an HttpClient with the handler
            using (var httpClient = new HttpClient(handler))
            {
                string base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Configuration.Instance.UserName}:{Configuration.Instance.PassWord}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
                // Add x-api-key header
                httpClient.DefaultRequestHeaders.Add("x-api-key", Configuration.Instance.APIKey);

                // Perform a POST request
                using (var response = await httpClient.PostAsync(apiFreeze05Path, null))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        string getData = await response.Content.ReadAsStringAsync();
                        Data? data = JsonConvert.DeserializeObject<Data>(getData);
                        string? outputFileName = DateTime.Now.ToString("yyyy-MM-dd_HH.mm_") + data?.FileName;
                        byte[] binaryData;

                        try
                        {
                            binaryData = Convert.FromBase64String(data.Result ?? string.Empty);
                        }
                        catch
                        {
                            log.Error("Failed to convert base64 data.");
                            return;
                        }

                        string PGPData = Encoding.UTF8.GetString(binaryData);
                        string PGPfilePath = Path.Combine(Configuration.Instance.ProcessPath, outputFileName);
                        OutputFilePath = Path.Combine(Configuration.Instance.ZipFilePath, outputFileName.Replace(".pgp", ""));

                        File.WriteAllText(PGPfilePath, PGPData);

                        using (ChoPGPEncryptDecrypt pgp = new ChoPGPEncryptDecrypt())
                        {
                            pgp.DecryptFile(PGPfilePath, OutputFilePath, Configuration.Instance.KeyFile, Configuration.Instance.PassPhrase);
                            log.Info(OutputFilePath + " Success \n");
                            File.Delete(PGPfilePath);
                        }
                    }
                }
            }
            // Get file name from file path
            string fileName = Path.GetFileName(OutputFilePath);

            // Read file content and convert to base64 string
            string base64Content = Convert.ToBase64String(File.ReadAllBytes(OutputFilePath));

            // Create new AttachedFile and add it to the list
            attachedFileList.Add(new AttachedFile
            {
                FileName = fileName,
                Base64ByteArrayContent = base64Content
            });

            messageMail = new Message
            {
                Status = "have been updated"
            };

            if (File.Exists(OutputFilePath))
            {
                File.Copy(OutputFilePath, Configuration.Instance.BackupPath + @"\" + fileName, true);
                File.Delete(OutputFilePath);
            }
        }
    }
}
