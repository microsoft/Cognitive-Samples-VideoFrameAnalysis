// 
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// 
// Microsoft Cognitive Services: http://www.microsoft.com/cognitive
// 
// Microsoft Cognitive Services Github:
// https://github.com/Microsoft/Cognitive
// 
// Copyright (c) Microsoft Corporation
// All rights reserved.
// 
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ProjectOxford.Emotion.Contract;
using Microsoft.ProjectOxford.Face.Contract;

namespace LiveCameraSample
{
    internal class Aggregation
    {
        public static Tuple<string, float> GetDominantEmotion(Scores scores)
        {
            float maxScore = 0;
            string dominant = "";
            if (scores.Anger > maxScore) { maxScore = scores.Anger; dominant = "Anger"; }
            if (scores.Contempt > maxScore) { maxScore = scores.Contempt; dominant = "Contempt"; }
            if (scores.Disgust > maxScore) { maxScore = scores.Disgust; dominant = "Disgust"; }
            if (scores.Fear > maxScore) { maxScore = scores.Fear; dominant = "Fear"; }
            if (scores.Happiness > maxScore) { maxScore = scores.Happiness; dominant = "Happiness"; }
            if (scores.Neutral > maxScore) { maxScore = scores.Neutral; dominant = "Neutral"; }
            if (scores.Sadness > maxScore) { maxScore = scores.Sadness; dominant = "Sadness"; }
            if (scores.Surprise > maxScore) { maxScore = scores.Surprise; dominant = "Surprise"; }
            return new Tuple<string, float>(dominant, maxScore);
        }

        public static string SummarizeEmotion(Scores scores)
        {
            var bestEmotion = Aggregation.GetDominantEmotion(scores);
            return string.Format("{0}: {1:N1}", bestEmotion.Item1, bestEmotion.Item2);
        }

        public static string SummarizeFaceAttributes(FaceAttributes attr)
        {
            List<string> attrs = new List<string>();
            if (attr.Gender != null) attrs.Add(attr.Gender);
            if (attr.Age > 0) attrs.Add(attr.Age.ToString());
            if (attr.HeadPose != null)
            {
                // Simple rule to estimate whether person is facing camera. 
                bool facing = Math.Abs(attr.HeadPose.Yaw) < 25;
                attrs.Add(facing ? "facing camera" : "not facing camera");
            }
            return string.Join(", ", attrs);
        }
    }
}
