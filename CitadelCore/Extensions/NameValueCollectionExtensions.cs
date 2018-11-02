/*
* Copyright © 2018-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Specialized;

namespace CitadelCore.Extensions
{
    /// <summary>
    /// Extensions for <see cref="NameValueCollection" /> instances.
    /// </summary>
    public static class NameValueCollectionExtensions
    {
        private static readonly char[] s_splitChars = new[] { ',' };

        /// <summary>
        /// Converts the NameValueCollection instance to an IHeaderDictionary.
        /// </summary>
        /// <param name="collection">
        /// The NameValueCollection instance.
        /// </param>
        /// <returns>
        /// The NameValueCollection converted into a IHeaderDictionary.
        /// </returns>
        public static IHeaderDictionary ToIHeaderDictionary(this NameValueCollection collection)
        {
            var dictionary = new HeaderDictionary();

            foreach (string key in collection)
            {
                var value = collection[key];

                dictionary.Add(key, new Microsoft.Extensions.Primitives.StringValues(key.Split(s_splitChars, StringSplitOptions.RemoveEmptyEntries)));
            }

            return dictionary;
        }
    }
}