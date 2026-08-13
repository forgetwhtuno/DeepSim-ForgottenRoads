using System.Collections.Generic;

namespace ErenshorDeepSims
{
    internal static class DiagnosticPrivacyTests
    {
        internal static List<string> Run()
        {
            List<string> results = new List<string>();
            string privateText = "player said a private thing; VERIFIED MEMORY: secret context";
            string described = DiagnosticPrivacy.DescribeChars("request", privateText);
            Add(results, "payload metadata reports length", described == "requestChars=" + privateText.Length);
            Add(results, "payload metadata never contains player text", !described.Contains("private thing"));
            Add(results, "payload metadata never contains memory text", !described.Contains("VERIFIED MEMORY"));
            Add(results, "null payload is safe", DiagnosticPrivacy.DescribeChars("reply", null) == "replyChars=0");
            System.Exception inner = new System.Net.WebException("private endpoint http://secret.local/query?q=private");
            System.Exception wrapped = new System.InvalidOperationException("provider echoed private content", inner);
            string exceptionType = DiagnosticPrivacy.ExceptionType(wrapped);
            Add(results, "exception metadata keeps only types", exceptionType == "InvalidOperationException/WebException");
            Add(results, "exception metadata omits message and endpoint", !exceptionType.Contains("private") && !exceptionType.Contains("http"));
            return results;
        }

        private static void Add(List<string> results, string name, bool pass)
        {
            results.Add("[DeepSims Privacy " + (pass ? "PASS" : "FAIL") + "] " + name);
        }
    }
}
