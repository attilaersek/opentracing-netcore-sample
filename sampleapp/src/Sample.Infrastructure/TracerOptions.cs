using System;

namespace Sample.Infrastructure
{
    public class TracerOptions
    {
        public string ServiceName { get; set; }
        public TracerMode Mode { get; set; }
        public string HttpEndPoint { get; set; }
        public TracerUdpEndPoint UdpEndPoint { get; set; }
    }
}
