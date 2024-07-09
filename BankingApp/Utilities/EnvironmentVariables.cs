using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BankingApp.Utilities
{
    public static class EnvironmentVariables
    {
        public static string MailAccount => Environment.GetEnvironmentVariable("MailAccount");
        public static string MailPassword => Environment.GetEnvironmentVariable("MailPassword");
        public static string SmtpHost => Environment.GetEnvironmentVariable("SmtpHost");
        public static string PrivateKey => Environment.GetEnvironmentVariable("PrivateKey");
    }
}