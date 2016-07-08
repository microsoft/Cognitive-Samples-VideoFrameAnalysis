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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace VideoFrameAnalyzer
{
    public static class ConcurrentLogger
    {
        private readonly static SemaphoreSlim s_printMutex = new SemaphoreSlim(1);
        private readonly static BlockingCollection<string> s_messageQueue = new BlockingCollection<string>();

        public static void WriteLine(string message)
        {
            var timestamp = DateTime.Now;
            // Push the message on the queue
            s_messageQueue.Add(timestamp.ToString("o") + ": " + message);
            // Start a new task that will dequeue one message and print it. The tasks will not
            // necessarily run in order, but since each task just takes the oldest message and
            // prints it, the messages will print in order. 
            Task.Run(async () =>
            {
                // Wait to get access to the queue. 
                await s_printMutex.WaitAsync();
                try
                {
                    string msg = s_messageQueue.Take();
                    Console.WriteLine(msg);
                }
                finally
                {
                    s_printMutex.Release();
                }
            });
        }
    }
}
