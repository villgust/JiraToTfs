using System;
using System.IO;
using System.Linq;
using System.Text;

namespace TechTalk.JiraRestClient
{
    public static class JiraString
    {
        private const string toInclude = "\t\r\n";

        public static string StripNonPrintable(string toStrip)
        {
            return new string(toStrip.Where(c => !char.IsControl(c) || toInclude.Contains(c)).ToArray());
        }
    }
}