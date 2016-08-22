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

// Uncomment this to enable the LogMessage function, which can with debugging timing issues.
// #define TRACE_GRABBER

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;

namespace VideoFrameAnalyzer
{
    /// <summary> A frame grabber. </summary>
    /// <typeparam name="AnalysisResultType"> Type of the analysis result. This is the type that
    ///     the AnalysisFunction will return, when it calls some API on a video frame. </typeparam>
    public class FrameGrabber<AnalysisResultType>
    {
        #region Types

        /// <summary> Additional information for new frame events. </summary>
        /// <seealso cref="T:System.EventArgs"/>
        public class NewFrameEventArgs : EventArgs
        {
            public NewFrameEventArgs(VideoFrame frame)
            {
                Frame = frame;
            }
            public VideoFrame Frame { get; }
        }

        /// <summary> Additional information for new result events, which occur when an API call
        ///     returns. </summary>
        /// <seealso cref="T:System.EventArgs"/>
        public class NewResultEventArgs : EventArgs
        {
            public NewResultEventArgs(VideoFrame frame)
            {
                Frame = frame;
            }
            public VideoFrame Frame { get; }
            public AnalysisResultType Analysis { get; set; } = default(AnalysisResultType);
            public bool TimedOut { get; set; } = false;
            public Exception Exception { get; set; } = null;
        }

        #endregion Types

        #region Properties

        /// <summary> Gets or sets the analysis function. The function can be any asynchronous
        ///     operation that accepts a <see cref="VideoFrame"/> and returns a
        ///     <see cref="Task{AnalysisResultType}"/>. </summary>
        /// <value> The analysis function. </value>
        /// <example> This example shows how to provide an analysis function using a lambda expression.
        ///     <code>
        ///     var client = new EmotionServiceClient("subscription key");
        ///     var grabber = new FrameGrabber();
        ///     grabber.AnalysisFunction = async (frame) =&gt; { return await client.RecognizeAsync(frame.Image.ToMemoryStream(".jpg")); };
        ///     </code></example>
        public Func<VideoFrame, Task<AnalysisResultType>> AnalysisFunction { get; set; } = null;

        /// <summary> Gets or sets the analysis timeout. When executing the
        ///     <see cref="AnalysisFunction"/> on a video frame, if the call doesn't return a
        ///     result within this time, it is abandoned and no result is returned for that
        ///     frame. </summary>
        /// <value> The analysis timeout. </value>
        public TimeSpan AnalysisTimeout { get; set; } = TimeSpan.FromMilliseconds(5000);

        public bool IsRunning { get { return _analysisTaskQueue != null; } }

