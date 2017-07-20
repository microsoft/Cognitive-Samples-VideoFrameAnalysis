using Microsoft.ProjectOxford.Vision.Contract;

namespace Sangam
{
    public class CelebritiesResult
    {
        public Celebrity[] Celebrities { get; set; }
    }

    public class Celebrity
    {
        public string Name { get; set; }
        public FaceRectangle FaceRectangle { get; set; }
        public float Confidence { get; set; }
    }
}