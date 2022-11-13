using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PTI.Microservices.Library.Models.AzureVideoIndexerService.GetFacesArtifactInfo
{

    public class GetFacesArtifactInfoResponse
    {
        public float version { get; set; }
        public float timescale { get; set; }
        public float offset { get; set; }
        public float framerate { get; set; }
        public float width { get; set; }
        public float height { get; set; }
        public float totalDuration { get; set; }
        public Fragment[] fragments { get; set; }
        public Group[][] groups { get; set; }
    }

    public class Fragment
    {
        public float start { get; set; }
        public float duration { get; set; }
        public float interval { get; set; }
        public Event[][] events { get; set; }
    }

    public class Event
    {
        public float index { get; set; }
        public int id { get; set; }
        public float x { get; set; }
        public float y { get; set; }
        public float width { get; set; }
        public float height { get; set; }
        public bool isDeputy { get; set; }
        public float freq { get; set; }
        public float roll { get; set; }
        public float pitch { get; set; }
        public float yaw { get; set; }
        public float detectionConfidence { get; set; }
    }

    public class Group
    {
        public int id { get; set; }
    }

}
