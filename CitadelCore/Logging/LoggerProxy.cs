/*
* Copyright © 2017-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace CitadelCore.Logging
{
    /// <summary>
    /// Delegate for receiving messages from the logger proxy.
    /// </summary>
    /// <param name="msg">
    /// The message to log.
    /// </param>
    public delegate void MessageHandler(string msg);

    /// <summary>
    /// The LoggerProxy class represents a very basic logging interface that the core engine uses
    /// internally to pump messages, agnostic to any receiver. You can/should use this class and the
    /// message handler properties to receive debugging and informational messages that you can
    /// optionally write to a log.
    /// </summary>
    public class LoggerProxy
    {
        private static LoggerProxy s_inst = new LoggerProxy();

        /// <summary>
        /// Info handler. This handler will receive informational messages about internal proxy state
        /// and actions.
        /// </summary>
        public event MessageHandler OnInfo;

        /// <summary>
        /// Warning handler. This handler will receive warning messages about internal proxy state
        /// and actions.
        /// </summary>
        public event MessageHandler OnWarning;

        /// <summary>
        /// Error handler. This handler will receive error messages about internal proxy state and actions.
        /// </summary>
        public event MessageHandler OnError;

        /// <summary>
        /// Singleton because getting fancy here is just showing off and a waste of time.
        /// </summary>
        public static LoggerProxy Default
        {
            get
            {
                return s_inst;
            }
        }

        private LoggerProxy()
        {
        }

        /// <summary>
        /// Logging proxy interface for informational messages.
        /// </summary>
        /// <param name="msg">
        /// The message.
        /// </param>
        /// <param name="callerName">
        /// Courtesy of compiler services, the name of the function from which this method was invoked.
        /// </param>
        /// <param name="callerFilePath">
        /// Courtesy of compiler services, the source file containing the function from which this
        /// method was invoked.
        /// </param>
        /// <param name="callerSourceLineNumber">
        /// Courtesy of compiler services, the line number in the source file from which this method
        /// was invoked.
        /// </param>
        /// <remarks>
        /// Though this is a public function, this is designed to be used by plugins or other such
        /// things extending classes in this binary. Unfortunately this last minute design change has
        /// forced us to make this public.
        /// </remarks>
        public void Info(string msg, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerSourceLineNumber = 0)
        {
            var formatted = string.Format("{0}\t {2}::{1}() #{3}", msg, callerName, Path.GetFileName(callerFilePath), callerSourceLineNumber);
            OnInfo?.Invoke(formatted);
        }

        /// <summary>
        /// Logging proxy interface for warning messages.
        /// </summary>
        /// <param name="msg">
        /// The message.
        /// </param>
        /// <param name="callerName">
        /// Courtesy of compiler services, the name of the function from which this method was invoked.
        /// </param>
        /// <param name="callerFilePath">
        /// Courtesy of compiler services, the source file containing the function from which this
        /// method was invoked.
        /// </param>
        /// <param name="callerSourceLineNumber">
        /// Courtesy of compiler services, the line number in the source file from which this method
        /// was invoked.
        /// </param>
        /// <remarks>
        /// Though this is a public function, this is designed to be used by plugins or other such
        /// things extending classes in this binary. Unfortunately this last minute design change has
        /// forced us to make this public.
        /// </remarks>
        public void Warn(string msg, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerSourceLineNumber = 0)
        {
            var formatted = string.Format("{0}\t {2}::{1}() #{3}", msg, callerName, Path.GetFileName(callerFilePath), callerSourceLineNumber);
            OnWarning?.Invoke(formatted);
        }

        /// <summary>
        /// Logging proxy interface for error messages.
        /// </summary>
        /// <param name="msg">
        /// The message.
        /// </param>
        /// <param name="callerName">
        /// Courtesy of compiler services, the name of the function from which this method was invoked.
        /// </param>
        /// <param name="callerFilePath">
        /// Courtesy of compiler services, the source file containing the function from which this
        /// method was invoked.
        /// </param>
        /// <param name="callerSourceLineNumber">
        /// Courtesy of compiler services, the line number in the source file from which this method
        /// was invoked.
        /// </param>
        /// <remarks>
        /// Though this is a public function, this is designed to be used by plugins or other such
        /// things extending classes in this binary. Unfortunately this last minute design change has
        /// forced us to make this public.
        /// </remarks>
        public void Error(string msg, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerSourceLineNumber = 0)
        {
            var formatted = string.Format("{0}\t {2}::{1}() #{3}", msg, callerName, Path.GetFileName(callerFilePath), callerSourceLineNumber);
            OnError?.Invoke(formatted);
        }

        /// <summary>
        /// Logging proxy interface for exception objects. Recursively writes the exception and
        /// stacktrace to a string, before piping that final string to listeners.
        /// </summary>
        /// <param name="e">
        /// The exception.
        /// </param>
        /// <param name="callerName">
        /// Courtesy of compiler services, the name of the function from which this method was invoked.
        /// </param>
        /// <param name="callerFilePath">
        /// Courtesy of compiler services, the source file containing the function from which this
        /// method was invoked.
        /// </param>
        /// <param name="callerSourceLineNumber">
        /// Courtesy of compiler services, the line number in the source file from which this method
        /// was invoked.
        /// </param>
        /// <remarks>
        /// Though this is a public function, this is designed to be used by plugins or other such
        /// things extending classes in this binary. Unfortunately this last minute design change has
        /// forced us to make this public.
        /// </remarks>
        public void Error(Exception e, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerSourceLineNumber = 0)
        {
            var b = new StringBuilder();
            while (e != null)
            {
                b.Append(e.Message);
                b.Append(e.StackTrace);
                e = e.InnerException;
            }

            b.AppendFormat("\n\t From {1}::{0}() #{2}", callerName, Path.GetFileName(callerFilePath), callerSourceLineNumber);

            OnError?.Invoke(b.ToString());
        }
    }
}