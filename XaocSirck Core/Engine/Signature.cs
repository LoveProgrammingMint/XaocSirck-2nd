using PeNet;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace XaocSirck_Core.Engine;

internal sealed class Signature
{
    public readonly HashSet<String> _trustedThumbprints = [];

    public Boolean FileDigitallySignedAndValid(PeFile pe)
    {
        try
        {
            X509Certificate2? auth = pe.SigningAuthenticodeCertificate;
            if (auth == null) return false;

            X509Chain chain = new()
            {
                ChainPolicy = {
                    RevocationMode = X509RevocationMode.Offline,
                    RevocationFlag = X509RevocationFlag.ExcludeRoot,
                    UrlRetrievalTimeout = TimeSpan.FromSeconds(10),
                    VerificationFlags = X509VerificationFlags.NoFlag
                }
            };

            Boolean chainOk = chain.Build(auth);
            Boolean isTrusted = chain.ChainElements
                .Any(el => _trustedThumbprints.Contains(el.Certificate.Thumbprint));

            if (isTrusted) return true;

            if (auth.NotAfter <= DateTime.Now) return false;

            Boolean revoked = chain.ChainElements
                .Any(el => el.ChainElementStatus.Any(s => s.Status == X509ChainStatusFlags.Revoked));

            if (revoked) return false;

            return chainOk;
        }
        catch (Exception ex)
        {
            App.Logger.Error("Signature validation failed", ex);
            return false;
        }
    }
}
