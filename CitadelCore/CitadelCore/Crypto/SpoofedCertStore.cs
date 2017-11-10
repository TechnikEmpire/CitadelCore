/*
* Copyright © 2017 Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelCore.Extensions;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CitadelCore.Crypto
{
    /// <summary>
    /// The SpoofedCertStore class establishes operating system trust. 
    /// </summary>
    internal class SpoofedCertStore
    {
        /// <summary>
        /// Dictionary that keeps all generated, cloned certificates issued by our fake CA. 
        /// </summary>
        private ConcurrentDictionary<string, System.Security.Cryptography.X509Certificates.X509Certificate2> m_certificates = new ConcurrentDictionary<string, System.Security.Cryptography.X509Certificates.X509Certificate2>();

        /// <summary>
        /// Our CA keypair for signing. 
        /// </summary>
        private AsymmetricCipherKeyPair m_caKeypair;

        /// <summary>
        /// Our CA signer for issuing cloned certificates. 
        /// </summary>
        private Asn1SignatureFactory m_caSigner;

        /// <summary>
        /// Our actual CA certificate. 
        /// </summary>
        private X509Certificate m_caCertificate;

        /// <summary>
        /// Constructs a new certificate store instance. 
        /// </summary>
        public SpoofedCertStore()
        {
            GenerateSelfSignedCertificate();
            EstablishOsTrust();
        }

        /// <summary>
        /// Issues a Domain Validation certificate for the supplied hostname. 
        /// </summary>
        /// <param name="host">
        /// The hostname for which to issue a DV certificate. 
        /// </param>
        /// <returns>
        /// A DV certificate for the specified host. 
        /// </returns>
        public System.Security.Cryptography.X509Certificates.X509Certificate2 GetSpoofedCertificateForHost(string host)
        {
            System.Security.Cryptography.X509Certificates.X509Certificate2 cloned = null;

            if(m_certificates.TryGetValue(host, out cloned))
            {
                return cloned;
            }

            var certGen = new X509V3CertificateGenerator();

            var serialRandomGen = new CryptoApiRandomGenerator();
            var serialRandom = new SecureRandom(serialRandomGen);
            var serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(Int64.MaxValue), serialRandom);

            X509Name dnName = new X509Name(string.Format("CN={0}", host));
            certGen.SetSerialNumber(serialNumber);
            certGen.SetIssuerDN(m_caCertificate.SubjectDN);
            certGen.SetNotBefore(DateTime.Now);
            certGen.SetNotAfter(DateTime.Now.AddYears(1));
            certGen.SetSubjectDN(dnName);
            
            var certificatePermissions = new List<KeyPurposeID>()
            {
                 KeyPurposeID.IdKPServerAuth
            };

            certGen.AddExtension(Org.BouncyCastle.Asn1.X509.X509Extensions.ExtendedKeyUsage, false, new ExtendedKeyUsage(certificatePermissions));

            var subjectAlternativeNamesExtension = new DerSequence(new[] { host }.Select(name => new GeneralName(GeneralName.DnsName, name)).ToArray<Asn1Encodable>());

            certGen.AddExtension(X509Extensions.SubjectAlternativeName.Id, false, subjectAlternativeNamesExtension);

            var kpg = new ECKeyPairGenerator();
            kpg.Init(new KeyGenerationParameters(new SecureRandom(), 256));

            var fkp = kpg.GenerateKeyPair();

            certGen.SetPublicKey(fkp.Public);

            certGen.AddExtension(X509Extensions.AuthorityKeyIdentifier, false, new AuthorityKeyIdentifierStructure(m_caCertificate));
            certGen.AddExtension(X509Extensions.SubjectKeyIdentifier, false, new SubjectKeyIdentifierStructure(fkp.Public));

            X509Certificate cert = certGen.Generate(m_caSigner);

            var converted = cert.ConvertFromBouncyCastle(fkp);

            m_certificates.GetOrAdd(host, converted);

            return converted;
        }

        /// <summary>
        /// Generates a self signed certificate for this store to be able to issue cloned certs. 
        /// </summary>
        private void GenerateSelfSignedCertificate()
        {
            var kpg = new ECKeyPairGenerator();
            kpg.Init(new KeyGenerationParameters(new SecureRandom(), 256));

            m_caKeypair = kpg.GenerateKeyPair();

            m_caSigner = new Asn1SignatureFactory("SHA256withECDSA", m_caKeypair.Private);

            DateTime startDate = DateTime.Now;
            DateTime expiryDate = DateTime.Now.AddYears(1);
            BigInteger serialNumber = BigInteger.ProbablePrime(256, new Random());

            var certGen = new X509V3CertificateGenerator();

            var certificatePermissions = new List<KeyPurposeID>()
            {
                 KeyPurposeID.IdKPCodeSigning,
                 KeyPurposeID.IdKPServerAuth,
                 KeyPurposeID.IdKPTimeStamping,
                 KeyPurposeID.IdKPOcspSigning,
                 KeyPurposeID.IdKPClientAuth
            };

            X509Name dnName = new X509Name("CN=Citadel Core");
            certGen.SetSerialNumber(serialNumber);
            certGen.SetIssuerDN(dnName);
            certGen.SetNotBefore(startDate);
            certGen.SetNotAfter(expiryDate);

            certGen.AddExtension(Org.BouncyCastle.Asn1.X509.X509Extensions.ExtendedKeyUsage, false, new ExtendedKeyUsage(certificatePermissions));            
            certGen.AddExtension(X509Extensions.SubjectKeyIdentifier, false, new SubjectKeyIdentifierStructure(m_caKeypair.Public));
            
            certGen.AddExtension(Org.BouncyCastle.Asn1.X509.X509Extensions.BasicConstraints, false, new BasicConstraints(true));

            // Note that because we're self signing, our subject and issuer names are the same.
            certGen.SetSubjectDN(dnName);
            certGen.SetPublicKey(m_caKeypair.Public);
            m_caCertificate = certGen.Generate(m_caSigner);
        }

        /// <summary>
        /// Attempts to establish trust with the host operating system by installing this instance's
        /// CA certificate in the trusted root certs store. This must be called only after GenerateSelfSignedCertificate().
        /// </summary>
        private void EstablishOsTrust()
        {
            InstallCertificateInHostOsTrustStore(m_caCertificate, true);
        }

        /// <summary>
        /// Attempts to install the given certificate in the host OS's trusted root store. 
        /// </summary>
        /// <param name="certificate">
        /// The certificate to install. 
        /// </param>
        /// <param name="overwrite">
        /// Whether or not to overwrite. If true, any and all certificates in the host OS store with
        /// a matching subject name will be deleted before the supplied certificate is installed.
        /// </param>
        public static void InstallCertificateInHostOsTrustStore(X509Certificate certificate, bool overwrite = false)
        {
            switch(Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                {
                    var store = new System.Security.Cryptography.X509Certificates.X509Store(System.Security.Cryptography.X509Certificates.StoreName.Root, System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine);
                    store.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadWrite);

                    if(overwrite)
                    {
                        UninstallCertificateInHostOsTrustStore(certificate);
                    }

                    store.Add(new System.Security.Cryptography.X509Certificates.X509Certificate2(certificate.GetEncoded()));

                    store.Close();
                }
                break;

                default:
                {
                    throw new PlatformNotSupportedException("This operating system is currently unsupported.");
                }
            }
        }

        /// <summary>
        /// Attempts to remove any and all certificates in the host OS's trusted root cert store that
        /// has the same subject name as the given certificate.
        /// </summary>
        /// <param name="certificate">
        /// The certificate who's subject name to use for matching certificates that need to be removed. 
        /// </param>
        public static void UninstallCertificateInHostOsTrustStore(X509Certificate certificate)
        {
            switch(Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                {
                    var store = new System.Security.Cryptography.X509Certificates.X509Store(System.Security.Cryptography.X509Certificates.StoreName.Root, System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine);
                    store.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadWrite);

                    foreach(var storeCert in store.Certificates)
                    {
                        if(storeCert.SubjectName.Format(false) == certificate.SubjectDN.ToString())
                        {
                            // Cert with same subject exists. Remove.
                            store.Remove(storeCert);
                        }
                    }
                }
                break;

                default:
                {
                    throw new PlatformNotSupportedException("This operating system is currently unsupported.");
                }
            }
        }
    }
}