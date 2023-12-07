namespace APSAMLO.Model
{
    public class Configuration
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

        private static Configuration? _instance;

        public static Configuration Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Configuration();
                }
                return _instance;
            }
        }
    }
}
