using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ErenshorDeepSims
{
    // HttpWebRequest.Timeout only bounds GetResponse(); ReadWriteTimeout bounds each individual
    // stream read/write and resets on every call, so a slow-drip response can keep ReadToEnd()
    // from ever returning. WikiClient/OfficialNewsClient/ExternalNewsClient all call out from the
    // single serial request pump (see DeepSimsPlugin.RequestPumpAsync); one hung fetch would
    // otherwise stall every subsequent whisper/party reply for the rest of the session. This races
    // the whole request+read against an explicit wall-clock deadline and aborts the request if it
    // loses, so a slow or hostile server is bounded to roughly the configured timeout regardless of
    // how it paces its response.
    internal static class NetworkTimeoutHelper
    {
        internal static string RunWithHardTimeout(HttpWebRequest request, int hardTimeoutMs)
        {
            Task<string> work = Task.Run(delegate
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    return reader.ReadToEnd();
            });

            Task winner = Task.WhenAny(work, Task.Delay(Math.Max(1, hardTimeoutMs))).GetAwaiter().GetResult();
            if (!ReferenceEquals(winner, work))
            {
                try { request.Abort(); } catch { }
                throw new TimeoutException("Request exceeded hard timeout of " + hardTimeoutMs + "ms.");
            }

            // Unwraps the AggregateException so callers keep catching the original WebException type.
            return work.GetAwaiter().GetResult();
        }
    }
}
