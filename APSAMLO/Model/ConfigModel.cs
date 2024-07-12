namespace APSAMLO.Model
{
    public class ConfigModel
    {
        public string? APIKey { get; set; }
        public string? CertificatePassword { get; set; }
        public string? CertificatePath { get; set; }
        public string? ProcessPath { get; set; }
        public string? KeyFile { get; set; }
        public string? PassPhrase { get; set; }
        public string? PassWord { get; set; }
        public string? UserName { get; set; }
        public string? ZipFilePath { get; set; }
        public string? BackupPath { get; set; }

        private static ConfigModel? _instance;

        public static ConfigModel Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ConfigModel();
                }
                return _instance;
            }
        }
    }
}
