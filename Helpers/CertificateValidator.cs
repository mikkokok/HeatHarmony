using HeatHarmony.Config;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace HeatHarmony.Helpers
{
    public static class CertificateValidator
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public static bool ValidateCertificate(HttpRequestMessage request, X509Certificate2 certificate, X509Chain certificateChain, SslPolicyErrors policy)
        {
            var certificate2 = new X509Certificate2(certificate);
            return certificate2.Thumbprint?.Equals(GlobalConfig.RestlessFalconConfig!.SslThumbprint, StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