        public double FrameRate
        {
            get { return _fps; }
            set
            {
                _fps = value;
                if (_timer != null)
                {
                    _timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1.0 / _fps));
                }
            }
        }

        public int Width { get; protected set; }
        public int Height { get; protected set; }

        #endregion Properties

        #region Fields

        protected Predicate<VideoFrame> _analysisPredicate = null;
        protected VideoCapture _reader = null;
        protected Timer _timer = null;
        protected SemaphoreSlim _timerMutex = new SemaphoreSlim(1);
        protected AutoResetEvent _frameGrabTimer = new AutoResetEvent(false);
        protected bool _stopping = false;
        protected Task _producerTask = null;
        protected Task _consumerTask = null;
        protected BlockingCollection<Task<NewResultEventArgs>> _analysisTaskQueue = null;
        protected bool _resetTrigger = true;
        protected int _numCameras = -1;
        protected int _currCameraIdx = -1;
        protected double _fps = 0;

        #endregion Fields

        #region Methods

        public FrameGrabber()
        {
        }

        /// <summary> (Only available in TRACE_GRABBER builds) logs a message. </summary>
        /// <param name="format"> Describes the format to use. </param>
        /// <param name="args">   Event information. </param>
        [Conditional("TRACE_GRABBER")]
        protected void LogMessage(string format, params object[] args)
        {
            ConcurrentLogger.WriteLine(String.Format(format, args));
        }

        /// <summary> Starts processing frames from a live camera. Stops any current video source
        ///     before starting the new source. </summary>
        /// <returns> A Task. </returns>
        public async Task StartProcessingCameraAsync(int cameraIndex = 0, double overrideFPS = 0)
        {
            // Check to see if we're re-opening the same camera. 
            if (_reader != null && _reader.CaptureType == CaptureType.Camera && cameraIndex == _currCameraIdx)
            {
                return;
            }

            await StopProcessingAsync().ConfigureAwait(false);

            _reader = new VideoCapture(cameraIndex);

            _fps = overrideFPS;

            if (_fps == 0)
            {
                _fps = 30;
            }

            Width = _reader.FrameWidth;
            Height = _reader.FrameHeight;

            StartProcessing(TimeSpan.FromSeconds(1 / _fps), () => DateTime.Now);

            _currCameraIdx = cameraIndex;
        }

        /// <summary> Starts capturing and processing video frames. </summary>
        /// <param name="frameGrabDelay"> The frame grab delay. </param>
        /// <param name="timestampFn">    Function to generate the timestamp for each frame. This
        ///     function will get called once per frame. </param>
        protected void StartProcessing(TimeSpan frameGrabDelay, Func<DateTime> timestampFn)
        {
            OnProcessingStarting();

            _resetTrigger = true;
            _frameGrabTimer.Reset();
            _analysisTaskQueue = new BlockingCollection<Task<NewResultEventArgs>>();

            var timerIterations = 0;

            // Create a background thread that will grab frames in a loop.
            _producerTask = Task.Factory.StartNew(() =>
            {
                var frameCount = 0;
                while (!_stopping)
                {
                    LogMessage("Producer: waiting for timer to trigger frame-grab");

                    // Wait to get released by the timer. 
                    _frameGrabTimer.WaitOne();
                    LogMessage("Producer: grabbing frame...");

                    var startTime = DateTime.Now;

                    // Grab single frame. 
                    var timestamp = timestampFn();
                    Mat image = new Mat();
                    bool success = _reader.Read(image);

                    LogMessage("Producer: frame-grab took {0} ms", (DateTime.Now - startTime).Milliseconds);

                    if (!success)
                    {
                        // If we've reached the end of the video, stop here. 
                        if (_reader.CaptureType == CaptureType.File)
                        {
                            LogMessage("Producer: null frame from video file, stop!");
                            // This will call StopProcessing on a new thread.
                            var stopTask = StopProcessingAsync();
                            // Break out of the loop to make sure we don't try grabbing more
                            // frames. 
                            break;
                        }
                        else
                        {
                            // If failed on live camera, try again. 
                            LogMessage("Producer: null frame from live camera, continue!");
                            continue;
                        }
                    }

                    // Package the image for submission.
                    VideoFrameMetadata meta;
                    meta.Index = frameCount;
                    meta.Timestamp = timestamp;
                    VideoFrame vframe = new VideoFrame(image, meta);

                    // Raise the new frame event
                    LogMessage("Producer: new frame provided, should analyze? Frame num: {0}", meta.Index);
                    OnNewFrameProvided(vframe);

                    if (_analysisPredicate(vframe))
                    {
                        LogMessage("Producer: analyzing frame");

                        // Call the analysis function on a threadpool thread
                        var analysisTask = DoAnalyzeFrame(vframe);

                        LogMessage("Producer: adding analysis task to queue {0}", analysisTask.Id);

                        // Push the frame onto the queue
                        _analysisTaskQueue.Add(analysisTask);
                    }
                    else
                    {
                        LogMessage("Producer: not analyzing frame");
                    }

                    LogMessage("Producer: iteration took {0} ms", (DateTime.Now - startTime).Milliseconds);

                    ++frameCount;
                }

                LogMessage("Producer: stopping, destroy reader and timer");
                _analysisTaskQueue.CompleteAdding();

                // We reach this point by breaking out of the while loop. So we must be stopping. 
                _reader.Dispose();
                _reader = null;

                // Make sure the timer stops, then get rid of it. 
                var h = new ManualResetEvent(false);
                _timer.Dispose(h);
                h.WaitOne();
                _timer = null;

                LogMessage("Producer: stopped");
            }, TaskCreationOptions.LongRunning);

            _consumerTask = Task.Factory.StartNew(async () =>
            {
                while (!_analysisTaskQueue.IsCompleted)
                {
                    LogMessage("Consumer: waiting for task to get added");

                    // Get the next processing task. 
                    Task<NewResultEventArgs> nextTask = null;

                    // Blocks if m_analysisTaskQueue.Count == 0
                    // IOE means that Take() was called on a completed collection.
                    // Some other thread can call CompleteAdding after we pass the
                    // IsCompleted check but before we call Take. 
                    // In this example, we can simply catch the exception since the 
                    // loop will break on the next iteration.
                    // See https://msdn.microsoft.com/en-us/library/dd997371(v=vs.110).aspx
                    try
                    {
                        nextTask = _analysisTaskQueue.Take();
                    }
                    catch (InvalidOperationException) { }

                    if (nextTask != null)
                    {
                        // Block until the result becomes available. 
                        LogMessage("Consumer: waiting for next result to arrive for task {0}", nextTask.Id);
                        var result = await nextTask;

                        // Raise the new result event. 
                        LogMessage("Consumer: got result for frame {0}. {1} tasks in queue", result.Frame.Metadata.Index, _analysisTaskQueue.Count);
                        OnNewResultAvailable(result);
                    }
                }

                LogMessage("Consumer: stopped");
            }, TaskCreationOptions.LongRunning);

            // Set up a timer object that will trigger the frame-grab at a regular interval.
            _timer = new Timer(async s /* state */ =>
            {
                await _timerMutex.WaitAsync();
                try
                {
                    // If the handle was not reset by the producer, then the frame-grab was missed.
                    bool missed = _frameGrabTimer.WaitOne(0);

                    _frameGrabTimer.Set();

                    if (missed)
                    {
                        LogMessage("Timer: missed frame-grab {0}", timerIterations - 1);
                    }
                    LogMessage("Timer: grab frame num {0}", timerIterations);
                    ++timerIterations;
                }
                finally
                {
                    _timerMutex.Release();
                }
            }, null, TimeSpan.Zero, frameGrabDelay);

            OnProcessingStarted();
        }

        /// <summary> Stops capturing and processing video frames. </summary>
        /// <returns> A Task. </returns>
        public async Task StopProcessingAsync()
        {
            OnProcessingStopping();

            _stopping = true;
            _frameGrabTimer.Set();
            if (_producerTask != null)
            {
                await _producerTask;
                _producerTask = null;
            }
            if (_consumerTask != null)
            {
                await _consumerTask;
                _consumerTask = null;
            }
            if (_analysisTaskQueue != null)
            {
                _analysisTaskQueue.Dispose();
                _analysisTaskQueue = null;
            }
            _stopping = false;

            OnProcessingStopped();
        }

        /// <summary> Trigger analysis on an arbitrary predicate. </summary>
        /// <param name="predicate"> The predicate to use to decide whether or not to analyze a
        ///     particular <see cref="VideoFrame"/>. The function must accept two arguments: the
        ///     <see cref="VideoFrame"/> in question, and a boolean flag that indicates the
        ///     trigger should be reset (e.g. on the first frame, or when the predicate has
        ///     changed). </param>
        public void TriggerAnalysisOnPredicate(Func<VideoFrame, bool, bool> predicate)
        {
            _resetTrigger = true;
            _analysisPredicate = (VideoFrame frame) =>
            {
                bool shouldCall = predicate(frame, _resetTrigger);
                _resetTrigger = false;
                return shouldCall;
            };
        }

        /// <summary> Trigger analysis at a regular interval. </summary>
        /// <param name="interval"> The time interval to wait between analyzing frames. </param>
        public void TriggerAnalysisOnInterval(TimeSpan interval)
        {
            _resetTrigger = true;

            // Keep track of the next timestamp to trigger. 
            DateTime nextCall = DateTime.MinValue;
            _analysisPredicate = (VideoFrame frame) =>
            {
                bool shouldCall = false;

                // If this is the first frame, then trigger and initialize the timer. 
                if (_resetTrigger)
                {
                    _resetTrigger = false;
                    nextCall = frame.Metadata.Timestamp;
                    shouldCall = true;
                }
                else
                {
                    shouldCall = frame.Metadata.Timestamp > nextCall;
                }

                // Return. 
                if (shouldCall)
                {
                    nextCall += interval;
                    return true;
                }
                return false;
            };
        }

        /// <summary> Gets the number of cameras available. Caution, the first time this function
        ///     is called, it opens each camera. Thus, it should be called before starting any
        ///     camera. </summary>
        /// <returns> The number cameras. </returns>
        public int GetNumCameras()
        {
            // Count cameras manually
            if (_numCameras == -1)
            {
                _numCameras = 0;
                while (_numCameras < 100)
                {
                    using (var vc = VideoCapture.FromCamera(_numCameras))
                    {
                        if (vc.IsOpened())
                            ++_numCameras;
                        else
                            break;
                    }
                }
            }

            return _numCameras;
        }

        /// <summary> Raises the processing starting event. </summary>
        protected void OnProcessingStarting()
        {
            ProcessingStarting?.Invoke(this, null);
        }

        /// <summary> Raises the processing started event. </summary>
        protected void OnProcessingStarted()
        {
            ProcessingStarted?.Invoke(this, null);
        }

        /// <summary> Raises the processing stopping event. </summary>
        protected void OnProcessingStopping()
        {
            ProcessingStopping?.Invoke(this, null);
        }

        /// <summary> Raises the processing stopped event. </summary>
        protected void OnProcessingStopped()
        {
            ProcessingStopped?.Invoke(this, null);
        }

        /// <summary> Raises the new frame provided event. </summary>
        /// <param name="frame"> The frame. </param>
        protected void OnNewFrameProvided(VideoFrame frame)
        {
            NewFrameProvided?.Invoke(this, new NewFrameEventArgs(frame));
        }

        /// <summary> Raises the new result event. </summary>
        /// <param name="args"> Event information to send to registered event handlers. </param>
        protected void OnNewResultAvailable(NewResultEventArgs args)
        {
            NewResultAvailable?.Invoke(this, args);
        }

        /// <summary> Executes the analysis operation asynchronously, then returns either the
        ///     result, or any exception that was thrown. </summary>
        /// <param name="frame"> The frame. </param>
        /// <returns> A Task&lt;NewResultEventArgs&gt; </returns>
        protected async Task<NewResultEventArgs> DoAnalyzeFrame(VideoFrame frame)
        {
            CancellationTokenSource source = new CancellationTokenSource();

            // Make a local reference to the function, just in case someone sets
            // AnalysisFunction = null before we can call it. 
            var fcn = AnalysisFunction;
            if (fcn != null)
            {
                NewResultEventArgs output = new NewResultEventArgs(frame);
                var task = fcn(frame);
                LogMessage("DoAnalysis: started task {0}", task.Id);
                try
                {
                    if (task == await Task.WhenAny(task, Task.Delay(AnalysisTimeout, source.Token)))
                    {
                        output.Analysis = await task;
                        source.Cancel();
                    }
                    else
                    {
                        output.TimedOut = true;
                    }
                }
                catch (Exception ae)
                {
                    output.Exception = ae;
                }

                LogMessage("DoAnalysis: returned from task {0}", task.Id);

                return output;
            }
            else
            {
                return null;
            }
        }

        #endregion Methods

        #region Events

        public event EventHandler ProcessingStarting;
        public event EventHandler ProcessingStarted;
        public event EventHandler ProcessingStopping;
        public event EventHandler ProcessingStopped;
        public event EventHandler<NewFrameEventArgs> NewFrameProvided;
        public event EventHandler<NewResultEventArgs> NewResultAvailable;

        #endregion Events
    }
}
