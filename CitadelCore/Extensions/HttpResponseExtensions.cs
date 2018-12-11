/*
* Copyright © 2018-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelCore.IO;
using CitadelCore.Logging;
using CitadelCore.Net.Http;
using CitadelCore.Util;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CitadelCore.Extensions
{
    /// <summary>
    /// Extensions for <see cref="HttpResponse" /> instances.
    /// </summary>
    public static class HttpResponseExtensions
    {
        /// <summary>
        /// Copies all possible headers from the given collection into this HttpResponse instance and
        /// then returns the headers that failed to be added.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <param name="headers">
        /// The headers.
        /// </param>
        /// <param name="exemptedHeaders">
        /// List of headers that are exempt from being removed if they are "forbidden" headers.
        /// </param>
        /// <returns>
        /// A collection of all headers that failed to be added.
        /// </returns>
        public static NameValueCollection PopulateHeaders(this HttpResponse message, NameValueCollection headers, HashSet<string> exemptedHeaders)
        {
            // This will hold whatever headers we cannot successfully add here.
            var clonedCollection = new NameValueCollection(headers);

            foreach (string key in headers)
            {
                if (!exemptedHeaders.Contains(key) && ForbiddenHttpHeaders.IsForbidden(key))
                {
                    continue;
                }

                try
                {
                    if (message.Headers.ContainsKey(key))
                    {
                        message.Headers.Remove(key);
                    }

                    message.Headers.Add(key, new Microsoft.Extensions.Primitives.StringValues(headers.GetValues(key)));
                    clonedCollection.Remove(key);
                }
                catch { }
            }

            return clonedCollection;
        }

        /// <summary>
        /// Applies the data set in the supplied HttpMessageInfo object to the actual HTTP object.
        /// </summary>
        /// <param name="message">
        /// The HTTP object.
        /// </param>
        /// <param name="messageInfo">
        /// The message info.
        /// </param>
        /// <param name="cancelToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// A boolean value indicating whether or not the operation was a success. Exceptions are
        /// handled and a value of false is returned in the event of an exception.
        /// </returns>
        public static async Task<bool> ApplyMessageInfo(this HttpResponse message, HttpMessageInfo messageInfo, CancellationToken cancelToken)
        {
            try
            {
                if (messageInfo.MessageType == MessageType.Response)
                {
                    var failedHeaders = message.PopulateHeaders(messageInfo.Headers, messageInfo.ExemptedHeaders);
                    message.StatusCode = (int)messageInfo.StatusCode;

#if VERBOSE_WARNINGS
                    foreach (string key in failedHeaders)
                    {
                        LoggerProxy.Default.Warn(string.Format("Failed to add HTTP header with key {0} and with value {1}.", key, failedHeaders[key]));
                    }
#endif

                    if (messageInfo.BodyIsUserCreated && messageInfo.Body.Length > 0)
                    {
                        using (var ms = new MemoryStream(messageInfo.Body.ToArray()))
                        {
                            ms.Position = 0;
                            message.ContentType = messageInfo.BodyContentType;
                            message.Headers.Add("Expires", new Microsoft.Extensions.Primitives.StringValues(TimeUtil.UnixEpochString));

                            await ms.CopyToAsync(message.Body, 4096, cancelToken);
                        }
                    }
                    return true;
                }
            }
            catch (Exception err)
            {
                LoggerProxy.Default.Error(err);
            }

            return false;
        }

        /// <summary>
        /// Clears all headers, including content headers, if any.
        /// </summary>
        /// <param name="message">
        /// The response message.
        /// </param>
        public static void ClearAllHeaders(this HttpResponse message)
        {
            message.Headers.Clear();
        }
    }
}