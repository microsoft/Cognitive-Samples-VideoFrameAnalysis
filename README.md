# Video Frame Analysis Sample

This sample contains a library, along with two applications, for analyzing video frames from a webcam in near-real-time using APIs from [Microsoft Cognitive Services][]. The library and applications are implemented in C#, and use the [OpenCvSharp][] package for webcam support. 

[Microsoft Cognitive Services]: https://www.microsoft.com/cognitive-services
[OpenCvSharp]:                  https://github.com/shimat/opencvsharp

## Getting Started

1. Get API keys for the Vision APIs from [microsoft.com/cognitive][Sign-Up]. For video frame analysis, the applicable APIs are:
    - [Computer Vision API][]
    - [Emotion API][]
    - [Face API][]
2. Open the sample in Visual Studio 2015, build and run the sample applications:
    - For BasicConsoleSample, the Face API key is hard-coded directly in [BasicConsoleSample/Program.cs](Windows/BasicConsoleSample/Program.cs).
    - For LiveCameraSample, the keys should be entered into the Settings pane of the app. They will be persisted across sessions as user data.
3. Reference the VideoFrameAnalyzer library from your own projects.

[Sign-Up]:             https://www.microsoft.com/cognitive-services/en-us/sign-up
[Computer Vision API]: https://www.microsoft.com/cognitive-services/en-us/computer-vision-api
[Emotion API]:         https://www.microsoft.com/cognitive-services/en-us/emotion-api
[Face API]:            https://www.microsoft.com/cognitive-services/en-us/face-api

## Using the VideoFrameAnalyzer Library

You can start using the library with only a few lines of code:
```csharp
// Create Face API Client. 
FaceServiceClient faceClient = new FaceServiceClient("<subscription key>");
// Create grabber, with analysis type Face[]. 
FrameGrabber<Face[]> grabber = new FrameGrabber<Face[]>();
// Set up Face API call, which returns a Face[]. Simply encodes image and submits to Face API. 
grabber.AnalysisFunction = async frame => return await faceClient.DetectAsync(frame.Image.ToMemoryStream(".jpg"));
// Tell grabber to call the Face API every 3 seconds. 
grabber.TriggerAnalysisOnInterval(TimeSpan.FromMilliseconds(3000));
// Start running. 
await grabber.StartProcessingCameraAsync();
```

## Contributing

We welcome contributions. Feel free to file issues and pull requests on the repo and we'll address them as we can. Learn more about how you can help on our [Contribution Rules & Guidelines](CONTRIBUTING.md). 

You can reach out to us anytime with questions and suggestions using our communities below:
 - **Support questions:** [StackOverflow][]
 - **Feedback & feature requests:** [Cognitive Services UserVoice Forum][]

This project has adopted the [Microsoft Open Source Code of Conduct][]. For more information see the [Code of Conduct FAQ][] or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

[StackOverflow]:                         https://stackoverflow.com/questions/tagged/microsoft-cognitive
[Cognitive Services UserVoice Forum]:    https://cognitive.uservoice.com
[Microsoft Open Source Code of Conduct]: https://opensource.microsoft.com/codeofconduct/
[Code of Conduct FAQ]:                   https://opensource.microsoft.com/codeofconduct/faq/

## License

All Microsoft Cognitive Services SDKs and samples are licensed with the MIT License. For more details, see [LICENSE](LICENSE.md).

## Developer Code of Conduct

The image, voice, video or text understanding capabilities of VideoFrameAnalyzer use Microsoft Cognitive Services. Microsoft will receive the images, audio, video, and other data that you upload (via this app) for service improvement purposes. To report abuse of the Microsoft Cognitive Services to Microsoft, please visit the Microsoft Cognitive Services website at https://www.microsoft.com/cognitive-services, and use the "Report Abuse" link at the bottom of the page to contact Microsoft. For more information about Microsoft privacy policies please see their privacy statement here: https://go.microsoft.com/fwlink/?LinkId=521839.

Developers using Cognitive Services, including this sample, are expected to follow the "Developer Code of Conduct for Microsoft Cognitive Services", found at http://go.microsoft.com/fwlink/?LinkId=698895.
