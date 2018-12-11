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
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Threading;

namespace CitadelCore.Extensions
{
    /// <summary>
    /// Extension methods for <seealso cref="HttpResponseMessageExtensions"/> instances.
    /// </summary>
    public static class HttpResponseMessageExtensions
    {
        /// <summary>
        /// Copies all possible headers from the given collection into this HttpResponseMessage
        /// instance and then returns the headers that failed to be added.
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
        public static NameValueCollection PopulateHeaders(this HttpResponseMessage message, NameValueCollection headers, HashSet<string> exemptedHeaders)
        {
            // This will hold whatever headers we cannot successfully add here.
            var clonedCollection = new NameValueCollection(headers);

            foreach (string key in headers)
            {
                if (ForbiddenHttpHeaders.IsForbidden(key))
                {
                    continue;
                }

                if (message.Headers.Contains(key))
                {
                    message.Headers.Remove(key);
                }

                if (message.Headers.TryAddWithoutValidation(key, headers.GetValues(key)))
                {
                    clonedCollection.Remove(key);
                }
                else
                {
                    if (message.Content != null)
                    {
                        if (message.Content.Headers.Contains(key))
                        {
                            message.Content.Headers.Remove(key);
                        }

                        if (message.Content.Headers.TryAddWithoutValidation(key, headers.GetValues(key)))
                        {
                            clonedCollection.Remove(key);
                        }
                    }
                }
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
        public static bool ApplyMessageInfo(this HttpResponseMessage message, HttpMessageInfo messageInfo, CancellationToken cancelToken)
        {
            try
            {
                if (messageInfo.MessageType == MessageType.Request)
                {
                    var failedHeaders = message.PopulateHeaders(messageInfo.Headers, messageInfo.ExemptedHeaders);

                    message.StatusCode = messageInfo.StatusCode;

                    if (messageInfo.BodyIsUserCreated && messageInfo.Body.Length > 0)
                    {
                        message.Content = new ByteArrayContent(messageInfo.Body.ToArray());

                        failedHeaders = message.PopulateHeaders(messageInfo.Headers, messageInfo.ExemptedHeaders);

#if VERBOSE_WARNINGS
                        foreach (string key in failedHeaders)
                        {
                            LoggerProxy.Default.Warn(string.Format("Failed to add HTTP header with key {0} and with value {1}.", key, failedHeaders[key]));
                        }
#endif

                        message.Content.Headers.TryAddWithoutValidation("Expires", TimeUtil.UnixEpochString);

                        if (messageInfo.BodyContentType != null)
                        {
                            message.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(messageInfo.BodyContentType);
                        }
                    }
                    else if (messageInfo.BodyIsUserCreated && messageInfo.Body.Length <= 0)
                    {
                        message.Content = null;
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
        public static void ClearAllHeaders(this HttpResponseMessage message)
        {
            message.Headers.Clear();
            if (message.Content != null && message.Content.Headers != null)
            {
                message.Content.Headers.Clear();
            }
        }

        /// <summary>
        /// Exports all headers, including content headers, if any.
        /// </summary>
        /// <param name="message">
        /// The response message.
        /// </param>
        /// <returns>
        /// A NameValueCollection of all headers.
        /// </returns>
        public static NameValueCollection ExportAllHeaders(this HttpResponseMessage message)
        {
            var retVal = new NameValueCollection();

            foreach (var header in message.Headers)
            {
                if (ForbiddenHttpHeaders.IsForbidden(header.Key))
                {
                    continue;
                }

                foreach (var value in header.Value)
                {
                    retVal.Add(header.Key, value);
                }
            }

            if (message.Content != null && message.Content.Headers != null)
            {
                foreach (var header in message.Content.Headers)
                {
                    if (ForbiddenHttpHeaders.IsForbidden(header.Key))
                    {
                        continue;
                    }

                    foreach (var value in header.Value)
                    {
                        retVal.Add(header.Key, value);
                    }
                }
            }

            return retVal;
        }
    }
}