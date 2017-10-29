using CitadelCore.Net.Handlers;
using CitadelCore.Net.Proxy;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace CitadelCore.Net.Handlers
{
    internal class FilterPassthroughResponseHandler : AbstractFilterResponseHandler
    {
        public FilterPassthroughResponseHandler(MessageBeginCallback messageBeginCallback, MessageEndCallback messageEndCallback) : base(messageBeginCallback, messageEndCallback)
        {
        }

        public override Task Handle(HttpContext context)
        {
            throw new NotImplementedException();
        }
    }
}