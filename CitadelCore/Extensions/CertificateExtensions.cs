/*
* Copyright © 2017-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Org.BouncyCastle.Pkcs;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace CitadelCore.Extensions
{
    /// <summary>
    /// CertificateExtensions provides some extensions that help us translate between BouncyCastle
    /// and "native" .NET structures of the same name and type.
    /// </summary>
    internal static class CertificateExtensions
    {
        /// <summary>
        /// Converts a V1 X509Certificate to a V2 certificate.
        /// </summary>
        /// <param name="cert">
        /// </param>
        /// <returns>
        /// </returns>
        public static X509Certificate2 ToV2Certificate(this X509Certificate cert)
        {
            if (cert is X509Certificate2 cast)
            {
                return cast;
            }

            return new X509Certificate2(cert);
        }

        public static X509Certificate2 ConvertFromBouncyCastle(this Org.BouncyCastle.X509.X509Certificate certificate, Org.BouncyCastle.Crypto.AsymmetricCipherKeyPair subjectKeyPair)
        {
            // Now to convert the Bouncy Castle certificate to a .NET certificate. See
            // http://web.archive.org/web/20100504192226/http://www.fkollmann.de/v2/post/Creating-certificates-using-BouncyCastle.aspx
            // ...but, basically, we create a PKCS12 store (a .PFX file) in memory, and add the
            // public and private key to that.
            var store = new Pkcs12Store();

            // What Bouncy Castle calls "alias" is the same as what Windows terms the "friendly name".
            string friendlyName = certificate.SubjectDN.ToString();

            // Add the certificate.
            var certificateEntry = new X509CertificateEntry(certificate);
            store.SetCertificateEntry(friendlyName, certificateEntry);

            // Add the private key.
            store.SetKeyEntry(friendlyName, new AsymmetricKeyEntry(subjectKeyPair.Private), new[] { certificateEntry });

            // Convert it to an X509Certificate2 object by saving/loading it from a MemoryStream. It
            // needs a password. Since we'll remove this later, it doesn't particularly matter what
            // we use.
            const string password = "password";
            byte[] certBytes = null;

            using (var ms = new MemoryStream())
            {
                store.Save(ms, password.ToCharArray(), new Org.BouncyCastle.Security.SecureRandom());
                certBytes = ms.ToArray();
            }

            var convertedCertificate = new X509Certificate2(certBytes, password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);

            return convertedCertificate;
        }
    }
}