using System;

namespace ErenshorDeepSims
{
    // Log-safe metadata helpers. Never return the supplied payload itself: prompts, player queries,
    // retrieved text, and model replies can all contain private conversation/memory context.
    internal static class DiagnosticPrivacy
    {
        internal static string DescribeChars(string label, string value)
        {
            string safeLabel = string.IsNullOrWhiteSpace(label) ? "payload" : label.Trim();
            return safeLabel + "Chars=" + (value == null ? 0 : value.Length);
        }

        internal static string ExceptionType(Exception ex)
        {
            if (ex == null) return "UnknownError";
            string type = ex.GetType().Name;
            Exception inner = ex.InnerException;
            if (inner != null && inner.GetType() != ex.GetType()) type += "/" + inner.GetType().Name;
            return type;
        }
    }
}
