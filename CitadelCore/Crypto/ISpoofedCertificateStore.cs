/*
* Copyright © 2018-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

namespace CitadelCore.Crypto
{
    /// <summary>
    /// Spoofing certificate store interface definition.
    /// </summary>
    public interface ISpoofedCertificateStore
    {
        /// <summary>
        /// Issues a Domain Validation certificate for the supplied hostname.
        /// </summary>
        /// <param name="host">
        /// The hostname for which to issue a DV certificate.
        /// </param>
        /// <returns>
        /// A DV certificate for the specified host.
        /// </returns>
        System.Security.Cryptography.X509Certificates.X509Certificate2 GetSpoofedCertificateForHost(string host);
    }
}