using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sangam
{

    // class to hold possible result..
    class LiveCameraResult
    {
        public Microsoft.ProjectOxford.Face.Contract.Face[] Faces { get; set; } = null;
        public Microsoft.ProjectOxford.Common.Contract.EmotionScores[] EmotionScores { get; set; } = null;
        public string[] CelebrityNames { get; set; } = null;
        public Microsoft.ProjectOxford.Vision.Contract.Tag[] Tags { get; set; } = null;
    }
}
