/*
* Copyright © 2018-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Microsoft.AspNetCore.Http;
using System.Collections.Specialized;

namespace CitadelCore.Extensions
{
    /// <summary>
    /// Extensions for <see cref="IHeaderDictionary" /> instances.
    /// </summary>
    public static class IHeaderDictionaryExtensions
    {
        /// <summary>
        /// Converts the IHeaderDictionary instance to a NameValueCollection.
        /// </summary>
        /// <param name="dict">
        /// The IHeaderDictionary instance.
        /// </param>
        /// <returns>
        /// The IHeaderDictionary converted into a NameValueCollection.
        /// </returns>
        public static NameValueCollection ToNameValueCollection(this IHeaderDictionary dict)
        {
            var collection = new NameValueCollection();

            foreach (var kvp in dict)
            {
                foreach (var sv in kvp.Value)
                {
                    foreach (var value in kvp.Value)
                    {
                        collection.Add(kvp.Key, value);
                    }
                }
            }

            return collection;
        }
    }
}